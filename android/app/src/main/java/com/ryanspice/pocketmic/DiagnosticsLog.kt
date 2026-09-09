package com.ryanspice.pocketmic

import android.os.SystemClock
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class DiagnosticLogEntry(val atMillis: Long, val message: String)

/**
 * A rolling log of state transitions, built by watching [StreamingState] and [ControlChannel]
 * rather than by requiring every call site that changes state to also remember to log it.
 *
 * [MicStreamingService] and [MainActivity] are off limits for this task, which turns out to be
 * the right constraint anyway: those two already publish everything worth logging into
 * [StreamingState] and [ControlChannel] as StateFlows. Watching those flows and emitting a line
 * whenever a value meaningfully changes gives a complete event log for free, and it means a
 * future producer of state changes is logged automatically without ever touching this file.
 *
 * [ensureStarted] is idempotent and cheap to call from every composition of the diagnostics
 * screen; the collectors keep running for the process lifetime once started; there is nothing to
 * tear down deliberately, since a background collector on a couple of low-frequency StateFlows
 * costs nothing while the app process is alive and there is no lifecycle boundary tighter than
 * that to hang it on.
 */
object DiagnosticsLog {
    private const val CAPACITY = 200

    private val mutableEntries = MutableStateFlow<List<DiagnosticLogEntry>>(emptyList())
    val entries: StateFlow<List<DiagnosticLogEntry>> = mutableEntries.asStateFlow()

    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Default)
    private var started = false

    @Synchronized
    fun ensureStarted() {
        if (started) return
        started = true
        observeStreamingStatus()
        observePeer()
        observeStats()
        observeStaleAnnounces()
        observeBindError()
    }

    fun append(message: String) {
        // update() is a CAS loop, not a plain read-modify-write, because several collectors
        // above run as independent coroutines on Dispatchers.Default and can append concurrently.
        mutableEntries.update { (it + DiagnosticLogEntry(System.currentTimeMillis(), message)).takeLast(CAPACITY) }
    }

    private fun observeStreamingStatus() {
        scope.launch {
            var previous: StreamStatus? = null
            var reconnectingSince = 0L
            StreamingState.state.collect { snapshot ->
                if (snapshot.status != previous) {
                    val suffix = snapshot.error?.let { ": $it" }.orEmpty()
                    append("Status -> ${snapshot.status}$suffix")

                    // How long an outage lasted is the number that separates a Wi-Fi roam from a
                    // receiver that was closed and reopened, and it is unrecoverable afterwards
                    // unless it is written down as it happens.
                    if (snapshot.status == StreamStatus.RECONNECTING) {
                        reconnectingSince = snapshot.reconnectingSinceMillis
                    } else if (previous == StreamStatus.RECONNECTING && reconnectingSince > 0L) {
                        val seconds = (SystemClock.elapsedRealtime() - reconnectingSince) / 1_000.0
                        append(
                            "Reconnect ${if (snapshot.status == StreamStatus.STREAMING) "succeeded" else "ended"} " +
                                "after %.1f s (total this session: ${snapshot.reconnects})".format(seconds),
                        )
                        reconnectingSince = 0L
                    }

                    previous = snapshot.status
                }
            }
        }
    }

    private fun observePeer() {
        scope.launch {
            var previous: PeerState? = null
            ControlChannel.peer.collect { peer ->
                if (peer == previous) return@collect
                previous = peer
                append(
                    when (peer) {
                        is PeerState.Searching -> "Discovery: searching"
                        is PeerState.NotFound -> "Discovery: no receiver found"
                        is PeerState.VersionMismatch ->
                            "Discovery: receiver at ${peer.address} speaks control protocol " +
                                "v${peer.receiverControlVersion}, this app speaks v${ControlProtocol.VERSION}"

                        is PeerState.Found -> when {
                            !peer.keyMatches ->
                                "Discovery: receiver at ${peer.address}, pairing key mismatch"

                            !peer.audioProtocolMatches ->
                                "Discovery: receiver at ${peer.address} sends audio protocol " +
                                    "v${peer.audioProtocolVersion}, this app speaks v${PacketCrypto.VERSION}"

                            else -> {
                                val build = if (peer.versionsKnown && peer.buildVersion.isNotBlank()) {
                                    " (${peer.frontEnd} ${peer.buildVersion})"
                                } else {
                                    ""
                                }
                                "Discovery: found ${peer.hostName.ifBlank { "receiver" }} at ${peer.address}$build"
                            }
                        }
                    },
                )
            }
        }
    }

    private fun observeStats() {
        scope.launch {
            var seenFirstReply = false
            var previousRejected = 0L
            ControlChannel.stats.collect { stats ->
                if (stats == null) return@collect
                if (!seenFirstReply) {
                    seenFirstReply = true
                    append("Receiver confirmed delivery: first STATS reply arrived")
                }
                if (stats.rejected > 0 && previousRejected == 0L) {
                    append("Receiver started rejecting packets (${stats.rejected}) - check the pairing key")
                }
                previousRejected = stats.rejected
            }
        }
    }

    private fun observeStaleAnnounces() {
        scope.launch {
            var previous = 0L
            ControlChannel.staleAnnounceCount.collect { count ->
                if (count > previous) {
                    append(
                        "Discovery: ignored ${count - previous} stale announce" +
                            if (count - previous == 1L) "" else "s" +
                            " (replayed or delayed reply)",
                    )
                }
                previous = count
            }
        }
    }

    private fun observeBindError() {
        scope.launch {
            ControlChannel.bindError.collect { error ->
                if (error != null) append("Control channel error: $error")
            }
        }
    }
}
