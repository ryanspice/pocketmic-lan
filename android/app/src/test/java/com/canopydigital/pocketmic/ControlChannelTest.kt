package com.canopydigital.pocketmic

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * The probe-nonce freshness gate: an announce is only believed when it echoes a probe this
 * phone actually sent recently. The HMAC proves the sender holds the key; the nonce proves the
 * reply belongs to this conversation and this round — together they close the replay hole where
 * a recorded announce (same key) was accepted as fresh discovery.
 */
class ControlChannelTest {
    @Test
    fun configurationKeepsTheAdoptedTargetAfterDiscoveryStops() {
        val target = ControlChannel.ConfigTarget("192.168.1.20", 50_000)

        ControlChannel.stopProbing()

        assertEquals(PeerState.NotFound, ControlChannel.peer.value)
        assertEquals(target, ControlChannel.resolveConfigTarget(target.host, target.audioPort,
            ControlChannel.peer.value))
    }

    @Test
    fun configurationUsesTheRecoveredPortInsteadOfAnOldDiscoveryResult() {
        val previous = PeerState.Found("192.168.1.20", "PC", DEFAULT_PORT, true)

        assertEquals(ControlChannel.ConfigTarget("192.168.1.20", 50_000),
            ControlChannel.resolveConfigTarget(previous.address, 50_000, previous))
    }

    @Test
    fun configurationFallbackUsesTheAuthenticatedAdvertisedPort() {
        val discovered = PeerState.Found("192.168.1.30", "PC", 51_000, true)

        assertEquals(ControlChannel.ConfigTarget(discovered.address, discovered.audioPort),
            ControlChannel.resolveConfigTarget("", DEFAULT_PORT, discovered))
        assertNull(ControlChannel.resolveConfigTarget("", DEFAULT_PORT, discovered.copy(keyMatches = false)))
        assertNull(ControlChannel.resolveConfigTarget("", DEFAULT_PORT, PeerState.NotFound))
        assertNull(ControlChannel.resolveConfigTarget("", DEFAULT_PORT, discovered.copy(audioPort = MAX_PORT + 1)))
    }

    @Test
    fun anInvalidAdoptedPortDoesNotRedirectConfigurationToAnotherReceiver() {
        val discovered = PeerState.Found("192.168.1.30", "Other PC", DEFAULT_PORT, true)

        assertNull(ControlChannel.resolveConfigTarget("192.168.1.20", MAX_PORT + 1, discovered))
    }

    @Test
    fun stoppingDiscoveryAfterRecoveryKeepsDeliveryConfirmationUntilItExpires() {
        val stats = ReceiverStats(20, 0, 0, 0, 0, 60, true)
        ControlChannel.recordDeliveryStats(stats, 10_000L)

        ControlChannel.stopProbing()

        assertEquals(stats, ControlChannel.stats.value)
        assertEquals(LinkAction.CONTINUE, ReconnectPolicy.evaluate(true, 10_100L,
            1_000L, ControlChannel.lastStatsAtMillis.value, 0L))
        assertEquals(LinkAction.ENTER_RECONNECTING, ReconnectPolicy.evaluate(true, 12_001L,
            1_000L, ControlChannel.lastStatsAtMillis.value, 0L))
    }

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
    fun clearingDiscoveryInvalidatesEveryPreviouslySentNonce() {
        val ring = ControlChannel.ProbeNonceRing(2)
        val previous = nonce(1)
        ring.remember(previous)
        ring.remember(nonce(2))

        ring.clear()

        assertFalse(ring.isFresh(previous))
        assertFalse(ring.isFresh(nonce(2)))
        ring.remember(nonce(3))
        assertTrue(ring.isFresh(nonce(3)))
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
