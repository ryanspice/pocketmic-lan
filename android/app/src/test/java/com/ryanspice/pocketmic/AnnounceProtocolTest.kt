package com.ryanspice.pocketmic

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * Announce data class properties: audioProtocolMatches, frontEndName, and the
 * graceful degradation when optional trailer fields are absent.
 */
class AnnounceProtocolTest {
    private val nonce = ByteArray(ControlProtocol.NONCE_SIZE) { index -> (index * 7 + 1).toByte() }

    @Test
    fun audioProtocolMatchesTrueWhenVersionsAreEqual() {
        val announce = Announce(
            nonce = nonce,
            audioPort = 49_500,
            hostName = "TEST",
            hasVersions = true,
            audioProtocolVersion = PacketCrypto.VERSION.toInt() and 0xff,
            controlProtocolVersion = ControlProtocol.VERSION.toInt(),
        )

        assertTrue(announce.audioProtocolMatches)
    }

    @Test
    fun audioProtocolMatchesFalseWhenVersionsDiffer() {
        val announce = Announce(
            nonce = nonce,
            audioPort = 49_500,
            hostName = "TEST",
            hasVersions = true,
            audioProtocolVersion = 99,
            controlProtocolVersion = ControlProtocol.VERSION.toInt(),
        )

        assertFalse(announce.audioProtocolMatches)
    }

    @Test
    fun announceWithEmptyHostNameStillDecodes() {
        val payload = ControlProtocol.announcePayload(nonce, 49_500, "")
        val announce = ControlProtocol.readAnnounce(payload)

        assertNotNull(announce)
        assertEquals(49_500, announce!!.audioPort)
        assertEquals("", announce.hostName)
    }

    @Test
    fun announceWithEmptyBuildVersionStillDecodes() {
        val payload = ControlProtocol.announcePayload(
            nonce = nonce,
            audioPort = 49_500,
            hostName = "TEST",
            audioProtocolVersion = 1,
            frontEnd = ControlProtocol.FRONT_END_MODERN,
            buildVersion = "",
        )
        val announce = ControlProtocol.readAnnounce(payload)

        assertNotNull(announce)
        assertTrue(announce!!.hasVersions)
        assertEquals("", announce.buildVersion)
    }
}
