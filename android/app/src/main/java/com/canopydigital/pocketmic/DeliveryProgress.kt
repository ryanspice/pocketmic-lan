package com.canopydigital.pocketmic

/** Run-scoped evidence of delivery, independent of receiver reachability and playback. */
internal class DeliveryProgress {
    private var session: Long? = null
    private var sequence: Int? = null
    private var watermark: Long? = null
    private var resetCandidate: Long? = null
    private var resetAdvances = 0
    private var receiptSupported = false
    var lastProgressAtMillis: Long = 0L
        private set

    fun reset(sessionId: Long? = session) {
        session = sessionId
        sequence = null
        watermark = null
        resetCandidate = null
        resetAdvances = 0
        receiptSupported = false
        lastProgressAtMillis = 0L
    }

    fun observe(stats: ReceiverStats, now: Long) {
        if (stats.sessionId != null) {
            receiptSupported = true
            if (session == null || stats.sessionId != session || stats.sequence == null) return
            val previous = sequence
            // Serial arithmetic handles uint32 sequence rollover. Equal/older receipts do not progress.
            if (previous == null || stats.sequence - previous > 0) {
                sequence = stats.sequence
                lastProgressAtMillis = now
            }
            return
        }
        // Never downgrade a receiver that has supplied session-qualified receipts during this run.
        if (receiptSupported || stats.packets < 0L) return
        val previous = watermark
        if (previous == null) {
            watermark = stats.packets // A baseline alone is not proof of current delivery.
        } else if (stats.packets > previous) {
            watermark = stats.packets
            resetCandidate = null
            resetAdvances = 0
            lastProgressAtMillis = now
        } else if (stats.packets < previous) {
            // Legacy receivers lack a session receipt. Require repeated lower-but-advancing
            // counters before accepting a restart; one reordered sample cannot reset the watermark.
            val candidate = resetCandidate
            resetAdvances = if (candidate != null && stats.packets > candidate) resetAdvances + 1 else 0
            resetCandidate = stats.packets
            if (resetAdvances >= 2) {
                watermark = stats.packets
                resetCandidate = null
                resetAdvances = 0
                lastProgressAtMillis = now
            }
        } else {
            resetCandidate = null
            resetAdvances = 0
        }
    }
}
