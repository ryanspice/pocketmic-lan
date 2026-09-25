package com.canopydigital.pocketmic

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

    @Test
    fun pcmRemainsTheDefaultAndOpusPreferenceIsPartOfTheSessionConfig() {
        val compatibleDefault = MicConfig("192.168.1.25", 49_500, "POCKETMIC-TEST", CaptureMode.VOICE, 1.0f)
        val opus = compatibleDefault.copy(codec = AudioCodec.OPUS)

        assertEquals(AudioCodec.PCM, compatibleDefault.codec)
        assertEquals(AudioCodec.OPUS, opus.codec)
        assertEquals(AudioCodec.OPUS, AudioCodec.fromWireValue(opus.codec.wireValue))
        assertEquals(AudioCodec.PCM, AudioCodec.fromWireValue("unknown"))
    }

    @Test
    fun inputGainChangesTheSamplesUsedByBothPacketCodecsAndClipsAtPcmLimits() {
        val halfGain = shortArrayOf(-20_000, 10_000, Short.MIN_VALUE, Short.MAX_VALUE)
        PcmInputGain.applyInPlace(halfGain, halfGain.size, 0.5f)
        assertEquals(listOf(-10_000, 5_000, -16_384, 16_384), halfGain.map { it.toInt() })

        val boosted = shortArrayOf(-20_000, 10_000, Short.MIN_VALUE, Short.MAX_VALUE)
        PcmInputGain.applyInPlace(boosted, boosted.size, 3.0f)
        assertEquals(listOf(-32_768, 30_000, -32_768, 32_767), boosted.map { it.toInt() })

        val unity = shortArrayOf(-12_345, 0, 23_456)
        val original = unity.copyOf()
        PcmInputGain.applyInPlace(unity, unity.size, 1.0f)
        assertEquals(original.toList(), unity.toList())
    }

    @Test
    fun opusInputContractBoundsFrameChannelsAndOutputBuffer() {
        assertTrue(OpusInputContract.isValid(pcmSize = 480, sampleCountPerChannel = 480, channels = 1, maxOutputBytes = 512))
        assertTrue(OpusInputContract.isValid(pcmSize = 960, sampleCountPerChannel = 480, channels = 2, maxOutputBytes = 512))
        assertTrue(!OpusInputContract.isValid(pcmSize = 479, sampleCountPerChannel = 480, channels = 1, maxOutputBytes = 512))
        assertTrue(!OpusInputContract.isValid(pcmSize = 480, sampleCountPerChannel = 480, channels = 2, maxOutputBytes = 512))
        assertTrue(!OpusInputContract.isValid(pcmSize = 480, sampleCountPerChannel = 480, channels = 1, maxOutputBytes = 513))
        assertTrue(!OpusInputContract.isValid(pcmSize = 480, sampleCountPerChannel = 0, channels = 1, maxOutputBytes = 512))
    }
}
