package com.ryanspice.pocketmic

/** What the streaming loop should do next about the state of the link. */
enum class LinkAction {
    /** Nothing changes. Keep capturing and sending exactly as before. */
    CONTINUE,

    /** The receiver has gone quiet. Enter RECONNECTING and resume discovery. */
    ENTER_RECONNECTING,

    /** Statistics are flowing again. Return to STREAMING and stop probing. */
    RESUME_STREAMING,

    /** Reconnection has been tried for long enough. Fail with an error the user can act on. */
    GIVE_UP,
}

/**
 * The reconnect decision, as a pure function of four timestamps.
 *
 * Extracted from [MicStreamingService] so the state machine can be tested off-device. The
 * service half of reconnect is unavoidably entangled with `AudioRecord`, sockets and a
 * foreground notification; the *decisions* are not, and those are the part that has to be right.
 *
 * The link is judged solely by the receiver's statistics feed, because it is the only signal
 * that proves delivery. The audio socket is deliberately unconnected — `send()` succeeds whether
 * or not anything is listening — so "we are still transmitting" carries no information at all.
 * This is the same reasoning that put [ControlChannel] there in the first place.
 */
object ReconnectPolicy {
    /**
     * How long statistics may be absent before the link counts as lost. The receiver sends every
     * 500 ms, so this tolerates three consecutive losses before reacting — jitter and a dropped
     * datagram must not be mistaken for an outage.
     */
    const val STATS_TIMEOUT_MS = ControlChannel.STATS_TIMEOUT_MS

    /**
     * Grace period after the microphone opens before silence counts against the link.
     *
     * The receiver only starts sending statistics once it has decrypted a packet from us, so the
     * first reply cannot arrive until audio has made the round trip. Reacting inside this window
     * would declare a reconnect on every single start.
     */
    const val CONNECT_GRACE_MS = 5_000L

    /**
     * How long to keep trying before failing. Deliberately generous: this is a LAN utility, and
     * the common causes — the receiver being restarted, a Wi-Fi roam, a laptop waking — all
     * resolve inside a minute. Giving up early would trade the bug being fixed here for a
     * different one, where the app abandons a link that was about to come back.
     */
    const val GIVE_UP_AFTER_MS = 60_000L

    /**
     * @param streaming true while in STREAMING, false while in RECONNECTING.
     * @param nowMillis monotonic now (SystemClock.elapsedRealtime). Liveness must never be
     *   measured against wall-clock time: a backward NTP jump would otherwise make statistics
     *   look perpetually fresh and the link would never be declared lost.
     * @param streamStartedAtMillis when this run opened the microphone, same clock.
     * @param lastStatsAtMillis when the last receiver statistics frame arrived, 0 for never.
     * @param reconnectStartedAtMillis when the current RECONNECTING episode began; ignored while
     *   [streaming] is true.
     */
    fun evaluate(
        streaming: Boolean,
        nowMillis: Long,
        streamStartedAtMillis: Long,
        lastStatsAtMillis: Long,
        reconnectStartedAtMillis: Long,
    ): LinkAction {
        // Statistics older than the start of this run are from a previous one. The control
        // channel outlives any single stream, so its last-seen timestamp has to be qualified by
        // the run or a stale value would read as a live link for the first two seconds.
        val fresh = lastStatsAtMillis > streamStartedAtMillis &&
            nowMillis - lastStatsAtMillis < STATS_TIMEOUT_MS

        return if (streaming) {
            when {
                fresh -> LinkAction.CONTINUE
                nowMillis - streamStartedAtMillis < CONNECT_GRACE_MS -> LinkAction.CONTINUE
                else -> LinkAction.ENTER_RECONNECTING
            }
        } else {
            when {
                fresh -> LinkAction.RESUME_STREAMING
                nowMillis - reconnectStartedAtMillis >= GIVE_UP_AFTER_MS -> LinkAction.GIVE_UP
                else -> LinkAction.CONTINUE
            }
        }
    }

    /**
     * Whether a rediscovered receiver is somewhere other than where audio is currently being
     * sent. A receiver can move address (DHCP, a different adapter) or port (the shared settings
     * file changed underneath it), and both have to be followed or reconnect would resolve the
     * peer correctly and then keep transmitting into the old hole.
     */
    fun isDifferentTarget(
        currentHost: String,
        currentPort: Int,
        foundHost: String,
        foundPort: Int,
    ): Boolean = foundHost.isNotBlank() &&
        foundPort in 1..65_535 &&
        (foundHost != currentHost || foundPort != currentPort)
}
