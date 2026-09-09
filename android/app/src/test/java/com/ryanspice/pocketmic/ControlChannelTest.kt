package com.ryanspice.pocketmic

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The probe-nonce freshness gate: an announce is only believed when it echoes a probe this
 * phone actually sent recently. The HMAC proves the sender holds the key; the nonce proves the
 * reply belongs to this conversation and this round — together they close the replay hole where
 * a recorded announce (same key) was accepted as fresh discovery.
 */
class ControlChannelTest {
    private fun nonce(seed: Byte): ByteArray = ByteArray(ControlProtocol.NONCE_SIZE) { (it + seed).toByte() }

    @Test
    fun anAnnounceEchoingTheCurrentProbeIsFresh() {
        val ring = ControlChannel.ProbeNonceRing(2)
        val probe = nonce(1)

        ring.remember(probe)

        assertTrue(ring.isFresh(probe))
    }

    @Test
    fun anAnnounceEchoingThePreviousRoundIsStillFresh() {
        val ring = ControlChannel.ProbeNonceRing(2)
        val previous = nonce(1)
        val current = nonce(2)

        ring.remember(previous)
        ring.remember(current)

        assertTrue(ring.isFresh(previous))
        assertTrue(ring.isFresh(current))
    }

    @Test
    fun aReplayOlderThanTheWindowIsRejected() {
        val ring = ControlChannel.ProbeNonceRing(2)
        val old = nonce(1)

        ring.remember(old)
        ring.remember(nonce(2))
        ring.remember(nonce(3))

        // The ring holds rounds 2 and 3; round 1 has been pushed out.
        assertFalse(ring.isFresh(old))
        assertTrue(ring.isFresh(nonce(2)))
    }

    @Test
    fun aNonceThatWasNeverSentIsRejected() {
        val ring = ControlChannel.ProbeNonceRing(2)
        ring.remember(nonce(1))

        assertFalse(ring.isFresh(nonce(99)))
    }

    @Test
    fun wrongSizedNoncesAreNeverFresh() {
        val ring = ControlChannel.ProbeNonceRing(2)
        ring.remember(nonce(1))

        assertFalse(ring.isFresh(ByteArray(ControlProtocol.NONCE_SIZE - 1)))
        assertFalse(ring.isFresh(ByteArray(ControlProtocol.NONCE_SIZE + 1)))
    }

    @Test
    fun rememberingRequiresTheRightSize() {
        val ring = ControlChannel.ProbeNonceRing(2)

        val thrown = try {
            ring.remember(ByteArray(ControlProtocol.NONCE_SIZE - 1))
            false
        } catch (expected: IllegalArgumentException) {
            true
        }

        assertTrue(thrown)
    }
}