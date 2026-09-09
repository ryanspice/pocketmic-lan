package com.ryanspice.pocketmic

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow

enum class StreamStatus {
    IDLE,
    CONNECTING,
    STREAMING,

    /**
     * Capturing and transmitting, but the receiver has stopped confirming delivery.
     *
     * A distinct state rather than a flag on STREAMING because it changes what the app *does*,
     * not only what it says: discovery probing resumes so a receiver that moved can be found
     * again. The microphone and the encryption session are deliberately untouched — tearing
     * either down would restart the packet sequence, and a repeated (key, nonce) pair is a
     * catastrophic AES-GCM failure, not a cosmetic one.
     */
    RECONNECTING,

    ERROR,
}

/** Which Wi-Fi lock the stream actually obtained. Reported so high jitter is explainable. */
enum class WifiLockMode {
    NONE,
    LOW_LATENCY,
    HIGH_PERFORMANCE,
    UNAVAILABLE,
}

data class StreamingSnapshot(
    val status: StreamStatus = StreamStatus.IDLE,
    val destination: String = "",
    val level: Float = 0f,
    val packetsSent: Long = 0L,

    /**
     * Packets the send queue discarded because the sender was behind. Bounded by design — the
     * queue drops the oldest rather than ever blocking capture — so a nonzero value means the
     * network genuinely could not keep up, which is a different diagnosis from a silent
     * capture overrun.
     */
    val packetsDropped: Long = 0L,

    /**
     * Read-loop iterations that took longer than 30 ms. One stall is a scheduling hiccup; a
     * run of them approaching the 40 ms hardware buffer is how the old choppiness started.
     */
    val readStalls: Int = 0,

    /**
     * Times the recorder's frame counter advanced past what this loop consumed plus the buffer
     * capacity. AudioRecord never reports this as an error — it just drops samples — so this
     * counter is the only evidence that capture itself lost audio.
     */
    val overruns: Int = 0,

    /** socket.send() calls slower than 25 ms, from the send coroutine's telemetry. */
    val sendStallCount: Int = 0,

    /** Largest single socket.send() duration observed this run, in milliseconds. */
    val maxSendMillis: Int = 0,

    val error: String? = null,
    val wifiLockMode: WifiLockMode = WifiLockMode.NONE,

    /** The band the phone is on (2.4 GHz / 5 GHz / 6 GHz / unknown), for diagnostics. */
    val wifiBand: String = "",

    /** Current Wi-Fi RSSI in dBm, or 0 when unknown. */
    val wifiRssi: Int = 0,
    /**
     * The microphone source actually obtained. Requested and obtained can differ silently —
     * UNPROCESSED is unavailable on many handsets and falls back to VOICE_RECOGNITION, which
     * already applies vendor processing. Stacking further DSP on that sounds wrong, so the
     * user has to be able to see which one they really got.
     */
    val captureSource: String = "",

    /**
     * How many times this run has lost and regained the receiver. Kept across the whole stream
     * rather than reset on recovery, because "it reconnected eleven times" is the diagnosis for
     * a flaky link, and a counter that resets each time would hide precisely that.
     */
    val reconnects: Int = 0,

    /** When the current RECONNECTING episode started, or 0 while the link is healthy. */
    val reconnectingSinceMillis: Long = 0L,
)

object StreamingState {
    private val mutableState = MutableStateFlow(StreamingSnapshot())
    val state: StateFlow<StreamingSnapshot> = mutableState.asStateFlow()

    fun update(transform: (StreamingSnapshot) -> StreamingSnapshot) {
        mutableState.value = transform(mutableState.value)
    }

    fun reset() {
        mutableState.value = StreamingSnapshot()
    }
}
