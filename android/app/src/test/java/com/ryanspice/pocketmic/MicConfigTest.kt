package com.ryanspice.pocketmic

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The pairing key generator and the shared validation rules. The key is the only secret in the
 * whole chain, so its shape — length, alphabet, non-determinism — is pinned here, and the
 * connection rules the service and the activity both apply are pinned so they cannot drift.
 */
class MicConfigTest {
    private val alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"

    @Test
    fun generatedKeysAreTwelveAlphabetCharactersLong() {
        repeat(50) {
            val key = AppPrefs.generatePairingKey()
            assertEquals(12, key.length)
            assertTrue("key '$key' contains a character outside the alphabet", key.all { it in alphabet })
        }
    }

    @Test
    fun generatedKeysDifferAcrossCalls() {
        val first = AppPrefs.generatePairingKey()
        val second = AppPrefs.generatePairingKey()
        assertNotEquals(first, second)
    }

    @Test
    fun validateConnectionAcceptsAWellFormedConfig() {
        val config = MicConfig("192.168.1.25", 49_500, "POCKETMIC-TEST", CaptureMode.VOICE, 1.0f)
        assertNull(config.validateConnection())
    }

    @Test
    fun validateConnectionRejectsBlankHostAndShortKey() {
        val blankHost = MicConfig("", 49_500, "POCKETMIC-TEST", CaptureMode.VOICE, 1.0f)
        val shortKey = MicConfig("192.168.1.25", 49_500, "1234567", CaptureMode.VOICE, 1.0f)
        assertEquals("Enter the Windows PC IP address.", blankHost.validateConnection())
        assertTrue(shortKey.validateConnection()!!.contains("at least 8"))
    }

    @Test
    fun validateConnectionRejectsPortsOutsideTheValidRange() {
        val tooLow = MicConfig("192.168.1.25", 0, "POCKETMIC-TEST", CaptureMode.VOICE, 1.0f)
        val tooHigh = MicConfig("192.168.1.25", 65_535, "POCKETMIC-TEST", CaptureMode.VOICE, 1.0f)
        assertEquals("Port must be between 1 and 65534.", tooLow.validateConnection())
        assertEquals("Port must be between 1 and 65534.", tooHigh.validateConnection())
    }

    @Test
    fun validateConnectionAcceptsTheHighestAudioPortWithAControlPortAvailable() {
        val config = MicConfig("192.168.1.25", MAX_PORT, "POCKETMIC-TEST", CaptureMode.VOICE, 1.0f)

        assertNull(config.validateConnection())
    }
}
