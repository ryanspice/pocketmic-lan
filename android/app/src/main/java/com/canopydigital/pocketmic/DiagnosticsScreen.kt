package com.canopydigital.pocketmic

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
import androidx.compose.ui.res.stringResource
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
                title = { Text(stringResource(R.string.action_diagnostics)) },
                navigationIcon = { TextButton(onClick = onBack) { Text(stringResource(R.string.action_back)) } },
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
                stringResource(R.string.diagnostics_event_log),
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
                            stringResource(R.string.diagnostics_empty_events),
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
            Text(stringResource(R.string.diagnostics_discovery), fontWeight = FontWeight.SemiBold)

            // Probing stops once the link is healthy, so whatever it last concluded is stale from
            // that moment on — and after a reconnect it is stale at its most alarming, reading
            // "No receiver found" beside a stream that is demonstrably live. Say plainly that
            // discovery is idle instead of reporting its last answer as though it were current.
            if (status == StreamStatus.STREAMING) {
                Text(
                    stringResource(R.string.diagnostics_discovery_idle_streaming),
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    style = MaterialTheme.typography.bodySmall,
                )
                bindError?.let {
                    Text(it, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodySmall)
                }
                return@Column
            }

            val (text, color) = when (peer) {
                is PeerState.Searching -> stringResource(R.string.diagnostics_searching_long) to MaterialTheme.colorScheme.onSurfaceVariant
                is PeerState.NotFound -> stringResource(R.string.diagnostics_no_receiver) to MaterialTheme.colorScheme.error
                is PeerState.VersionMismatch -> stringResource(
                    R.string.connection_receiver_protocol_mismatch,
                    peer.address,
                    peer.receiverControlVersion,
                    ControlProtocol.VERSION,
                ) to MaterialTheme.colorScheme.error

                is PeerState.Found -> when {
                    !peer.keyMatches -> stringResource(
                        R.string.diagnostics_pairing_mismatch,
                        peer.address,
                    ) to MaterialTheme.colorScheme.error

                    !peer.audioProtocolMatches -> stringResource(
                        R.string.diagnostics_audio_protocol_mismatch,
                        peer.address,
                        peer.audioProtocolVersion,
                        PacketCrypto.VERSION,
                    ) to MaterialTheme.colorScheme.error

                    else -> stringResource(
                        R.string.diagnostics_receiver_identity,
                        peer.hostName.ifBlank { stringResource(R.string.diagnostics_receiver_fallback) },
                        peer.address,
                        peer.audioPort,
                    ) to MaterialTheme.colorScheme.primary
                }
            }
            Text(text, color = color)
            if (peer is PeerState.Found && peer.versionsKnown) {
                // The one place the three shipping parts are visible side by side. When the phone
                // and the PC disagree, this is the line that says which two builds disagreed.
                Text(
                    stringResource(
                        R.string.diagnostics_receiver_details,
                        peer.frontEnd,
                        peer.buildVersion,
                        peer.audioProtocolVersion,
                        ControlProtocol.VERSION,
                    ),
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
            Text(stringResource(R.string.diagnostics_this_phone), fontWeight = FontWeight.SemiBold)
            LabelledRow(stringResource(R.string.diagnostics_status), snapshot.status.name)
            LabelledRow(stringResource(R.string.diagnostics_destination), snapshot.destination.ifBlank { "-" })
            LabelledRow(stringResource(R.string.diagnostics_packets_sent), snapshot.packetsSent.toString())
            LabelledRow(stringResource(R.string.diagnostics_queue_drops), snapshot.packetsDropped.toString())
            LabelledRow(stringResource(R.string.diagnostics_read_stalls), snapshot.readStalls.toString())
            LabelledRow(stringResource(R.string.diagnostics_capture_overruns), snapshot.overruns.toString())
            LabelledRow(stringResource(R.string.diagnostics_send_stalls), snapshot.sendStallCount.toString())
            LabelledRow(stringResource(R.string.diagnostics_max_send), stringResource(R.string.unit_milliseconds, snapshot.maxSendMillis))
            LabelledRow(stringResource(R.string.diagnostics_wifi_lock), snapshot.wifiLockMode.name)
            if (snapshot.wifiBand.isNotBlank()) {
                LabelledRow(
                    stringResource(R.string.diagnostics_wifi),
                    stringResource(R.string.unit_dbm, snapshot.wifiBand, snapshot.wifiRssi),
                )
            }
            LabelledRow(stringResource(R.string.diagnostics_capture_source), snapshot.captureSource.ifBlank { "-" })
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
                Text(stringResource(R.string.diagnostics_last_stats), fontWeight = FontWeight.SemiBold)
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
                Text(stringResource(R.string.diagnostics_no_reply), color = MaterialTheme.colorScheme.onSurfaceVariant)
            } else {
                LabelledRow(stringResource(R.string.diagnostics_packets_in), stats.packets.toString())
                LabelledRow(stringResource(R.string.diagnostics_lost), stats.lost.toString())
                LabelledRow(stringResource(R.string.diagnostics_late), stats.late.toString())
                LabelledRow(stringResource(R.string.diagnostics_rejected), stats.rejected.toString())
                LabelledRow(stringResource(R.string.diagnostics_trimmed), stats.trimmed.toString())
                LabelledRow(stringResource(R.string.diagnostics_buffer), stringResource(R.string.unit_milliseconds, stats.bufferMillis))
                LabelledRow(stringResource(R.string.diagnostics_playing), stats.playing.toString())
                val ageSeconds = if (lastStatsAt > 0) (nowMillis - lastStatsAt) / 1000.0 else null
                ageSeconds?.let {
                    Text(
                        stringResource(R.string.diagnostics_last_reply, it),
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
