package com.ryanspice.pocketmic

import android.content.Context
import android.net.ConnectivityManager
import android.net.LinkAddress
import android.os.SystemClock
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.Inet4Address
import java.net.InetAddress
import java.net.InetSocketAddress
import java.security.SecureRandom

/** What discovery currently knows about the Windows receiver. */
sealed interface PeerState {
    data object Searching : PeerState

    data object NotFound : PeerState

    data class Found(
        val address: String,
        val hostName: String,
        val audioPort: Int,
        val keyMatches: Boolean,
        /**
         * False when the receiver predates the announce version trailer. Its version fields are
         * then meaningless and [audioProtocolMatches] is optimistic by design — a receiver that
         * never stated a version must not be accused of a mismatch.
         */
        val versionsKnown: Boolean = false,
        val audioProtocolVersion: Int = 0,
        val audioProtocolMatches: Boolean = true,
        val frontEnd: String = "",
        val buildVersion: String = "",
    ) : PeerState

    /**
     * A receiver answered on the control port but framed its reply with a version this app
     * cannot read, so nothing inside it can be trusted — not even the host name.
     *
     * This exists because the alternative is silence. [ControlProtocol.parse] drops such a frame,
     * and without this state a newer receiver on the network would look exactly like no receiver
     * at all: the single most misleading outcome discovery can produce.
     */
    data class VersionMismatch(
        val address: String,
        val receiverControlVersion: Int,
    ) : PeerState
}

/**
 * Owns the single UDP control socket. Discovery probes and receiver statistics share it so
 * only one port needs binding and only one firewall rule is required.
 *
 * The audio path cannot report delivery: its socket is intentionally unconnected, so `send()`
 * succeeds whether or not anything is listening. Everything here exists to give the phone a
 * truthful answer instead of an optimistic one.
 */
object ControlChannel {
    private const val PROBE_INTERVAL_MS = 1_000L
    private const val NOT_FOUND_AFTER_MS = 4_000L
    const val STATS_TIMEOUT_MS = 2_000L

    /**
     * An announce is only fresh when it echoes a probe this phone sent in the current round or
     * the one before. A genuine reply can be delayed by at most one round; anything older is
     * either a recorded replay or a reply so stale it should not be believed.
     */
    private const val FRESH_NONCE_WINDOW = 2

    private val random = SecureRandom()
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val sentProbeNonces = ProbeNonceRing(FRESH_NONCE_WINDOW)

    private val mutablePeer = MutableStateFlow<PeerState>(PeerState.NotFound)
    val peer: StateFlow<PeerState> = mutablePeer.asStateFlow()

    private val mutableStats = MutableStateFlow<ReceiverStats?>(null)
    val stats: StateFlow<ReceiverStats?> = mutableStats.asStateFlow()

    private val mutableStatsAt = MutableStateFlow(0L)
    val lastStatsAtMillis: StateFlow<Long> = mutableStatsAt.asStateFlow()

    private val mutableStaleAnnounces = MutableStateFlow(0L)
    val staleAnnounceCount: StateFlow<Long> = mutableStaleAnnounces.asStateFlow()

    private val mutableBindError = MutableStateFlow<String?>(null)
    val bindError: StateFlow<String?> = mutableBindError.asStateFlow()

    @Volatile
    private var socket: DatagramSocket? = null

    @Volatile
    private var controlKey: ByteArray? = null

    @Volatile
    private var probing = false

    private var receiveJob: Job? = null
    private var probeJob: Job? = null
    private var startedPort = 0

    @Synchronized
    fun start(context: Context, pairingKey: String, audioPort: Int, probe: Boolean) {
        val port = ControlProtocol.controlPort(audioPort)
        controlKey = ControlProtocol.deriveControlKey(pairingKey)
        probing = probe

        if (socket != null && startedPort == port) {
            if (probe) restartProbeLoop(context, audioPort)
            return
        }

        stopInternal()
        startedPort = port

        val bound = runCatching {
            DatagramSocket(null).apply {
                reuseAddress = true
                broadcast = true
                bind(InetSocketAddress(port))
            }
        }.getOrElse { error ->
            mutableBindError.value =
                "Could not open control port $port (${error.message ?: "unknown"}). " +
                    "Discovery and delivery confirmation are unavailable."
            null
        } ?: return

        mutableBindError.value = null
        socket = bound
        receiveJob = scope.launch { receiveLoop(bound) }
        if (probe) restartProbeLoop(context, audioPort)
    }

    @Synchronized
    fun setProbing(context: Context, audioPort: Int, enabled: Boolean) {
        probing = enabled
        if (enabled) restartProbeLoop(context, audioPort) else probeJob?.cancel()
    }

    private fun restartProbeLoop(context: Context, audioPort: Int) {
        probeJob?.cancel()
        val appContext = context.applicationContext
        probeJob = scope.launch { probeLoop(appContext, audioPort) }
    }

    /**
     * Pushes voice-processing settings to the receiver. Fire and forget over UDP: the sliders
     * are continuous, so a dropped update is corrected by the next one within a moment, and
     * retrying would be more complexity than the problem deserves.
     */
    fun sendConfig(dsp: DspSettings, audioPort: Int) {
        val key = controlKey ?: return
        val active = socket ?: return
        val target = (peer.value as? PeerState.Found)?.address ?: return

        scope.launch {
            runCatching {
                val frame = ControlProtocol.build(
                    key,
                    ControlProtocol.TYPE_CONFIG,
                    ControlProtocol.configPayload(dsp),
                )
                active.send(
                    DatagramPacket(
                        frame,
                        frame.size,
                        InetAddress.getByName(target),
                        ControlProtocol.controlPort(audioPort),
                    ),
                )
            }
        }
    }

    @Synchronized
    fun stop() {
        stopInternal()
        mutablePeer.value = PeerState.NotFound
        mutableStats.value = null
        mutableStatsAt.value = 0L
        mutableStaleAnnounces.value = 0L
    }

    private fun stopInternal() {
        probeJob?.cancel()
        receiveJob?.cancel()
        probeJob = null
        receiveJob = null
        runCatching { socket?.close() }
        socket = null
        startedPort = 0
    }

    private suspend fun probeLoop(context: Context, audioPort: Int) {
        val firstProbeAt = SystemClock.elapsedRealtime()
        mutablePeer.value = PeerState.Searching

        while (currentScopeActive() && probing) {
            val key = controlKey
            val active = socket
            if (key == null || active == null) break

            val targets = broadcastTargets(context)
            if (targets.isEmpty()) {
                mutablePeer.value = PeerState.NotFound
            } else {
                val nonce = ByteArray(ControlProtocol.NONCE_SIZE).also(random::nextBytes)
                // Remembered before the send so a reply can never race ahead of the record.
                sentProbeNonces.remember(nonce)
                val frame = ControlProtocol.build(
                    key,
                    ControlProtocol.TYPE_PROBE,
                    ControlProtocol.probePayload(nonce),
                )
                val controlPort = ControlProtocol.controlPort(audioPort)
                for (target in targets) {
                    runCatching {
                        active.send(DatagramPacket(frame, frame.size, target, controlPort))
                    }
                }
            }

            if (mutablePeer.value is PeerState.Searching &&
                SystemClock.elapsedRealtime() - firstProbeAt > NOT_FOUND_AFTER_MS
            ) {
                mutablePeer.value = PeerState.NotFound
            }
            delay(PROBE_INTERVAL_MS)
        }
    }

    private suspend fun receiveLoop(active: DatagramSocket) {
        val buffer = ByteArray(ControlProtocol.FRAME_SIZE)
        while (currentScopeActive()) {
            val packet = DatagramPacket(buffer, buffer.size)
            val received = runCatching { active.receive(packet); true }.getOrElse { false }
            if (!received) {
                if (active.isClosed) return
                continue
            }

            val key = controlKey ?: continue
            val message = ControlProtocol.parse(packet.data, packet.length)
            if (message == null) {
                reportIfVersionMismatch(packet)
                continue
            }
            val authentic = ControlProtocol.verify(key, message)

            when (message.type) {
                ControlProtocol.TYPE_ANNOUNCE -> {
                    val announce = ControlProtocol.readAnnounce(message.payload)

                    // Freshness gate. The announce must echo a probe this phone actually sent
                    // in the last two rounds; the HMAC proves the sender holds the key, but a
                    // recorded replay of a genuine exchange also passes the HMAC. An announce
                    // carrying an older nonce is exactly that replay — or a reply so delayed it
                    // should not be believed — and accepting it would re-surface a stale
                    // address or port as fresh discovery. The next probe round (1 s) replaces
                    // it with the truth either way.
                    if (announce == null || !sentProbeNonces.isFresh(announce.nonce)) {
                        if (announce != null) mutableStaleAnnounces.update { it + 1 }
                        continue
                    }

                    val address = packet.address?.hostAddress ?: continue
                    mutablePeer.value = PeerState.Found(
                        address = address,
                        // An unauthenticated announce cannot be trusted to carry a name.
                        hostName = if (authentic) announce.hostName else "",
                        audioPort = if (authentic) announce.audioPort else 0,
                        keyMatches = authentic,
                        // Likewise the versions: an unsigned announce could claim anything, and
                        // a fabricated "matching" version is worse than an unknown one.
                        versionsKnown = authentic && announce.hasVersions,
                        audioProtocolVersion = if (authentic) announce.audioProtocolVersion else 0,
                        audioProtocolMatches = !authentic || announce.audioProtocolMatches,
                        frontEnd = if (authentic) announce.frontEndName else "",
                        buildVersion = if (authentic) announce.buildVersion else "",
                    )
                }

                ControlProtocol.TYPE_STATS -> {
                    if (!authentic) continue
                    val stats = ControlProtocol.readStats(message.payload) ?: continue
                    mutableStats.value = stats
                    // Monotonic, not wall clock: a backward NTP jump must not make a stale
                    // statistics frame look fresh, or the link would never be declared lost.
                    mutableStatsAt.value = SystemClock.elapsedRealtime()
                }
            }
        }
    }

    /**
     * Turns a frame this app could not parse into an explanation, when there is one to give.
     *
     * Only a datagram that carries the control magic but a framing version we do not speak is
     * worth reporting: anything else on this port is unrelated traffic, and claiming a version
     * mismatch for it would be a new way of being wrong. Nothing about such a frame is
     * trustworthy — it is unauthenticated by construction, since verification needs a layout we
     * do not have — so only the fact of the disagreement is published.
     */
    private fun reportIfVersionMismatch(packet: DatagramPacket) {
        val version = ControlProtocol.peekVersion(packet.data, packet.length)
        if (version < 0 || version == (ControlProtocol.VERSION.toInt() and 0xff)) return

        val address = packet.address?.hostAddress ?: return
        mutablePeer.value = PeerState.VersionMismatch(address, version)
    }

    private fun currentScopeActive(): Boolean = scope.isActive

    /**
     * Remembers the last few probe nonces so an announce can prove it answers a probe this
     * phone actually sent. Not a security boundary on its own — the HMAC is that — but the
     * freshness half of replay defence: an authenticated announce that echoes no recent probe
     * is either a recording or a reply too stale to believe.
     */
    internal class ProbeNonceRing(private val capacity: Int) {
        private val nonces = Array(capacity) { ByteArray(ControlProtocol.NONCE_SIZE) }
        private var count = 0
        private var index = 0

        @Synchronized
        fun remember(nonce: ByteArray) {
            require(nonce.size == ControlProtocol.NONCE_SIZE) {
                "Probe nonce must be ${ControlProtocol.NONCE_SIZE} bytes."
            }
            nonce.copyInto(nonces[index])
            index = (index + 1) % capacity
            if (count < capacity) count += 1
        }

        @Synchronized
        fun isFresh(nonce: ByteArray): Boolean {
            if (nonce.size != ControlProtocol.NONCE_SIZE) return false
            for (slot in 0 until count) {
                if (nonces[slot].contentEquals(nonce)) return true
            }
            return false
        }
    }

    /**
     * Directed subnet broadcast, computed from the active link's prefix length.
     * 255.255.255.255 is dropped by many access points, so 10.0.0.255-style targets are used.
     */
    private fun broadcastTargets(context: Context): List<InetAddress> {
        val manager = context.getSystemService(ConnectivityManager::class.java) ?: return emptyList()
        val network = manager.activeNetwork ?: return emptyList()
        val properties = manager.getLinkProperties(network) ?: return emptyList()
        return properties.linkAddresses.mapNotNull(::broadcastFor)
    }

    /**
     * Returns a warning when the target address cannot be on this phone's network, or null when
     * it looks reachable.
     *
     * This exists because of a real failure: the phone sat on 192.168.50.x while the PC was on
     * 10.0.0.x, every packet was dropped upstream, and the app cheerfully reported "Live" the
     * whole time. The UDP socket is unconnected by design, so send() succeeds regardless — this
     * check is the only thing standing between the user and that silence.
     */
    fun subnetWarning(context: Context, host: String): String? {
        val target = runCatching { InetAddress.getByName(host) }.getOrNull() as? Inet4Address
            ?: return null
        if (target.isLoopbackAddress) return null

        val manager = context.getSystemService(ConnectivityManager::class.java) ?: return null
        val network = manager.activeNetwork ?: return null
        val properties = manager.getLinkProperties(network) ?: return null

        val ipv4 = properties.linkAddresses.filter { it.address is Inet4Address }
        if (ipv4.isEmpty()) return null

        val matches = ipv4.any { link ->
            val local = link.address as Inet4Address
            sameSubnet(local, target, link.prefixLength)
        }
        if (matches) return null

        val phoneAddress = (ipv4.first().address as Inet4Address).hostAddress
        return "The PC address $host is not on this phone's network ($phoneAddress). " +
            "Connect both devices to the same Wi-Fi, or audio will be sent nowhere."
    }

    private fun sameSubnet(a: Inet4Address, b: Inet4Address, prefixLength: Int): Boolean {
        if (prefixLength !in 1..32) return false
        fun toInt(address: Inet4Address): Int {
            var value = 0
            for (byte in address.address) value = (value shl 8) or (byte.toInt() and 0xff)
            return value
        }
        val mask = if (prefixLength == 32) -1 else ((1 shl prefixLength) - 1) shl (32 - prefixLength)
        return (toInt(a) and mask) == (toInt(b) and mask)
    }

    private fun broadcastFor(linkAddress: LinkAddress): InetAddress? {
        val address = linkAddress.address as? Inet4Address ?: return null
        val prefix = linkAddress.prefixLength
        if (prefix !in 1..31) return null

        val raw = address.address
        var value = 0
        for (byte in raw) value = (value shl 8) or (byte.toInt() and 0xff)
        val hostMask = (1 shl (32 - prefix)) - 1
        val broadcast = value or hostMask

        return runCatching {
            InetAddress.getByAddress(
                byteArrayOf(
                    ((broadcast ushr 24) and 0xff).toByte(),
                    ((broadcast ushr 16) and 0xff).toByte(),
                    ((broadcast ushr 8) and 0xff).toByte(),
                    (broadcast and 0xff).toByte(),
                ),
            )
        }.getOrNull()
    }
}
