package com.ryanspice.pocketmic

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The reconnect state machine, and the invariant it must never break.
 *
 * The decision half of reconnect is pure by construction so it can be tested here rather than by
 * pulling the Wi-Fi out and watching. The safety half — that recovering a link can never reuse a
 * packet sequence within a session — is tested against the real cipher, because an AES-GCM
 * (key, nonce) collision is a total loss of confidentiality for both messages involved, not a
 * cosmetic defect, and "the code looks like it cannot happen" is not the standard for that.
 */
class ReconnectPolicyTest {
    private val started = 1_000_000L

    @Test
    fun aHealthyLinkIsLeftAlone() {
        val now = started + 30_000L

        val action = ReconnectPolicy.evaluate(
            streaming = true,
            nowMillis = now,
            streamStartedAtMillis = started,
            lastStatsAtMillis = now - 200L,
            reconnectStartedAtMillis = 0L,
        )

        assertEquals(LinkAction.CONTINUE, action)
    }

    /**
     * The receiver only starts reporting once it has decrypted a packet from us, so the first
     * reply cannot arrive until audio has made the round trip. Reacting inside that window would
     * declare a reconnect on every single start.
     */
    @Test
    fun silenceIsToleratedWhileTheFirstReplyIsStillPlausible() {
        val action = ReconnectPolicy.evaluate(
            streaming = true,
            nowMillis = started + ReconnectPolicy.CONNECT_GRACE_MS - 1,
            streamStartedAtMillis = started,
            lastStatsAtMillis = 0L,
            reconnectStartedAtMillis = 0L,
        )

        assertEquals(LinkAction.CONTINUE, action)
    }

    @Test
    fun aReceiverThatNeverRepliesEventuallyTriggersReconnect() {
        val action = ReconnectPolicy.evaluate(
            streaming = true,
            nowMillis = started + ReconnectPolicy.CONNECT_GRACE_MS,
            streamStartedAtMillis = started,
            lastStatsAtMillis = 0L,
            reconnectStartedAtMillis = 0L,
        )

        assertEquals(LinkAction.ENTER_RECONNECTING, action)
    }

    @Test
    fun statisticsFallingSilentDuringAHealthyStreamTriggersReconnect() {
        val now = started + 30_000L

        val action = ReconnectPolicy.evaluate(
            streaming = true,
            nowMillis = now,
            streamStartedAtMillis = started,
            lastStatsAtMillis = now - ReconnectPolicy.STATS_TIMEOUT_MS,
            reconnectStartedAtMillis = 0L,
        )

        assertEquals(LinkAction.ENTER_RECONNECTING, action)
    }

    /**
     * A single dropped statistics datagram must not look like an outage. The receiver sends every
     * 500 ms, so the timeout deliberately tolerates three consecutive losses.
     */
    @Test
    fun oneLateStatisticsFrameIsNotAnOutage() {
        val now = started + 30_000L

        val action = ReconnectPolicy.evaluate(
            streaming = true,
            nowMillis = now,
            streamStartedAtMillis = started,
            lastStatsAtMillis = now - (ReconnectPolicy.STATS_TIMEOUT_MS - 1),
            reconnectStartedAtMillis = 0L,
        )

        assertEquals(LinkAction.CONTINUE, action)
    }

    /**
     * The control channel outlives any one stream, so its last-seen timestamp has to be qualified
     * by the run. Without that, a stale reply from a previous session would read as a live link
     * for the first two seconds of the next one.
     */
    @Test
    fun statisticsFromBeforeThisRunDoNotCountAsALiveLink() {
        val action = ReconnectPolicy.evaluate(
            streaming = true,
            nowMillis = started + ReconnectPolicy.CONNECT_GRACE_MS,
            streamStartedAtMillis = started,
            lastStatsAtMillis = started - 100L,
            reconnectStartedAtMillis = 0L,
        )

        assertEquals(LinkAction.ENTER_RECONNECTING, action)
    }

    @Test
    fun statisticsReturningEndsTheReconnect() {
        val now = started + 40_000L

        val action = ReconnectPolicy.evaluate(
            streaming = false,
            nowMillis = now,
            streamStartedAtMillis = started,
            lastStatsAtMillis = now - 100L,
            reconnectStartedAtMillis = now - 6_000L,
        )

        assertEquals(LinkAction.RESUME_STREAMING, action)
    }

    @Test
    fun reconnectKeepsTryingUntilTheBoundIsReached() {
        val reconnectStarted = started + 10_000L

        val stillTrying = ReconnectPolicy.evaluate(
            streaming = false,
            nowMillis = reconnectStarted + ReconnectPolicy.GIVE_UP_AFTER_MS - 1,
            streamStartedAtMillis = started,
            lastStatsAtMillis = 0L,
            reconnectStartedAtMillis = reconnectStarted,
        )
        val exhausted = ReconnectPolicy.evaluate(
            streaming = false,
            nowMillis = reconnectStarted + ReconnectPolicy.GIVE_UP_AFTER_MS,
            streamStartedAtMillis = started,
            lastStatsAtMillis = 0L,
            reconnectStartedAtMillis = reconnectStarted,
        )

        assertEquals(LinkAction.CONTINUE, stillTrying)
        assertEquals(LinkAction.GIVE_UP, exhausted)
    }

    /**
     * Recovery is checked before the deadline: a receiver that answers on the very last pass has
     * reconnected, not failed.
     */
    @Test
    fun aReplyOnTheLastPassRecoversRatherThanFails() {
        val reconnectStarted = started + 10_000L
        val now = reconnectStarted + ReconnectPolicy.GIVE_UP_AFTER_MS

        val action = ReconnectPolicy.evaluate(
            streaming = false,
            nowMillis = now,
            streamStartedAtMillis = started,
            lastStatsAtMillis = now - 10L,
            reconnectStartedAtMillis = reconnectStarted,
        )

        assertEquals(LinkAction.RESUME_STREAMING, action)
    }

    @Test
    fun aReceiverAtTheSameAddressAndPortIsNotARedirect() {
        assertFalse(ReconnectPolicy.isDifferentTarget("10.0.0.35", 49_500, "10.0.0.35", 49_500))
    }

    @Test
    fun aReceiverThatMovedAddressOrPortIsARedirect() {
        assertTrue(ReconnectPolicy.isDifferentTarget("10.0.0.35", 49_500, "10.0.0.41", 49_500))
        assertTrue(ReconnectPolicy.isDifferentTarget("10.0.0.35", 49_500, "10.0.0.35", 49_600))
    }

    /**
     * An unauthenticated announce carries a blank name and a zero port. Following that would
     * point a live stream at nothing at all.
     */
    @Test
    fun anIncompleteAnnounceIsNeverFollowed() {
        assertFalse(ReconnectPolicy.isDifferentTarget("10.0.0.35", 49_500, "", 49_500))
        assertFalse(ReconnectPolicy.isDifferentTarget("10.0.0.35", 49_500, "10.0.0.41", 0))
        assertFalse(ReconnectPolicy.isDifferentTarget("10.0.0.35", 49_500, "10.0.0.41", 70_000))
    }

    @Test
    fun theHighestLegalReceiverPortIsAccepted() {
        // The port range boundary is 1..65_535; the other extreme is covered by the invalid
        // cases above, so the upper edge is the one that needs pinning.
        assertTrue(ReconnectPolicy.isDifferentTarget("10.0.0.35", 49_500, "10.0.0.41", 65_535))
    }

    /**
     * The invariant reconnect exists to protect. The AES-GCM nonce is the session id followed by
     * the packet sequence, so reusing a sequence inside a session reuses a (key, nonce) pair —
     * which reveals the XOR of both plaintexts and destroys the authentication guarantee for the
     * whole key. Reconnecting must therefore never reset the counter, only continue it.
     */
    @Test
    fun aReconnectNeverReusesASequenceWithinASession() {
        val encryptor = PacketCrypto.Encryptor(
            key = PacketCrypto.deriveKey("POCKETMIC-TEST"),
            sessionId = 0x0102030405060708L,
            sampleRate = 48_000,
            pcmBytes = 960,
        )
        val pcm = ByteArray(960)
        val used = mutableSetOf<Pair<Long, Int>>()
        var sequence = 0

        fun sendOne() {
            val packet = encryptor.encrypt(sequence, pcm)
            val nonce = readLongBigEndian(packet, 8) to readIntBigEndian(packet, 16)
            assertTrue("(session, sequence) repeated at sequence $sequence", used.add(nonce))
            sequence += 1
        }

        repeat(500) { sendOne() }

        // The link drops here: statistics stop, the app enters RECONNECTING, discovery resumes.
        // The microphone, the socket, the cipher and this counter are all deliberately untouched,
        // which is exactly why what follows is safe.
        repeat(500) { sendOne() }

        assertEquals(1_000, used.size)
        assertEquals(1_000, sequence)
    }

    /**
     * And the other half of the rule: when a restart really is needed, a fresh session id makes
     * the sequences that were already used unreachable rather than merely unlikely.
     */
    @Test
    fun aGenuineRestartUnderANewSessionIdCannotCollideWithSequencesAlreadySent() {
        val key = PacketCrypto.deriveKey("POCKETMIC-TEST")
        val pcm = ByteArray(960)
        val used = mutableSetOf<Pair<Long, Int>>()

        for (sessionId in listOf(0x1111111111111111L, 0x2222222222222222L)) {
            val encryptor = PacketCrypto.Encryptor(key, sessionId, 48_000, 960)
            for (sequence in 0 until 100) {
                val packet = encryptor.encrypt(sequence, pcm)
                assertTrue(
                    "session $sessionId reused sequence $sequence",
                    used.add(readLongBigEndian(packet, 8) to readIntBigEndian(packet, 16)),
                )
            }
        }

        assertEquals(200, used.size)
    }

    private fun readLongBigEndian(source: ByteArray, offset: Int): Long {
        var value = 0L
        for (index in 0 until 8) {
            value = (value shl 8) or (source[offset + index].toLong() and 0xff)
        }
        return value
    }

    private fun readIntBigEndian(source: ByteArray, offset: Int): Int {
        var value = 0
        for (index in 0 until 4) {
            value = (value shl 8) or (source[offset + index].toInt() and 0xff)
        }
        return value
    }
}
