package com.ryanspice.pocketmic

import android.Manifest
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.os.SystemClock
import androidx.activity.ComponentActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExtendedFloatingActionButton
import androidx.compose.material3.FilterChip
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.ModalBottomSheet
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Slider
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.rememberModalBottomSheetState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import kotlinx.coroutines.delay
import kotlin.math.roundToInt

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            PocketMicTheme {
                PocketMicScreen()
            }
        }
    }
}

// Warm paper + gold, matching the web design system (web/styles.css): dark slate-grey becomes
// a warm near-black, and the accent is the same gold rgba(184,138,59) the web surface uses.
private val Background = Color(0xFF171411)
private val Surface = Color(0xFF1F1B16)
private val SurfaceRaised = Color(0xFF27211C)
private val Accent = Color(0xFFD4A95C)
private val Danger = Color(0xFFFF756B)
private val Caution = Color(0xFFFFC46B)
private val TextPrimary = Color(0xFFE9E1D4)
private val TextMuted = Color(0xFFA79E8D)

/** Debounce for pushing DSP slider changes to the receiver, so dragging does not flood UDP. */
private const val DSP_PUSH_DEBOUNCE_MS = 120L

/** How often the "is the PC confirming delivery" indicator is re-derived. */
private const val LINK_ALIVE_POLL_MS = 500L

/** Input cap for the pairing key field. */
private const val PAIRING_KEY_MAX_CHARS = 64

/** Input cap for the port field: 65,535 is five digits. */
private const val PORT_INPUT_MAX_DIGITS = 5

@Composable
private fun PocketMicTheme(content: @Composable () -> Unit) {
    MaterialTheme(
        colorScheme = darkColorScheme(
            primary = Accent,
            onPrimary = Color(0xFF221A08),
            background = Background,
            onBackground = TextPrimary,
            surface = Surface,
            onSurface = TextPrimary,
            error = Danger,
        ),
        content = content,
    )
}

@Composable
private fun PocketMicScreen() {
    val context = LocalContext.current
    val initial = remember { AppPrefs.load(context) }
    val initialUi = remember { AppPrefs.loadUi(context) }
    val snapshot by StreamingState.state.collectAsState()
    val peer by ControlChannel.peer.collectAsState()
    val stats by ControlChannel.stats.collectAsState()
    val lastStatsAt by ControlChannel.lastStatsAtMillis.collectAsState()

    var host by rememberSaveable { mutableStateOf(initial.host) }
    var portText by rememberSaveable { mutableStateOf(initial.port.toString()) }
    var pairingKey by rememberSaveable { mutableStateOf(initial.pairingKey) }
    var captureModeWire by rememberSaveable { mutableStateOf(initial.captureMode.wireValue) }
    var gain by rememberSaveable { mutableStateOf(initial.gain) }
    var autoConnect by rememberSaveable { mutableStateOf(initialUi.autoConnect) }
    var fabCorner by rememberSaveable { mutableStateOf(initialUi.fabCorner) }
    var validationError by rememberSaveable { mutableStateOf<String?>(null) }
    var showInfo by rememberSaveable { mutableStateOf(false) }
    var showDiagnostics by rememberSaveable { mutableStateOf(false) }
    var showQrScanner by rememberSaveable { mutableStateOf(false) }
    var dsp by remember { mutableStateOf(AppPrefs.loadDsp(context)) }

    val captureMode = CaptureMode.fromWireValue(captureModeWire)
    val isActive = snapshot.status == StreamStatus.CONNECTING ||
        snapshot.status == StreamStatus.STREAMING ||
        snapshot.status == StreamStatus.RECONNECTING
    val port = portText.toIntOrNull()
        ?.takeIf { it in MIN_PORT..MAX_PORT }
        ?: DEFAULT_PORT

    // Probe while idle, and again while reconnecting. During healthy capture the receiver's own
    // statistics feed answers everything a probe would, so broadcasting then is pure radio cost
    // on the hot path — but once that feed stops, probing is the only way back, which is exactly
    // why suppressing it unconditionally left the app unable to heal itself.
    //
    // MicStreamingService drives the same switch, because reconnect has to work with this
    // activity destroyed. Both derive it from one status, so they cannot disagree.
    val shouldProbe = !isActive || snapshot.status == StreamStatus.RECONNECTING
    DisposableEffect(pairingKey, port, shouldProbe) {
        ControlChannel.start(context, pairingKey, port, probe = shouldProbe)
        ControlChannel.setProbing(context, port, enabled = shouldProbe)
        onDispose { }
    }

    // Auto-fill from discovery. Only the address is adopted; starting capture stays manual.
    // A receiver whose protocol this app cannot speak is not adopted: pointing at it silently
    // would produce a stream it rejects every packet of.
    LaunchedEffect(peer, autoConnect) {
        val found = peer as? PeerState.Found ?: return@LaunchedEffect
        if (autoConnect && found.keyMatches && found.audioProtocolMatches &&
            !isActive && host != found.address
        ) {
            host = found.address
            AppPrefs.save(context, MicConfig(found.address, port, pairingKey, captureMode, gain))
        }
    }

    // Push slider changes to the receiver, debounced so dragging does not flood the channel.
    LaunchedEffect(dsp, captureMode, peer) {
        if (captureMode != CaptureMode.CUSTOM) return@LaunchedEffect
        delay(DSP_PUSH_DEBOUNCE_MS)
        AppPrefs.saveDsp(context, dsp)
        ControlChannel.sendConfig(dsp, port)
    }

    var linkAlive by remember { mutableStateOf(false) }
    LaunchedEffect(isActive) {
        while (isActive) {
            // Monotonic clock: liveness must not be fooled by a wall-clock jump backwards.
            linkAlive = lastStatsAt > 0 &&
                SystemClock.elapsedRealtime() - lastStatsAt < ControlChannel.STATS_TIMEOUT_MS
            delay(LINK_ALIVE_POLL_MS)
        }
        linkAlive = false
    }

    val permissionLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.RequestMultiplePermissions(),
    ) { grants ->
        val granted = grants[Manifest.permission.RECORD_AUDIO] == true ||
            ContextCompat.checkSelfPermission(context, Manifest.permission.RECORD_AUDIO) ==
            PackageManager.PERMISSION_GRANTED
        if (granted) {
            startService(context, AppPrefs.load(context), discoveredName(ControlChannel.peer.value))
        } else {
            validationError = "Microphone permission is required."
        }
    }

    fun attemptStart() {
        validationError = null
        val parsedPort = portText.toIntOrNull()
        val config = when {
            host.isBlank() -> null.also {
                validationError = if (autoConnect) {
                    "No receiver found yet. Turn off auto-connect to enter the PC address manually."
                } else {
                    "Enter the Windows PC IP address."
                }
            }

            parsedPort == null || parsedPort !in MIN_PORT..MAX_PORT ->
                null.also { validationError = "Enter a valid UDP port." }

            pairingKey.length < MIN_PAIRING_KEY_LENGTH ->
                null.also {
                    validationError = "Pairing key must be at least $MIN_PAIRING_KEY_LENGTH characters."
                }

            else -> MicConfig(host.trim(), parsedPort, pairingKey, captureMode, gain)
        } ?: return

        // Catch the unreachable-network case before opening the microphone, rather than
        // streaming into a black hole and reporting success.
        ControlChannel.subnetWarning(context, config.host)?.let {
            validationError = it
            return
        }

        // And catch a receiver that has already told us it speaks a different audio protocol.
        // Every packet would be rejected, and a rejection count is how a version problem gets
        // misread as a pairing-key problem.
        val discovered = peer as? PeerState.Found
        if (discovered != null && discovered.address == config.host && !discovered.audioProtocolMatches) {
            validationError =
                "That receiver sends audio protocol v${discovered.audioProtocolVersion} and this app " +
                "speaks v${PacketCrypto.VERSION}. Update the PC receiver and the phone app to the " +
                "same release."
            return
        }

        AppPrefs.save(context, config)
        val missing = buildList {
            if (ContextCompat.checkSelfPermission(context, Manifest.permission.RECORD_AUDIO) !=
                PackageManager.PERMISSION_GRANTED
            ) {
                add(Manifest.permission.RECORD_AUDIO)
            }
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU &&
                ContextCompat.checkSelfPermission(context, Manifest.permission.POST_NOTIFICATIONS) !=
                PackageManager.PERMISSION_GRANTED
            ) {
                add(Manifest.permission.POST_NOTIFICATIONS)
            }
        }
        val name = discoveredName(peer)
        if (missing.isEmpty()) {
            startService(context, config, name)
        } else {
            permissionLauncher.launch(missing.toTypedArray())
        }
    }

    Scaffold(containerColor = Background) { padding ->
        BoxWithConstraints(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .imePadding(),
        ) {
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    // Content is sized to fit this device at rest; the scroll container exists
                    // only so fields stay reachable when the keyboard is up or on short screens.
                    .verticalScroll(rememberScrollState())
                    .padding(horizontal = 18.dp, vertical = 14.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Text(
                        text = "PocketMic",
                        style = MaterialTheme.typography.headlineMedium,
                        fontWeight = FontWeight.Bold,
                    )
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        TextButton(onClick = { showDiagnostics = true }) { Text("Diagnostics") }
                        TextButton(onClick = { showInfo = true }) { Text("Info") }
                    }
                }

                StatusCard(snapshot, stats, linkAlive)

                ConnectionCard(
                    autoConnect = autoConnect,
                    onAutoConnectChange = {
                        autoConnect = it
                        AppPrefs.saveAutoConnect(context, it)
                    },
                    peer = peer,
                    snapshot = snapshot,
                    linkAlive = linkAlive,
                    host = host,
                    onHostChange = { host = it },
                    portText = portText,
                    onPortChange = { portText = it.filter(Char::isDigit).take(PORT_INPUT_MAX_DIGITS) },
                    pairingKey = pairingKey,
                    onKeyChange = { pairingKey = it.take(PAIRING_KEY_MAX_CHARS) },
                    enabled = !isActive,
                    showQrScanner = showQrScanner,
                    onShowQrScannerChange = { showQrScanner = it },
                    onQrScanned = { data ->
                        host = data.host
                        portText = data.port.toString()
                        pairingKey = data.pairingKey
                        showQrScanner = false
                        AppPrefs.save(context, MicConfig(data.host, data.port, data.pairingKey, captureMode, gain))
                    },
                )

                CaptureCard(
                    captureMode = captureMode,
                    onModeChange = { captureModeWire = it.wireValue },
                    gain = gain,
                    onGainChange = { gain = it },
                    enabled = !isActive,
                    snapshot = snapshot,
                    dsp = dsp,
                    onDspChange = { dsp = it },
                )

                validationError?.let { Text(it, color = Danger, style = MaterialTheme.typography.bodyMedium) }
                snapshot.error?.let { Text(it, color = Danger, style = MaterialTheme.typography.bodyMedium) }
                ControlChannel.bindError.collectAsState().value?.let {
                    Text(it, color = Caution, style = MaterialTheme.typography.bodySmall)
                }

                Spacer(Modifier.height(72.dp))
            }

            DraggableActionButton(
                corner = fabCorner,
                onCornerChange = {
                    fabCorner = it
                    AppPrefs.saveFabCorner(context, it)
                },
                isActive = isActive,
                onClick = { if (isActive) stopService(context) else attemptStart() },
                containerWidth = maxWidth.value,
                containerHeight = maxHeight.value,
            )
        }
    }

    if (showInfo) {
        InfoSheet(onDismiss = { showInfo = false })
    }

    // Drawn over the main screen rather than routed to, because the app has no navigation graph
    // and adding one for a single diagnostics page would cost more than it explains.
    if (showDiagnostics) {
        DiagnosticsScreen(onBack = { showDiagnostics = false })
    }
}

/**
 * Snaps to a corner rather than resting anywhere it is released, so it can never end up
 * covering a text field or drifting off-screen after a rotation.
 */
@Composable
private fun androidx.compose.foundation.layout.BoxScope.DraggableActionButton(
    corner: FabCorner,
    onCornerChange: (FabCorner) -> Unit,
    isActive: Boolean,
    onClick: () -> Unit,
    containerWidth: Float,
    containerHeight: Float,
) {
    var dragX by remember(corner) { mutableStateOf(0f) }
    var dragY by remember(corner) { mutableStateOf(0f) }

    val alignment = when (corner) {
        FabCorner.BOTTOM_END -> Alignment.BottomEnd
        FabCorner.BOTTOM_START -> Alignment.BottomStart
        FabCorner.TOP_END -> Alignment.TopEnd
        FabCorner.TOP_START -> Alignment.TopStart
    }

    ExtendedFloatingActionButton(
        onClick = onClick,
        containerColor = if (isActive) Danger else Accent,
        contentColor = if (isActive) Color(0xFF2A0502) else Color(0xFF05231B),
        modifier = Modifier
            .align(alignment)
            .padding(16.dp)
            .pointerInput(corner, containerWidth, containerHeight) {
                detectDragGestures(
                    onDragEnd = {
                        val startSide = when (corner) {
                            FabCorner.BOTTOM_START, FabCorner.TOP_START -> true
                            else -> false
                        }
                        val topSide = when (corner) {
                            FabCorner.TOP_START, FabCorner.TOP_END -> true
                            else -> false
                        }
                        // Release position decides the nearest corner.
                        val movedRight = dragX > containerWidth / 4f
                        val movedLeft = dragX < -containerWidth / 4f
                        val movedDown = dragY > containerHeight / 4f
                        val movedUp = dragY < -containerHeight / 4f

                        val nowStart = when {
                            movedLeft -> true
                            movedRight -> false
                            else -> startSide
                        }
                        val nowTop = when {
                            movedUp -> true
                            movedDown -> false
                            else -> topSide
                        }
                        onCornerChange(
                            when {
                                nowTop && nowStart -> FabCorner.TOP_START
                                nowTop -> FabCorner.TOP_END
                                nowStart -> FabCorner.BOTTOM_START
                                else -> FabCorner.BOTTOM_END
                            },
                        )
                        dragX = 0f
                        dragY = 0f
                    },
                ) { change, amount ->
                    change.consume()
                    dragX += amount.x
                    dragY += amount.y
                }
            },
    ) {
        Text(
            text = if (isActive) "Stop" else "Start",
            fontWeight = FontWeight.Bold,
        )
    }
}

@Composable
private fun StatusCard(
    snapshot: StreamingSnapshot,
    stats: ReceiverStats?,
    linkAlive: Boolean,
) {
    val label = when (snapshot.status) {
        StreamStatus.IDLE -> "Ready"
        StreamStatus.CONNECTING -> "Connecting"
        StreamStatus.STREAMING -> if (linkAlive) "Live — PC confirmed" else "Sending — no reply from PC"
        StreamStatus.RECONNECTING -> "Reconnecting — still recording"
        StreamStatus.ERROR -> "Stopped with error"
    }
    val statusColor = when {
        snapshot.status == StreamStatus.ERROR -> Danger
        snapshot.status == StreamStatus.STREAMING && linkAlive -> Accent
        snapshot.status == StreamStatus.STREAMING -> Caution
        snapshot.status == StreamStatus.RECONNECTING -> Caution
        else -> TextMuted
    }

    Card(
        colors = CardDefaults.cardColors(containerColor = SurfaceRaised),
        shape = RoundedCornerShape(16.dp),
    ) {
        Column(
            modifier = Modifier.padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Box(
                        Modifier
                            .size(9.dp)
                            .background(statusColor, CircleShape),
                    )
                    Spacer(Modifier.width(8.dp))
                    Text(label, color = statusColor, fontWeight = FontWeight.Bold)
                }
                if (snapshot.packetsSent > 0) {
                    Text("${snapshot.packetsSent}", color = TextMuted, style = MaterialTheme.typography.bodySmall)
                }
            }

            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(10.dp)
                    .background(Background, RoundedCornerShape(6.dp)),
            ) {
                Box(
                    modifier = Modifier
                        .fillMaxWidth(snapshot.level.coerceIn(0f, 1f))
                        .height(10.dp)
                        .background(Accent, RoundedCornerShape(6.dp)),
                )
            }

            stats?.let {
                Text(
                    "PC: ${it.packets} in · lost ${it.lost} · buffer ${it.bufferMillis} ms",
                    color = TextMuted,
                    style = MaterialTheme.typography.bodySmall,
                )
                if (it.rejected > 0) {
                    Text(
                        "PC rejected ${it.rejected} packets — pairing key mismatch.",
                        color = Danger,
                        style = MaterialTheme.typography.bodySmall,
                    )
                }
            }

            if (snapshot.wifiLockMode == WifiLockMode.UNAVAILABLE) {
                Text(
                    "Wi-Fi low-latency lock unavailable; expect higher jitter.",
                    color = Caution,
                    style = MaterialTheme.typography.bodySmall,
                )
            }
        }
    }
}

@Composable
private fun ConnectionCard(
    autoConnect: Boolean,
    onAutoConnectChange: (Boolean) -> Unit,
    peer: PeerState,
    snapshot: StreamingSnapshot,
    linkAlive: Boolean,
    host: String,
    onHostChange: (String) -> Unit,
    portText: String,
    onPortChange: (String) -> Unit,
    pairingKey: String,
    onKeyChange: (String) -> Unit,
    enabled: Boolean,
    showQrScanner: Boolean,
    onShowQrScannerChange: (Boolean) -> Unit,
    onQrScanned: (QrPairingData) -> Unit,
) {
    val context = LocalContext.current
    val streaming = !enabled

    Card(
        colors = CardDefaults.cardColors(containerColor = Surface),
        shape = RoundedCornerShape(16.dp),
    ) {
        Column(
            modifier = Modifier.padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text("Auto-connect", fontWeight = FontWeight.SemiBold)
                Switch(checked = autoConnect, onCheckedChange = onAutoConnectChange, enabled = enabled)
            }

            if (streaming) {
                // While the stream is up, discovery is not the source of truth — the receiver's
                // own statistics feed is. Reporting the last idle discovery result here is how a
                // phone that was streaming happily came to display "Searching for a PocketMic
                // receiver…" indefinitely: probing had been suppressed, so the label was frozen
                // at whatever it read the moment capture began.
                val (message, colour) = when {
                    snapshot.status == StreamStatus.RECONNECTING ->
                        "Lost contact with ${snapshot.destination}. Searching for it again — " +
                            "recording continues and nothing needs restarting." to Caution

                    linkAlive -> "Connected to ${snapshot.destination}." to Accent

                    else -> "Sending to ${snapshot.destination}, waiting for the first reply." to Caution
                }
                Text(message, color = colour, style = MaterialTheme.typography.bodyMedium)
                if (snapshot.reconnects > 0) {
                    Text(
                        "Recovered from ${snapshot.reconnects} dropout" +
                            if (snapshot.reconnects == 1) "." else "s.",
                        color = TextMuted,
                        style = MaterialTheme.typography.bodySmall,
                    )
                }
            } else if (autoConnect) {
                val (message, colour) = when (val current = peer) {
                    is PeerState.Searching -> "Searching for a PocketMic receiver…" to TextMuted
                    is PeerState.NotFound ->
                        "No receiver found. Start PocketMicReceiver on your PC, or turn this off to enter the address." to Caution

                    // Reported as a version disagreement rather than as an absent receiver,
                    // which is what it would otherwise look like: a frame this app cannot parse
                    // is dropped, and a dropped frame is indistinguishable from no reply at all.
                    is PeerState.VersionMismatch ->
                        "A receiver at ${current.address} speaks control protocol " +
                            "v${current.receiverControlVersion} and this app speaks " +
                            "v${ControlProtocol.VERSION}. Update both to the same release." to Danger

                    is PeerState.Found -> when {
                        !current.keyMatches ->
                            "Receiver at ${current.address} — pairing key does not match." to Danger

                        !current.audioProtocolMatches ->
                            "${current.hostName.ifBlank { "Receiver" }} at ${current.address} sends " +
                                "audio protocol v${current.audioProtocolVersion} and this app speaks " +
                                "v${PacketCrypto.VERSION}. Update both to the same release." to Danger

                        else -> {
                            val build = if (current.versionsKnown && current.buildVersion.isNotBlank()) {
                                " · ${current.frontEnd} ${current.buildVersion}"
                            } else {
                                ""
                            }
                            "${current.hostName.ifBlank { "Receiver" }} at ${current.address}$build" to Accent
                        }
                    }
                }
                Text(message, color = colour, style = MaterialTheme.typography.bodyMedium)
            } else {
                OutlinedTextField(
                    value = host,
                    onValueChange = onHostChange,
                    label = { Text("PC IP address") },
                    placeholder = { Text("192.168.1.25") },
                    singleLine = true,
                    enabled = enabled,
                    modifier = Modifier.fillMaxWidth(),
                )
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    OutlinedTextField(
                        value = portText,
                        onValueChange = onPortChange,
                        label = { Text("UDP port") },
                        singleLine = true,
                        enabled = enabled,
                        modifier = Modifier.width(140.dp),
                    )
                }
                OutlinedTextField(
                    value = pairingKey,
                    onValueChange = onKeyChange,
                    label = { Text("Pairing key") },
                    singleLine = true,
                    enabled = enabled,
                    visualTransformation = PasswordVisualTransformation(),
                    modifier = Modifier.fillMaxWidth(),
                )
                Row(horizontalArrangement = Arrangement.spacedBy(10.dp)) {
                    OutlinedButton(onClick = {
                        context.getSystemService(ClipboardManager::class.java)
                            ?.setPrimaryClip(ClipData.newPlainText("PocketMic pairing key", pairingKey))
                    }) { Text("Copy key") }
                    OutlinedButton(
                        onClick = { onKeyChange(AppPrefs.generatePairingKey()) },
                        enabled = enabled,
                    ) { Text("Generate") }
                    OutlinedButton(
                        onClick = { onShowQrScannerChange(!showQrScanner) },
                        enabled = enabled,
                    ) { Text(if (showQrScanner) "Hide camera" else "Scan QR") }
                }
                if (showQrScanner) {
                    QrScannerView(onScanned = onQrScanned)
                }
            }
        }
    }
}

@Composable
private fun DspSlider(
    label: String,
    value: String,
    current: Float,
    range: ClosedFloatingPointRange<Float>,
    onChange: (Float) -> Unit,
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(label, style = MaterialTheme.typography.bodySmall, color = TextMuted)
        Text(value, style = MaterialTheme.typography.bodySmall, color = Accent)
    }
    Slider(value = current, onValueChange = onChange, valueRange = range)
}

@Composable
private fun CaptureCard(
    captureMode: CaptureMode,
    onModeChange: (CaptureMode) -> Unit,
    gain: Float,
    onGainChange: (Float) -> Unit,
    enabled: Boolean,
    snapshot: StreamingSnapshot,
    dsp: DspSettings,
    onDspChange: (DspSettings) -> Unit,
) {
    Card(
        colors = CardDefaults.cardColors(containerColor = Surface),
        shape = RoundedCornerShape(16.dp),
    ) {
        Column(
            modifier = Modifier.padding(14.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                FilterChip(
                    selected = captureMode == CaptureMode.CLEAN,
                    onClick = { onModeChange(CaptureMode.CLEAN) },
                    label = { Text("Clean") },
                    enabled = enabled,
                )
                FilterChip(
                    selected = captureMode == CaptureMode.VOICE,
                    onClick = { onModeChange(CaptureMode.VOICE) },
                    label = { Text("Voice") },
                    enabled = enabled,
                )
                FilterChip(
                    selected = captureMode == CaptureMode.CUSTOM,
                    onClick = { onModeChange(CaptureMode.CUSTOM) },
                    label = { Text("Custom") },
                    enabled = enabled,
                )
            }
            Text("Input gain ${"%.1f".format(gain)}×", style = MaterialTheme.typography.bodyMedium)
            Slider(
                value = gain,
                onValueChange = { onGainChange((it * 10).roundToInt() / 10f) },
                valueRange = 0.5f..3.0f,
                enabled = enabled,
            )

            // Custom exposes the receiver's chain directly. These take effect live — the PC is
            // doing the processing, so there is no need to stop the stream to retune.
            if (captureMode == CaptureMode.CUSTOM) {
                DspSlider("High-pass", "${dsp.highPassHz} Hz", dsp.highPassHz.toFloat(), 20f..300f) {
                    onDspChange(dsp.copy(highPassHz = it.roundToInt()))
                }
                DspSlider("Noise gate", "${dsp.gate}%", dsp.gate.toFloat(), 0f..100f) {
                    onDspChange(dsp.copy(gate = it.roundToInt()))
                }
                DspSlider("Compressor", "${dsp.compressor}%", dsp.compressor.toFloat(), 0f..100f) {
                    onDspChange(dsp.copy(compressor = it.roundToInt()))
                }
                DspSlider("Presence", "${"%.1f".format(dsp.presenceDb)} dB", dsp.presenceDb, 0f..12f) {
                    onDspChange(dsp.copy(presenceDb = (it * 10).roundToInt() / 10f))
                }
                DspSlider("Make-up", "${dsp.makeup}%", dsp.makeup.toFloat(), 0f..100f) {
                    onDspChange(dsp.copy(makeup = it.roundToInt()))
                }
                Text(
                    "Processing runs on the PC, so changes apply instantly while streaming.",
                    color = TextMuted,
                    style = MaterialTheme.typography.bodySmall,
                )
            }

            if (snapshot.captureSource.isNotBlank()) {
                Text(
                    "Source in use: ${snapshot.captureSource}",
                    color = TextMuted,
                    style = MaterialTheme.typography.bodySmall,
                )
            }
        }
    }
}

@OptIn(androidx.compose.material3.ExperimentalMaterial3Api::class)
@Composable
private fun InfoSheet(onDismiss: () -> Unit) {
    ModalBottomSheet(
        onDismissRequest = onDismiss,
        sheetState = rememberModalBottomSheetState(),
        containerColor = Surface,
    ) {
        Column(
            modifier = Modifier.padding(20.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Text("About PocketMic", fontWeight = FontWeight.Bold, style = MaterialTheme.typography.titleMedium)
            Text(
                "Uses this phone as an encrypted low-latency microphone for a Windows PC on the same network.",
                color = TextMuted,
            )
            Text("Capture modes", fontWeight = FontWeight.SemiBold)
            Text(
                "Voice uses the phone's voice-communication source — the vendor's own noise suppression, " +
                    "echo handling and gain control, exactly one processing layer. Clean requests unprocessed " +
                    "input where the phone supports it, otherwise voice-recognition input. Custom uses the " +
                    "cleanest source and shapes it with the receiver's processing chain, which runs on the PC.",
                color = TextMuted,
                style = MaterialTheme.typography.bodySmall,
            )
            Text("Security", fontWeight = FontWeight.SemiBold)
            Text(
                "Audio is encrypted with AES-256-GCM using a key derived from the pairing key. " +
                    "Intended for a trusted private network only — do not forward this UDP port to the internet.",
                color = TextMuted,
                style = MaterialTheme.typography.bodySmall,
            )
            Spacer(Modifier.height(12.dp))
        }
    }
}

private fun startService(context: Context, config: MicConfig, peerName: String = "") {
    val intent = Intent(context, MicStreamingService::class.java)
        .setAction(MicStreamingService.ACTION_START)
        .putExtra(MicStreamingService.EXTRA_HOST, config.host)
        .putExtra(MicStreamingService.EXTRA_PORT, config.port)
        .putExtra(MicStreamingService.EXTRA_KEY, config.pairingKey)
        .putExtra(MicStreamingService.EXTRA_MODE, config.captureMode.wireValue)
        .putExtra(MicStreamingService.EXTRA_GAIN, config.gain)
        .putExtra(MicStreamingService.EXTRA_PEER_NAME, peerName)
    ContextCompat.startForegroundService(context, intent)
}

/** Only a key-verified announce may name the machine in the notification. */
private fun discoveredName(peer: PeerState): String =
    (peer as? PeerState.Found)?.takeIf { it.keyMatches }?.hostName.orEmpty()

private fun stopService(context: Context) {
    context.startService(
        Intent(context, MicStreamingService::class.java)
            .setAction(MicStreamingService.ACTION_STOP),
    )
}
