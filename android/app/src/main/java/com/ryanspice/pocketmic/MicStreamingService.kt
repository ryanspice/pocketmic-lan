package com.ryanspice.pocketmic

import android.Manifest
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Intent
import android.content.pm.PackageManager
import android.content.pm.ServiceInfo
import android.media.AudioFormat
import android.media.AudioManager
import android.media.AudioRecord
import android.media.AudioTimestamp
import android.media.MediaRecorder
import android.net.wifi.WifiManager
import android.os.Build
import android.os.IBinder
import android.os.PowerManager
import android.os.SystemClock
import android.util.Log
import androidx.annotation.RequiresPermission
import androidx.core.app.NotificationCompat
import androidx.core.content.ContextCompat
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.channels.BufferOverflow
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.currentCoroutineContext
import kotlinx.coroutines.ensureActive
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import java.net.DatagramPacket
import java.net.DatagramSocket
import java.net.Inet4Address
import java.net.InetAddress
import java.security.SecureRandom
import kotlin.math.max
import kotlin.math.roundToInt
import kotlin.math.sqrt

class MicStreamingService : Service() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    @Volatile
    private var streamJob: Job? = null

    @Volatile
    private var activeAudioRecord: AudioRecord? = null

    @Volatile
    private var destroying = false

    private var wakeLock: PowerManager.WakeLock? = null

    private var wifiLock: WifiManager.WifiLock? = null

    /** The mode the held lock was created with; a WifiLock's mode cannot change after creation. */
    private var wifiLockModeHeld = 0

    /** Machine name of the receiver, supplied by discovery. Blank when entered manually. */
    private var peerName: String = ""

    override fun onCreate() {
        super.onCreate()
        createNotificationChannel()
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        try {
            when (intent?.action) {
                ACTION_STOP -> stopStreaming()
                ACTION_START -> startStreaming(intent)
                else -> stopSelf(startId)
            }
        } catch (error: Exception) {
            StreamingState.update {
                it.copy(
                    status = StreamStatus.ERROR,
                    level = 0f,
                    error = error.message ?: error.javaClass.simpleName,
                )
            }
            stopSelf(startId)
        }
        return START_NOT_STICKY
    }

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onDestroy() {
        destroying = true
        streamJob?.cancel()
        stopActiveAudioRecord()
        releaseWakeLock()
        stopForeground(STOP_FOREGROUND_REMOVE)
        scope.cancel()
        if (StreamingState.state.value.status != StreamStatus.ERROR) {
            StreamingState.reset()
        }
        super.onDestroy()
    }

    private fun startStreaming(intent: Intent) {
        if (streamJob != null) return

        peerName = intent.getStringExtra(EXTRA_PEER_NAME).orEmpty()

        val config = MicConfig(
            host = intent.getStringExtra(EXTRA_HOST).orEmpty().trim(),
            port = intent.getIntExtra(EXTRA_PORT, DEFAULT_PORT),
            pairingKey = intent.getStringExtra(EXTRA_KEY).orEmpty(),
            captureMode = CaptureMode.fromWireValue(intent.getStringExtra(EXTRA_MODE)),
            gain = intent.getFloatExtra(EXTRA_GAIN, 1.0f).coerceIn(0.5f, 3.0f),
        )

        val configError = validate(config)
        if (configError != null) {
            StreamingState.update { it.copy(status = StreamStatus.ERROR, error = configError) }
            stopSelf()
            return
        }

        val notification = createNotification(config)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(
                NOTIFICATION_ID,
                notification,
                ServiceInfo.FOREGROUND_SERVICE_TYPE_MICROPHONE,
            )
        } else {
            startForeground(NOTIFICATION_ID, notification)
        }

        StreamingState.update {
            StreamingSnapshot(
                status = StreamStatus.CONNECTING,
                destination = describeDestination(config),
            )
        }

        acquireWakeLock()
        val job = scope.launch { runStream(config) }
        streamJob = job
        job.invokeOnCompletion {
            if (streamJob === job) streamJob = null
        }
    }

    private suspend fun runStream(config: MicConfig) {
        var audioRecord: AudioRecord? = null
        var socket: DatagramSocket? = null
        var senderJob: Job? = null
        var sendQueue: Channel<ByteArray>? = null
        var terminalError: String? = null

        try {
            if (ContextCompat.checkSelfPermission(this, Manifest.permission.RECORD_AUDIO) != PackageManager.PERMISSION_GRANTED) {
                error("Microphone permission is not granted.")
            }

            val coroutineContext = currentCoroutineContext()
            coroutineContext.ensureActive()

            val address = InetAddress.getAllByName(config.host)
                .firstOrNull { it is Inet4Address }
                ?: error("The Windows receiver address did not resolve to IPv4.")
            coroutineContext.ensureActive()

            // Keep the UDP socket unconnected. A connected DatagramSocket can surface an ICMP
            // port-unreachable error when the PC receiver starts after the phone; unconnected
            // sends allow the stream to keep running until the receiver is ready.
            socket = DatagramSocket().apply {
                sendBufferSize = UDP_SEND_BUFFER_BYTES
            }

            val samplesPerPacket = SAMPLE_RATE / PACKETS_PER_SECOND
            val pcmBytes = ByteArray(samplesPerPacket * Short.SIZE_BYTES)
            val samples = ShortArray(samplesPerPacket)
            val minBuffer = AudioRecord.getMinBufferSize(
                SAMPLE_RATE,
                AudioFormat.CHANNEL_IN_MONO,
                AudioFormat.ENCODING_PCM_16BIT,
            )
            check(minBuffer > 0) {
                "This phone does not support 48 kHz mono PCM microphone capture."
            }

            // Four packets gives the recorder room to survive short scheduler stalls without
            // forcing the previous 80 ms minimum on devices whose native buffer is smaller.
            // The capture loop no longer sends on this thread, so the buffer only has to cover
            // scheduling, not socket stalls — but the floor stays: a smaller native buffer
            // would make the phone's own scheduler the limiting factor.
            val audioBufferBytes = max(minBuffer, pcmBytes.size * MIN_BUFFER_PACKET_MULTIPLIER)
            val format = AudioFormat.Builder()
                .setEncoding(AudioFormat.ENCODING_PCM_16BIT)
                .setSampleRate(SAMPLE_RATE)
                .setChannelMask(AudioFormat.CHANNEL_IN_MONO)
                .build()

            // Capture is deliberately single-layer. VOICE_COMMUNICATION already applies the
            // vendor's AEC/NS/AGC inside the source; stacking software effects on top of that
            // was the double-DSP that made the default path hissy and pumped. The device layer
            // alone is the one the handset vendor tuned, and it is the only processing this
            // mode applies. (AcousticEchoCanceler specifically has no reference signal here:
            // the phone plays nothing, so cancelling the phone's own playback is a no-op that
            // can only cost CPU.)
            val recorder = createStartedAudioRecord(config.captureMode, format, audioBufferBytes)
            audioRecord = recorder
            activeAudioRecord = recorder

            val streamSessionId = secureRandom.nextLong()
            val encryptor = PacketCrypto.Encryptor(
                key = PacketCrypto.deriveKey(config.pairingKey),
                sessionId = streamSessionId,
                sampleRate = SAMPLE_RATE,
                pcmBytes = pcmBytes.size,
            )
            var sequence = 0
            var uiTick = 0
            var framesConsumed = 0L
            var readStalls = 0
            var overruns = 0
            val senderTelemetry = SenderTelemetry()

            // Link state. `streaming` false means RECONNECTING: still capturing, still
            // transmitting, no longer being confirmed. Every one of these is a local of this
            // function, which is what structurally guarantees a reconnect can never restart the
            // sequence — recovery never leaves this frame, and the only way to reach a fresh
            // `sequence = 0` is to leave it and build a new encryptor with a new session id.
            //
            // All liveness timestamps use the monotonic clock. Wall-clock time can jump
            // backwards (NTP correction), which used to make statistics look perpetually fresh
            // and the link never lost; elapsedRealtime only moves forward.
            val streamStartedAt = SystemClock.elapsedRealtime()
            var streaming = true
            var reconnectStartedAt = 0L
            var reconnects = 0
            var targetHost = config.host
            var targetPort = config.port
            var lastWifiPolicyCheck = streamStartedAt

            coroutineContext.ensureActive()

            StreamingState.update {
                it.copy(status = StreamStatus.STREAMING, error = null)
            }

            // ---- the send side, on its own coroutine -------------------------------------
            //
            // The capture loop must never block on the network. A stalled socket.send() used to
            // stall the AudioRecord read loop, and any stall longer than the 40 ms hardware
            // buffer silently dropped samples with no error. The sender owns the socket, the
            // queue decouples them, and DROP_OLDEST bounds latency: when the network cannot
            // keep up, the freshest packets win and the backlog can never grow without limit.
            val queue = Channel<ByteArray>(
                capacity = SEND_QUEUE_PACKETS,
                onBufferOverflow = BufferOverflow.DROP_OLDEST,
                // Every packet the queue evicts (or that is still queued at shutdown) is
                // reported here, so the dropped counter is exact. Written on the capture
                // thread only, so a plain @Volatile field is sufficient.
                onUndeliveredElement = { senderTelemetry.dropped += 1 },
            )
            sendQueue = queue
            val sendTarget = SendTarget(address, config.port)
            senderJob = scope.launch(Dispatchers.IO) {
                val senderDatagram = DatagramPacket(ByteArray(0), 0, address, config.port)
                while (coroutineContext.isActive) {
                    val packet = queue.receiveCatching().getOrNull() ?: break
                    senderDatagram.setData(packet, 0, packet.size)
                    senderDatagram.address = sendTarget.address
                    senderDatagram.port = sendTarget.port
                    val sendStarted = SystemClock.elapsedRealtime()
                    runCatching { socket?.send(senderDatagram) }
                    val sendMs = (SystemClock.elapsedRealtime() - sendStarted).toInt()
                    if (sendMs > senderTelemetry.maxSendMs) senderTelemetry.maxSendMs = sendMs
                    if (sendMs >= SEND_STALL_THRESHOLD_MS) senderTelemetry.stalls += 1
                    senderTelemetry.sent += 1
                }
            }

            while (coroutineContext.isActive) {
                val readStartedAt = SystemClock.elapsedRealtime()
                var samplesRead = 0
                while (samplesRead < samples.size && coroutineContext.isActive) {
                    val read = recorder.read(
                        samples,
                        samplesRead,
                        samples.size - samplesRead,
                        AudioRecord.READ_BLOCKING,
                    )

                    if (!coroutineContext.isActive) break
                    when {
                        read > 0 -> samplesRead += read
                        read == 0 -> continue
                        else -> error(audioReadError(read))
                    }
                }
                if (!coroutineContext.isActive) break
                if (samplesRead != samples.size) continue

                framesConsumed += samplesRead
                val readMs = SystemClock.elapsedRealtime() - readStartedAt
                if (readMs >= READ_STALL_THRESHOLD_MS) readStalls += 1

                uiTick += 1
                val updateUi = uiTick >= UI_UPDATE_EVERY_PACKETS
                var sumSquares = 0.0
                val unityGain = config.gain == 1.0f

                for (index in samples.indices) {
                    val scaled = if (unityGain) {
                        samples[index].toInt()
                    } else {
                        (samples[index] * config.gain)
                            .roundToInt()
                            .coerceIn(Short.MIN_VALUE.toInt(), Short.MAX_VALUE.toInt())
                    }

                    pcmBytes[index * 2] = (scaled and 0xff).toByte()
                    pcmBytes[index * 2 + 1] = ((scaled ushr 8) and 0xff).toByte()
                    if (updateUi) {
                        sumSquares += scaled.toDouble() * scaled.toDouble()
                    }
                }

                if (sequence == -1) {
                    error("Packet sequence exhausted; restart the stream to create a new encryption session.")
                }
                val packetBytes = encryptor.encrypt(sequence, pcmBytes)
                // The encryptor reuses its internal packet buffer on the next call, so the
                // queued copy is what makes an asynchronous send safe. One kilobyte per packet
                // is nothing against the queue's 100-packet-per-second throughput.
                queue.trySend(packetBytes.copyOf())
                sequence += 1

                if (updateUi) {
                    uiTick = 0
                    val now = SystemClock.elapsedRealtime()
                    val rms = sqrt(sumSquares / samples.size) / Short.MAX_VALUE
                    val visualLevel = (rms * 4.0).toFloat().coerceIn(0f, 1f)
                    StreamingState.update {
                        it.copy(
                            level = visualLevel,
                            packetsSent = senderTelemetry.sent,
                            packetsDropped = senderTelemetry.dropped,
                            readStalls = readStalls,
                            overruns = overruns,
                            sendStallCount = senderTelemetry.stalls,
                            maxSendMillis = senderTelemetry.maxSendMs,
                        )
                    }

                    // An AudioRecord overrun never surfaces as an error code — the hardware
                    // simply drops samples. Comparing the recorder's own frame counter against
                    // what this loop has consumed turns the silent overrun into a number on the
                    // diagnostics screen.
                    val timestamp = AudioTimestamp()
                    if (recorder.getTimestamp(timestamp, AudioTimestamp.TIMEBASE_MONOTONIC) ==
                        AudioRecord.SUCCESS
                    ) {
                        val unconsumed = timestamp.framePosition - framesConsumed
                        val bufferFrames = audioBufferBytes / Short.SIZE_BYTES
                        if (unconsumed > bufferFrames + samplesPerPacket) overruns += 1
                    }

                    // The Wi-Fi lock policy is fed by the live network, so it is re-evaluated
                    // on a slow cadence: a roam across a band boundary should switch modes, and
                    // sustained receiver-side loss should back the sender off LOW_LATENCY.
                    if (now - lastWifiPolicyCheck >= WIFI_POLICY_RECHECK_MS) {
                        lastWifiPolicyCheck = now
                        revalidateWifiLock()
                    }

                    // Ten packets, so a hundred milliseconds. Fast enough that a two-second
                    // statistics timeout is noticed promptly, slow enough that the clock read
                    // and the flow read stay off the ninety percent of iterations that do
                    // nothing but capture and send.
                    when (
                        ReconnectPolicy.evaluate(
                            streaming = streaming,
                            nowMillis = now,
                            streamStartedAtMillis = streamStartedAt,
                            lastStatsAtMillis = ControlChannel.lastStatsAtMillis.value,
                            reconnectStartedAtMillis = reconnectStartedAt,
                        )
                    ) {
                        LinkAction.CONTINUE -> Unit

                        LinkAction.ENTER_RECONNECTING -> {
                            streaming = false
                            reconnectStartedAt = now

                            // A lost link usually means the network changed; re-check the lock
                            // policy against whatever the radio is doing now.
                            revalidateWifiLock()

                            // Probing is suppressed during capture to keep broadcast traffic off
                            // the hot path, and that suppression is precisely why the app could
                            // never heal itself. Turn it back on: a receiver that restarted or
                            // moved cannot be found any other way. The configured port is used,
                            // not any rediscovered one, because that is the port the control
                            // socket is bound to and therefore the only one it can probe on.
                            ControlChannel.setProbing(this@MicStreamingService, config.port, enabled = true)
                            StreamingState.update {
                                it.copy(status = StreamStatus.RECONNECTING, reconnectingSinceMillis = now)
                            }
                        }

                        LinkAction.RESUME_STREAMING -> {
                            streaming = true
                            reconnects += 1
                            reconnectStartedAt = 0L
                            ControlChannel.setProbing(this@MicStreamingService, config.port, enabled = false)
                            StreamingState.update {
                                it.copy(
                                    status = StreamStatus.STREAMING,
                                    error = null,
                                    reconnects = reconnects,
                                    reconnectingSinceMillis = 0L,
                                )
                            }
                        }

                        LinkAction.GIVE_UP -> error(
                            "No reply from the receiver for " +
                                "${ReconnectPolicy.GIVE_UP_AFTER_MS / 1_000} seconds. Check that " +
                                "PocketMicReceiver is running on the PC and that both devices are " +
                                "on the same Wi-Fi network.",
                        )
                    }

                    if (!streaming) {
                        val found = ControlChannel.peer.value as? PeerState.Found
                        if (found != null && found.keyMatches && found.audioProtocolMatches &&
                            ReconnectPolicy.isDifferentTarget(
                                targetHost,
                                targetPort,
                                found.address,
                                found.audioPort,
                            )
                        ) {
                            val resolved = runCatching {
                                InetAddress.getAllByName(found.address)
                                    .firstOrNull { it is Inet4Address }
                            }.getOrNull()

                            if (resolved != null) {
                                targetHost = found.address
                                targetPort = found.audioPort
                                sendTarget.address = resolved
                                sendTarget.port = targetPort
                                redirectTo(config.copy(host = targetHost, port = targetPort), found.hostName)
                            }
                        }
                    }
                }
            }
        } catch (cancelled: CancellationException) {
            throw cancelled
        } catch (error: Exception) {
            terminalError = error.message ?: error.javaClass.simpleName
        } finally {
            if (activeAudioRecord === audioRecord) activeAudioRecord = null
            senderJob?.cancel()
            sendQueue?.close()
            runCatching { audioRecord?.stop() }
            runCatching { audioRecord?.release() }
            runCatching { socket?.close() }
            releaseWakeLock()
            if (!destroying) {
                terminalError?.let { message ->
                    StreamingState.update {
                        it.copy(
                            status = StreamStatus.ERROR,
                            level = 0f,
                            error = message,
                        )
                    }
                }
                stopForeground(STOP_FOREGROUND_REMOVE)
                stopSelf()
            }
        }
    }

    // runStream() verifies RECORD_AUDIO before reaching here; the annotation carries that
    // contract to callers so lint can prove the AudioRecord.Builder call is guarded.
    @RequiresPermission(Manifest.permission.RECORD_AUDIO)
    private fun createStartedAudioRecord(
        mode: CaptureMode,
        format: AudioFormat,
        bufferSizeBytes: Int,
    ): AudioRecord {
        var lastFailure: Exception? = null
        for (source in audioSourceCandidates(mode)) {
            var candidate: AudioRecord? = null
            var keepCandidate = false
            try {
                candidate = AudioRecord.Builder()
                    .setAudioSource(source)
                    .setAudioFormat(format)
                    .setBufferSizeInBytes(bufferSizeBytes)
                    .build()

                if (candidate.state != AudioRecord.STATE_INITIALIZED) {
                    lastFailure = IllegalStateException("Microphone source $source did not initialize.")
                    continue
                }

                candidate.startRecording()
                if (candidate.recordingState == AudioRecord.RECORDSTATE_RECORDING) {
                    keepCandidate = true
                    StreamingState.update { it.copy(captureSource = audioSourceName(source)) }
                    return candidate
                }
                lastFailure = IllegalStateException("Microphone source $source did not start recording.")
            } catch (error: Exception) {
                lastFailure = error
            } finally {
                if (!keepCandidate) {
                    runCatching { candidate?.stop() }
                    runCatching { candidate?.release() }
                }
            }
        }

        throw IllegalStateException(
            "Android could not start a 48 kHz mono microphone source.",
            lastFailure,
        )
    }

    private fun audioSourceName(source: Int): String = when (source) {
        MediaRecorder.AudioSource.UNPROCESSED -> "UNPROCESSED (raw)"
        MediaRecorder.AudioSource.VOICE_COMMUNICATION -> "VOICE_COMMUNICATION (device processed)"
        MediaRecorder.AudioSource.VOICE_RECOGNITION -> "VOICE_RECOGNITION (partly processed)"
        MediaRecorder.AudioSource.MIC -> "MIC (default)"
        else -> "source $source"
    }

    private fun audioSourceCandidates(mode: CaptureMode): List<Int> {
        val candidates = mutableListOf<Int>()
        if (mode == CaptureMode.VOICE) {
            candidates += MediaRecorder.AudioSource.VOICE_COMMUNICATION
        } else if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.N) {
            val audioManager = getSystemService(AudioManager::class.java)
            val supportsUnprocessed = audioManager
                ?.getProperty(AudioManager.PROPERTY_SUPPORT_AUDIO_SOURCE_UNPROCESSED)
                ?.toBooleanStrictOrNull() == true
            if (supportsUnprocessed) candidates += MediaRecorder.AudioSource.UNPROCESSED
        }

        candidates += MediaRecorder.AudioSource.VOICE_RECOGNITION
        candidates += MediaRecorder.AudioSource.MIC
        return candidates.distinct()
    }

    /**
     * Points the live stream at a receiver that discovery has just found somewhere other than
     * where audio is currently being sent.
     *
     * Only the destination moves. The microphone, the socket, the cipher and the packet sequence
     * are all untouched, which is the entire reason reconnect is safe: the AES-GCM nonce is the
     * session id followed by the sequence, so anything that restarted the sequence inside a
     * session would reuse a (key, nonce) pair. Redirecting a datagram cannot.
     */
    private fun redirectTo(config: MicConfig, discoveredName: String) {
        peerName = discoveredName
        val destination = describeDestination(config)
        StreamingState.update { it.copy(destination = destination) }

        runCatching {
            getSystemService(NotificationManager::class.java)
                ?.notify(NOTIFICATION_ID, createNotification(config))
        }
    }

    /**
     * Prefers the receiver's own machine name over a bare address: "SPICE-PC-2023" tells you at
     * a glance which machine has your microphone, an IP address does not.
     */
    private fun describeDestination(config: MicConfig): String = if (peerName.isNotBlank()) {
        "$peerName (${config.host})"
    } else {
        "${config.host}:${config.port}"
    }

    private fun stopStreaming() {
        streamJob?.cancel()
        stopActiveAudioRecord()
        stopSelf()
    }

    private fun stopActiveAudioRecord() {
        runCatching { activeAudioRecord?.stop() }
    }

    private fun audioReadError(code: Int): String = when (code) {
        AudioRecord.ERROR_DEAD_OBJECT -> "The microphone device disconnected and must be restarted."
        AudioRecord.ERROR_INVALID_OPERATION -> "Microphone capture is no longer in a valid recording state."
        AudioRecord.ERROR_BAD_VALUE -> "Android rejected the microphone read buffer."
        AudioRecord.ERROR -> "Android reported an unspecified microphone read failure."
        else -> "Microphone read failed with code $code."
    }

    private fun validate(config: MicConfig): String? = config.validateConnection()

    private fun createNotification(config: MicConfig): Notification {
        val openAppIntent = PendingIntent.getActivity(
            this,
            0,
            Intent(this, MainActivity::class.java),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        val stopIntent = PendingIntent.getService(
            this,
            1,
            Intent(this, MicStreamingService::class.java).setAction(ACTION_STOP),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )

        val destination = describeDestination(config)

        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_stat_mic)
            .setContentTitle("PocketMic → $destination")
            .setContentText("Streaming microphone audio to $destination")
            .setContentIntent(openAppIntent)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .addAction(R.drawable.ic_stat_mic, "Stop", stopIntent)
            .build()
    }

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val manager = getSystemService(NotificationManager::class.java)
        manager.createNotificationChannel(
            NotificationChannel(
                CHANNEL_ID,
                "Microphone streaming",
                NotificationManager.IMPORTANCE_LOW,
            ).apply {
                description = "Shown while PocketMic captures and sends microphone audio."
            },
        )
    }

    private fun acquireWakeLock() {
        if (wakeLock?.isHeld != true) {
            wakeLock = getSystemService(PowerManager::class.java)
                .newWakeLock(PowerManager.PARTIAL_WAKE_LOCK, "$packageName:stream")
                .apply {
                    setReferenceCounted(false)
                    acquire()
                }
        }
        acquireWifiLock()
    }

    /**
     * A partial wake lock keeps the CPU running but leaves the Wi-Fi radio free to enter
     * power save, which delivers packets in clumps instead of pacing them. Measured without
     * this lock: median interarrival 0 ms (bursts) with gaps up to 250 ms, against a 10 ms
     * ideal. The lock mode is a policy choice fed by the live network rather than a constant:
     * LOW_LATENCY additionally disables aggregation-induced delay where supported, which helps
     * pacing on a clean 5 GHz link but trades per-packet airtime contention on a crowded or
     * weak one, so it is only preferred where measurements say it wins.
     */
    private fun acquireWifiLock() {
        if (wifiLock?.isHeld == true) return
        val wifiManager = getSystemService(WifiManager::class.java) ?: run {
            publishWifiState(acquired = null)
            return
        }

        val mode = desiredWifiLockMode()
        val acquired = runCatching {
            wifiManager.createWifiLock(mode, "$packageName:stream").apply {
                setReferenceCounted(false)
                acquire()
            }
        }.onFailure { error ->
            Log.w(TAG, "Wi-Fi lock acquisition failed (${error.message}); continuing without it.")
        }.getOrNull()

        wifiLock = acquired
        wifiLockModeHeld = mode
        publishWifiState(acquired)
    }

    /**
     * Re-checks the lock against the current network and mode-change policy, releasing and
     * re-acquiring when the desired mode has changed (a WifiLock's mode is fixed at creation).
     * Called on a ten-second cadence and whenever the link enters RECONNECTING.
     */
    private fun revalidateWifiLock() {
        val current = wifiLock
        if (current?.isHeld != true) {
            acquireWifiLock()
            return
        }
        if (desiredWifiLockMode() == wifiLockModeHeld) return

        releaseWifiLock()
        acquireWifiLock()
    }

    /**
     * Which lock mode the current network warrants.
     *
     * Provisional thresholds, decided from the project's own A/B data (ROADMAP.md:190-206):
     * LOW_LATENCY fixed clumping and improved p99 on the owner's clean 5 GHz link but left the
     * tail unchanged, and on a weak or crowded radio its aggregation-free per-packet
     * contention is the wrong trade. The band/RSSI rule and the loss gate below encode that;
     * both are policy, not protocol, and should be re-measured on the owner's network.
     */
    @Suppress("DEPRECATION")
    private fun desiredWifiLockMode(): Int {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.Q) {
            return WifiManager.WIFI_MODE_FULL_HIGH_PERF
        }
        val wifiManager = getSystemService(WifiManager::class.java)
            ?: return WifiManager.WIFI_MODE_FULL_LOW_LATENCY
        val info = runCatching { wifiManager.connectionInfo }.getOrNull()
            ?: return WifiManager.WIFI_MODE_FULL_LOW_LATENCY

        // Receiver statistics are cumulative since the run started, so a cumulative loss rate
        // above 2 % means the link is genuinely losing packets, not one jitter blip.
        val stats = ControlChannel.stats.value
        val sustainedLoss = stats != null && stats.packets > 0 &&
            stats.lost.toDouble() / stats.packets > 0.02

        val fiveGhz = info.frequency >= 4_900
        val strongRssi = info.rssi >= -75
        return if (fiveGhz && strongRssi && !sustainedLoss) {
            WifiManager.WIFI_MODE_FULL_LOW_LATENCY
        } else {
            WifiManager.WIFI_MODE_FULL_HIGH_PERF
        }
    }

    /** Publishes the lock mode plus the live band/RSSI so diagnostics can explain the choice. */
    @Suppress("DEPRECATION")
    private fun publishWifiState(acquired: WifiManager.WifiLock?) {
        val info = runCatching { getSystemService(WifiManager::class.java)?.connectionInfo }.getOrNull()
        val band = when {
            info == null || info.frequency <= 0 -> "unknown"
            info.frequency >= 5_925 -> "6 GHz"
            info.frequency >= 4_900 -> "5 GHz"
            else -> "2.4 GHz"
        }
        StreamingState.update {
            it.copy(
                wifiLockMode = when {
                    acquired?.isHeld != true -> WifiLockMode.UNAVAILABLE
                    wifiLockModeHeld == WifiManager.WIFI_MODE_FULL_LOW_LATENCY -> WifiLockMode.LOW_LATENCY
                    else -> WifiLockMode.HIGH_PERFORMANCE
                },
                wifiBand = band,
                wifiRssi = info?.rssi ?: 0,
            )
        }
    }

    private fun releaseWifiLock() {
        runCatching {
            wifiLock?.let { lock ->
                if (lock.isHeld) lock.release()
            }
        }
        wifiLock = null
        wifiLockModeHeld = 0
    }

    private fun releaseWakeLock() {
        releaseWifiLock()
        wakeLock?.let { lock ->
            if (lock.isHeld) lock.release()
        }
        wakeLock = null
    }

    companion object {
        const val TAG = "PocketMic"

        const val ACTION_START = "com.ryanspice.pocketmic.START"
        const val ACTION_STOP = "com.ryanspice.pocketmic.STOP"
        const val EXTRA_HOST = "host"
        const val EXTRA_PORT = "port"
        const val EXTRA_KEY = "key"
        const val EXTRA_MODE = "mode"
        const val EXTRA_GAIN = "gain"
        const val EXTRA_PEER_NAME = "peer_name"

        private const val SAMPLE_RATE = 48_000
        private const val PACKETS_PER_SECOND = 100
        private const val UI_UPDATE_EVERY_PACKETS = 10

        /** Four 10 ms packets gives the recorder room to survive short scheduler stalls. */
        private const val MIN_BUFFER_PACKET_MULTIPLIER = 4

        private const val UDP_SEND_BUFFER_BYTES = 256 * 1024

        /**
         * How many packets the send queue may hold before the oldest is dropped. Eight packets
         * is 80 ms of audio: enough to absorb a send() spike without ever growing into a
         * latency problem, because the queue bounds latency by construction.
         */
        private const val SEND_QUEUE_PACKETS = 8

        /** A socket.send() slower than this counts as a send stall, for diagnostics. */
        private const val SEND_STALL_THRESHOLD_MS = 25L

        /** A read-loop iteration slower than this counts as a read stall, for diagnostics. */
        private const val READ_STALL_THRESHOLD_MS = 30L

        /** How often the Wi-Fi lock policy is re-evaluated against the live network. */
        private const val WIFI_POLICY_RECHECK_MS = 10_000L

        private const val CHANNEL_ID = "mic_stream"
        private const val NOTIFICATION_ID = DEFAULT_PORT
        private val secureRandom = SecureRandom()
    }
}

/**
 * Counters the send coroutine publishes for the UI tick to read. Separate object rather than
 * plain locals because the sender runs on its own coroutine; every field is written by exactly
 * one thread and read at 10 Hz, so @Volatile is all the synchronisation these need.
 */
private class SenderTelemetry {
    @Volatile
    var sent = 0L

    @Volatile
    var dropped = 0L

    @Volatile
    var stalls = 0

    @Volatile
    var maxSendMs = 0
}

/**
 * The packet's destination, read by the send coroutine on every packet. Local to one stream so
 * a discovery redirect can repoint the sender without touching the socket; @Volatile because
 * the redirect happens on the capture coroutine.
 */
private class SendTarget(@Volatile var address: InetAddress, @Volatile var port: Int)
