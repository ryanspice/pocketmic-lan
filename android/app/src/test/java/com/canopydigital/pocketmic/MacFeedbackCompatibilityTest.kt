package com.canopydigital.pocketmic

import java.io.File
import java.util.Properties
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test

/** Existing Android control/liveness behavior against the independently encoded Mac STATS. */
class MacFeedbackCompatibilityTest {
    private val started = 1_000_000L

    @Test
    fun macReceptionOnlyStatsUseTheExistingAuthenticatedWireContract() {
        val fixture = fixture()
        val key = ControlProtocol.deriveControlKey(fixture.getProperty("pairing_key"))
        val frame = bytes(fixture.getProperty("control.stats.frame_hex"))
        assertArrayEquals(bytes(fixture.getProperty("control.key_hex")), key)
        val message = ControlProtocol.parse(frame, frame.size)
        assertNotNull(message)
        assertEquals(ControlProtocol.TYPE_STATS, message!!.type)
        assertTrue(ControlProtocol.verify(key, message))
        assertEquals(ReceiverStats(7000, 2, 3, 4, 0, 0, false), ControlProtocol.readStats(message.payload))
        // Unknown CoreAudio output remains false; reception is sufficient for link liveness.
        assertFalse(ControlProtocol.readStats(message.payload)!!.playing)
        assertFalse(ControlProtocol.verify(ControlProtocol.deriveControlKey("wrong-pairing-key"), message))
        frame[10] = (frame[10].toInt() xor 1).toByte()
        assertFalse(ControlProtocol.verify(key, ControlProtocol.parse(frame, frame.size)!!))
    }

    @Test
    fun authenticatedMacStatsKeepAnUnmodifiedAndroidPolicyAlivePastSixtyFiveSeconds() {
        val fixture = fixture()
        val frame = bytes(fixture.getProperty("control.stats.frame_hex"))
        val message = ControlProtocol.parse(frame, frame.size)!!
        val key = ControlProtocol.deriveControlKey(fixture.getProperty("pairing_key"))
        var lastStats = 0L
        for (elapsed in 0L..120_000L step 100L) {
            if (elapsed % 500L == 0L) {
                assertTrue(ControlProtocol.verify(key, message))
                assertNotNull(ControlProtocol.readStats(message.payload))
                lastStats = started + elapsed
            }
            assertEquals("elapsed=$elapsed", LinkAction.CONTINUE,
                ReconnectPolicy.evaluate(true, started + elapsed, started, lastStats, 0L))
        }
    }

    @Test
    fun stoppedFeedbackStillTimesOutAndARealReplyRecoversWithoutRelaxingTheDeadline() {
        val stoppedAt = started + 70_000L
        val reconnectAt = stoppedAt + ReconnectPolicy.STATS_TIMEOUT_MS
        assertEquals(LinkAction.CONTINUE,
            ReconnectPolicy.evaluate(true, reconnectAt - 1, started, stoppedAt, 0L))
        assertEquals(LinkAction.ENTER_RECONNECTING,
            ReconnectPolicy.evaluate(true, reconnectAt, started, stoppedAt, 0L))
        assertEquals(LinkAction.GIVE_UP, ReconnectPolicy.evaluate(false,
            reconnectAt + ReconnectPolicy.GIVE_UP_AFTER_MS, started, stoppedAt, reconnectAt))
        assertEquals(LinkAction.RESUME_STREAMING,
            ReconnectPolicy.evaluate(false, reconnectAt + 500L, started, reconnectAt + 400L, reconnectAt))
    }

    @Test
    fun wrongKeyStatsNeverStartTheLivenessClockAndTheOriginalSixtyFiveSecondFailureRemains() {
        val fixture = fixture()
        val message = ControlProtocol.parse(bytes(fixture.getProperty("control.stats.frame_hex")), 256)!!
        val wrongKey = ControlProtocol.deriveControlKey("wrong-pairing-key")
        var lastStats = 0L
        for (elapsed in 0L..65_000L step 500L) {
            if (ControlProtocol.verify(wrongKey, message)) lastStats = started + elapsed
        }
        assertEquals(0L, lastStats)
        val reconnectAt = started + ReconnectPolicy.CONNECT_GRACE_MS
        assertEquals(LinkAction.ENTER_RECONNECTING,
            ReconnectPolicy.evaluate(true, reconnectAt, started, lastStats, 0L))
        assertEquals(LinkAction.GIVE_UP, ReconnectPolicy.evaluate(false,
            reconnectAt + ReconnectPolicy.GIVE_UP_AFTER_MS, started, lastStats, reconnectAt))
    }

    private fun fixture(): Properties {
        var directory: File? = File(requireNotNull(System.getProperty("user.dir")))
        while (directory != null) {
            val file = File(directory, "tests/fixtures/protocol-vectors.properties")
            if (file.isFile) return Properties().apply { file.inputStream().use { load(it) } }
            directory = directory.parentFile
        }
        error("Shared protocol fixture not found")
    }

    private fun bytes(hex: String) = hex.chunked(2).map { it.toInt(16).toByte() }.toByteArray()
}
