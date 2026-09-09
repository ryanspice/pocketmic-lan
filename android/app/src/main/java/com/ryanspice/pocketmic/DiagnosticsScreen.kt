package com.ryanspice.pocketmic

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableLongStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import android.os.SystemClock
import kotlinx.coroutines.delay
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/**
 * Live diagnostics screen: discovery state, the phone's own streaming snapshot, the most recent
 * STATS payload the receiver pushed back, and a rolling log of state transitions.
 *
 * Deliberately reads only existing state ([StreamingState], [ControlChannel]) plus
 * [DiagnosticsLog], which observes that same state to synthesize its log — nothing here needs
 * [MicStreamingService] or [MainActivity] to be changed to call into it. Wire-up is therefore a
 * single call site; see the class doc on [DiagnosticsLog] for why the log needs no producer of
 * its own.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DiagnosticsScreen(onBack: () -> Unit) {
    DisposableEffect(Unit) {
        DiagnosticsLog.ensureStarted()
        onDispose { }
    }

    val snapshot by StreamingState.state.collectAsState()
    val peer by ControlChannel.peer.collectAsState()
    val stats by ControlChannel.stats.collectAsState()
    val lastStatsAt by ControlChannel.lastStatsAtMillis.collectAsState()
    val bindError by ControlChannel.bindError.collectAsState()
    val log by DiagnosticsLog.entries.collectAsState()

    var nowMillis by remember { mutableLongStateOf(SystemClock.elapsedRealtime()) }
    LaunchedEffect(Unit) {
        while (true) {
            nowMillis = SystemClock.elapsedRealtime()
            delay(500)
        }
    }

    Scaffold(
        containerColor = MaterialTheme.colorScheme.background,
        topBar = {
            TopAppBar(
                title = { Text("Diagnostics") },
                navigationIcon = { TextButton(onClick = onBack) { Text("Back") } },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.background,
                ),
            )
        },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(horizontal = 18.dp, vertical = 10.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            DiscoveryCard(peer, bindError, snapshot.status)
            StreamingCard(snapshot)
            StatsCard(stats, lastStatsAt, nowMillis)

            Text(
                "Event log",
                fontWeight = FontWeight.SemiBold,
                color = MaterialTheme.colorScheme.onBackground,
            )

            LazyColumn(
                // weight, not fillMaxSize: this sits below several fixed-height cards in a
                // plain (non-scrolling) Column, so it must claim only the leftover space,
                // not the full parent height on top of what they already used.
                modifier = Modifier.fillMaxWidth().weight(1f),
                verticalArrangement = Arrangement.spacedBy(4.dp),
            ) {
                if (log.isEmpty()) {
                    item {
                        Text(
                            "No events yet.",
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            style = MaterialTheme.typography.bodySmall,
                        )
                    }
                }
                items(log.asReversed()) { entry ->
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        Text(
                            TimeFormatter.format(Date(entry.atMillis)),
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            style = MaterialTheme.typography.bodySmall,
                        )
                        Text(
                            entry.message,
                            color = MaterialTheme.colorScheme.onBackground,
                            style = MaterialTheme.typography.bodySmall,
                        )
                    }
                }
            }
        }
    }
}

private object TimeFormatter {
    private val format = SimpleDateFormat("HH:mm:ss", Locale.US)
    fun format(date: Date): String = format.format(date)
}

@Composable
private fun DiscoveryCard(peer: PeerState, bindError: String?, status: StreamStatus) {
    Card(
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        shape = RoundedCornerShape(16.dp),
    ) {
        Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            Text("Discovery", fontWeight = FontWeight.SemiBold)

            // Probing stops once the link is healthy, so whatever it last concluded is stale from
            // that moment on — and after a reconnect it is stale at its most alarming, reading
            // "No receiver found" beside a stream that is demonstrably live. Say plainly that
            // discovery is idle instead of reporting its last answer as though it were current.
            if (status == StreamStatus.STREAMING) {
                Text(
                    "Idle while streaming — delivery is confirmed by the statistics feed below, " +
                        "which is a stronger signal than a discovery reply.",
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    style = MaterialTheme.typography.bodySmall,
                )
                bindError?.let {
                    Text(it, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall)
                }
                return@Column
            }

            val (text, color) = when (peer) {
                is PeerState.Searching -> "Searching..." to MaterialTheme.colorScheme.onSurfaceVariant
                is PeerState.NotFound -> "No receiver found" to MaterialTheme.colorScheme.error
                is PeerState.VersionMismatch ->
                    "Receiver at ${peer.address} speaks control protocol v${peer.receiverControlVersion}, " +
                        "this app speaks v${ControlProtocol.VERSION}" to MaterialTheme.colorScheme.error

                is PeerState.Found -> when {
                    !peer.keyMatches ->
                        "Receiver at ${peer.address} - pairing key mismatch" to MaterialTheme.colorScheme.error

                    !peer.audioProtocolMatches ->
                        "Receiver at ${peer.address} sends audio protocol v${peer.audioProtocolVersion}, " +
                            "this app speaks v${PacketCrypto.VERSION}" to MaterialTheme.colorScheme.error

                    else ->
                        "${peer.hostName.ifBlank { "Receiver" }} at ${peer.address}:${peer.audioPort}" to
                            MaterialTheme.colorScheme.primary
                }
            }
            Text(text, color = color)
            if (peer is PeerState.Found && peer.versionsKnown) {
                // The one place the three shipping parts are visible side by side. When the phone
                // and the PC disagree, this is the line that says which two builds disagreed.
                Text(
                    "Receiver: ${peer.frontEnd} ${peer.buildVersion} · " +
                        "audio protocol v${peer.audioProtocolVersion} · " +
                        "control protocol v${ControlProtocol.VERSION}",
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    style = MaterialTheme.typography.bodySmall,
                )
            }
            bindError?.let {
                Text(it, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall)
            }
        }
    }
}

@Composable
private fun StreamingCard(snapshot: StreamingSnapshot) {
    Card(
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        shape = RoundedCornerShape(16.dp),
    ) {
        Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            Text("This phone", fontWeight = FontWeight.SemiBold)
            LabelledRow("Status", snapshot.status.name)
            LabelledRow("Destination", snapshot.destination.ifBlank { "-" })
            LabelledRow("Packets sent", snapshot.packetsSent.toString())
            LabelledRow("Packets dropped (queue)", snapshot.packetsDropped.toString())
            LabelledRow("Read stalls", snapshot.readStalls.toString())
            LabelledRow("Capture overruns", snapshot.overruns.toString())
            LabelledRow("Send stalls", snapshot.sendStallCount.toString())
            LabelledRow("Max send", "${snapshot.maxSendMillis} ms")
            LabelledRow("Wi-Fi lock", snapshot.wifiLockMode.name)
            if (snapshot.wifiBand.isNotBlank()) {
                LabelledRow("Wi-Fi", "${snapshot.wifiBand} · ${snapshot.wifiRssi} dBm")
            }
            LabelledRow("Capture source", snapshot.captureSource.ifBlank { "-" })
            snapshot.error?.let {
                Text(it, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall)
            }
        }
    }
}

@Composable
private fun StatsCard(stats: ReceiverStats?, lastStatsAt: Long, nowMillis: Long) {
    Card(
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
        shape = RoundedCornerShape(16.dp),
    ) {
        Column(modifier = Modifier.padding(14.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text("Last STATS from PC", fontWeight = FontWeight.SemiBold)
                val alive = lastStatsAt > 0 && nowMillis - lastStatsAt < ControlChannel.STATS_TIMEOUT_MS
                Box(
                    Modifier
                        .size(9.dp)
                        .background(
                            if (alive) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.onSurfaceVariant,
                            CircleShape,
                        ),
                )
            }

            if (stats == null) {
                Text("No reply yet.", color = MaterialTheme.colorScheme.onSurfaceVariant)
            } else {
                LabelledRow("Packets in", stats.packets.toString())
                LabelledRow("Lost", stats.lost.toString())
                LabelledRow("Late", stats.late.toString())
                LabelledRow("Rejected", stats.rejected.toString())
                LabelledRow("Trimmed", stats.trimmed.toString())
                LabelledRow("Buffer", "${stats.bufferMillis} ms")
                LabelledRow("Playing", stats.playing.toString())
                val ageSeconds = if (lastStatsAt > 0) (nowMillis - lastStatsAt) / 1000.0 else null
                ageSeconds?.let {
                    Text(
                        "Last reply %.1fs ago".format(it),
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        style = MaterialTheme.typography.bodySmall,
                    )
                }
            }
        }
    }
}

@Composable
private fun LabelledRow(label: String, value: String) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(label, color = MaterialTheme.colorScheme.onSurfaceVariant, style = MaterialTheme.typography.bodySmall)
        Text(value, color = MaterialTheme.colorScheme.onBackground, style = MaterialTheme.typography.bodySmall)
    }
}
