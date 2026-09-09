package com.ryanspice.pocketmic

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The control channel is what turns "it does not work" into an answer, so its failure modes
 * matter as much as its happy path. These tests pin the wire format, the deliberate split
 * between structural parsing and authentication, and the payload codecs the receiver mirrors
 * in C#.
 */
class ControlProtocolTest {
    private val key = ControlProtocol.deriveControlKey("POCKETMIC-TEST")
    private val otherKey = ControlProtocol.deriveControlKey("POCKETMIC-OTHER")
    private val nonce = ByteArray(ControlProtocol.NONCE_SIZE) { index -> (index * 7 + 1).toByte() }

    @Test
    fun controlChannelSitsOnePortAboveTheAudioStream() {
        assertEquals(49_501, ControlProtocol.controlPort(49_500))
    }

    @Test
    fun deriveControlKeyIsDeterministicAndSeparatedFromTheAudioKey() {
        assertArrayEquals(key, ControlProtocol.deriveControlKey("POCKETMIC-TEST"))
        assertEquals(32, key.size)
        assertFalse(key.contentEquals(otherKey))
        assertFalse(key.contentEquals(PacketCrypto.deriveKey("POCKETMIC-TEST").encoded))
    }

    @Test
    fun buildAndParseRoundTripPreservesTypeAndPayload() {
        val payload = ControlProtocol.probePayload(nonce)

        val frame = ControlProtocol.build(key, ControlProtocol.TYPE_PROBE, payload)
        val message = ControlProtocol.parse(frame, frame.size)

        assertNotNull(message)
        assertEquals(ControlProtocol.TYPE_PROBE, message!!.type)
        assertArrayEquals(payload, message.payload)
    }

    @Test
    fun everyFrameIsPaddedToTheFixedSizeSoRepliesCannotAmplify() {
        val empty = ControlProtocol.build(key, ControlProtocol.TYPE_PROBE, ByteArray(0))
        val full = ControlProtocol.build(
            key,
            ControlProtocol.TYPE_ANNOUNCE,
            ByteArray(ControlProtocol.MAX_PAYLOAD) { 0x5a },
        )

        assertEquals(ControlProtocol.FRAME_SIZE, empty.size)
        assertEquals(ControlProtocol.FRAME_SIZE, full.size)
    }

    @Test
    fun buildRejectsPayloadLargerThanTheFrameCanCarry() {
        val oversize = ByteArray(ControlProtocol.MAX_PAYLOAD + 1)

        val thrown = try {
            ControlProtocol.build(key, ControlProtocol.TYPE_CONFIG, oversize)
            false
        } catch (expected: IllegalArgumentException) {
            true
        }

        assertTrue(thrown)
    }

    @Test
    fun verifyAcceptsAFrameSignedWithTheSameKey() {
        val frame = ControlProtocol.build(key, ControlProtocol.TYPE_STATS, byteArrayOf(1, 2, 3))
        val message = ControlProtocol.parse(frame, frame.size)

        assertTrue(ControlProtocol.verify(key, message!!))
    }

    @Test
    fun verifyRejectsAFrameSignedWithADifferentPairingKey() {
        val frame = ControlProtocol.build(otherKey, ControlProtocol.TYPE_ANNOUNCE, nonce)
        val message = ControlProtocol.parse(frame, frame.size)

        assertFalse(ControlProtocol.verify(key, message!!))
    }

    @Test
    fun verifyRejectsAFrameWhosePayloadWasTampered() {
        val frame = ControlProtocol.build(key, ControlProtocol.TYPE_CONFIG, byteArrayOf(1, 2, 3, 4, 5, 6, 7))
        frame[ControlProtocol.HEADER_SIZE] = (frame[ControlProtocol.HEADER_SIZE] + 1).toByte()

        val message = ControlProtocol.parse(frame, frame.size)

        assertFalse(ControlProtocol.verify(key, message!!))
    }

    @Test
    fun verifyRejectsAFrameWhoseTagWasTampered() {
        val frame = ControlProtocol.build(key, ControlProtocol.TYPE_STATS, byteArrayOf(9))
        frame[ControlProtocol.SIGNED_SIZE] = (frame[ControlProtocol.SIGNED_SIZE] + 1).toByte()

        val message = ControlProtocol.parse(frame, frame.size)

        assertFalse(ControlProtocol.verify(key, message!!))
    }

    /**
     * The whole point of the split: a wrong pairing key must still produce a parsed message so
     * discovery can say "a receiver answered but the key is wrong" instead of reporting silence.
     */
    @Test
    fun parseSucceedsStructurallyEvenWhenTheHmacIsWrong() {
        val payload = ControlProtocol.announcePayload(nonce, 49_500, "DESKTOP")
        val frame = ControlProtocol.build(otherKey, ControlProtocol.TYPE_ANNOUNCE, payload)

        val message = ControlProtocol.parse(frame, frame.size)

        assertNotNull(message)
        assertEquals(ControlProtocol.TYPE_ANNOUNCE, message!!.type)
        assertArrayEquals(payload, message.payload)
        assertFalse(ControlProtocol.verify(key, message))
    }

    @Test
    fun parseRejectsAFrameOfTheWrongLength() {
        val frame = ControlProtocol.build(key, ControlProtocol.TYPE_PROBE, nonce)

        assertNull(ControlProtocol.parse(frame, frame.size - 1))
        assertNull(ControlProtocol.parse(frame.copyOf(ControlProtocol.FRAME_SIZE - 1), ControlProtocol.FRAME_SIZE - 1))
    }

    @Test
    fun parseRejectsForeignTrafficWithoutTheMagic() {
        val frame = ControlProtocol.build(key, ControlProtocol.TYPE_PROBE, nonce)
        frame[0] = 'X'.code.toByte()

        assertNull(ControlProtocol.parse(frame, frame.size))
    }

    @Test
    fun parseRejectsAnUnknownProtocolVersion() {
        val frame = ControlProtocol.build(key, ControlProtocol.TYPE_PROBE, nonce)
        frame[5] = 2

        assertNull(ControlProtocol.parse(frame, frame.size))
    }

    @Test
    fun parseRejectsADeclaredPayloadLongerThanTheFrame() {
        val frame = ControlProtocol.build(key, ControlProtocol.TYPE_PROBE, nonce)
        val overstated = ControlProtocol.MAX_PAYLOAD + 1
        frame[8] = ((overstated ushr 8) and 0xff).toByte()
        frame[9] = (overstated and 0xff).toByte()

        assertNull(ControlProtocol.parse(frame, frame.size))
    }

    @Test
    fun probePayloadRejectsANonceOfTheWrongSize() {
        val thrown = try {
            ControlProtocol.probePayload(ByteArray(ControlProtocol.NONCE_SIZE - 1))
            false
        } catch (expected: IllegalArgumentException) {
            true
        }

        assertTrue(thrown)
    }

    @Test
    fun announcePayloadRoundTripsNoncePortAndHostName() {
        val payload = ControlProtocol.announcePayload(nonce, 49_500, "RYAN-DESKTOP")

        val announce = ControlProtocol.readAnnounce(payload)

        assertNotNull(announce)
        assertArrayEquals(nonce, announce!!.nonce)
        assertEquals(49_500, announce.audioPort)
        assertEquals("RYAN-DESKTOP", announce.hostName)
    }

    @Test
    fun announcePayloadTruncatesAnOverlongHostNameToSixtyThreeCharacters() {
        val longName = "H".repeat(200)

        val announce = ControlProtocol.readAnnounce(
            ControlProtocol.announcePayload(nonce, 49_500, longName),
        )

        assertEquals(63, announce!!.hostName.length)
    }

    @Test
    fun announceRoundTripsTheProtocolVersionsFrontEndAndBuild() {
        val payload = ControlProtocol.announcePayload(
            nonce = nonce,
            audioPort = 49_500,
            hostName = "RYAN-DESKTOP",
            audioProtocolVersion = 7,
            frontEnd = ControlProtocol.FRONT_END_CLASSIC,
            buildVersion = "0.1.2",
        )

        val announce = ControlProtocol.readAnnounce(payload)

        assertNotNull(announce)
        assertEquals(49_500, announce!!.audioPort)
        assertEquals("RYAN-DESKTOP", announce.hostName)
        assertTrue(announce.hasVersions)
        assertEquals(7, announce.audioProtocolVersion)
        assertEquals(ControlProtocol.VERSION.toInt(), announce.controlProtocolVersion)
        assertEquals("classic", announce.frontEndName)
        assertEquals("0.1.2", announce.buildVersion)
        assertFalse(announce.audioProtocolMatches)
    }

    /**
     * Byte offsets asserted literally rather than through the decoder, because the receiver
     * implements this layout independently in C# and a shared encoder would let both drift
     * together without a single test noticing.
     */
    @Test
    fun theVersionTrailerSitsImmediatelyAfterTheHostName() {
        val payload = ControlProtocol.announcePayload(
            nonce = nonce,
            audioPort = 49_500,
            hostName = "PC",
            audioProtocolVersion = 1,
            frontEnd = ControlProtocol.FRONT_END_MODERN,
            buildVersion = "0.1.3",
        )

        val trailer = ControlProtocol.NONCE_SIZE + 3 + 2
        assertEquals(2, payload[ControlProtocol.NONCE_SIZE + 2].toInt() and 0xff)
        assertEquals(1, payload[trailer].toInt() and 0xff)
        assertEquals(ControlProtocol.VERSION.toInt(), payload[trailer + 1].toInt() and 0xff)
        assertEquals(2, payload[trailer + 2].toInt() and 0xff)
        assertEquals(5, payload[trailer + 3].toInt() and 0xff)
        assertEquals("0.1.3", String(payload, trailer + 4, 5, Charsets.UTF_8))
        assertEquals(trailer + 4 + 5, payload.size)
    }

    /**
     * The trailer sits after the variable-length name, so a receiver whose machine name is long
     * and multi-byte must not push it out of the frame — that would turn version negotiation off
     * for exactly the machines whose names are unusual.
     */
    @Test
    fun theVersionTrailerSurvivesTheLongestPossibleHostName() {
        val widest = "中".repeat(200)

        val payload = ControlProtocol.announcePayload(
            nonce = nonce,
            audioPort = 49_500,
            hostName = widest,
            audioProtocolVersion = PacketCrypto.VERSION,
            frontEnd = ControlProtocol.FRONT_END_MODERN,
            buildVersion = "0.1.3",
        )
        val frame = ControlProtocol.build(key, ControlProtocol.TYPE_ANNOUNCE, payload)
        val announce = ControlProtocol.readAnnounce(payload)

        assertTrue(payload.size <= ControlProtocol.MAX_PAYLOAD)
        assertEquals(ControlProtocol.FRAME_SIZE, frame.size)
        assertTrue(announce!!.hasVersions)
        assertEquals("0.1.3", announce.buildVersion)
        assertTrue(announce.audioProtocolMatches)
    }

    /**
     * A receiver built before the trailer existed announces without it. Claiming a version for
     * that receiver would be worse than claiming none: the phone would then compare against a
     * number nobody ever sent.
     */
    @Test
    fun anAnnounceWithoutAVersionTrailerStillDecodesAndClaimsNoVersions() {
        val full = ControlProtocol.announcePayload(nonce, 49_500, "OLD-PC")
        val legacy = full.copyOf(ControlProtocol.NONCE_SIZE + 3 + 6)

        val announce = ControlProtocol.readAnnounce(legacy)

        assertNotNull(announce)
        assertEquals(49_500, announce!!.audioPort)
        assertEquals("OLD-PC", announce.hostName)
        assertFalse(announce.hasVersions)
        assertEquals(0, announce.audioProtocolVersion)
        assertEquals("", announce.buildVersion)

        // Unknown is treated as compatible: a receiver that never announced a version has, by
        // definition, the only version that existed when it was built.
        assertTrue(announce.audioProtocolMatches)
    }

    /**
     * The same discipline `AudioPipeline.TryDecrypt` applies on the receiver: refuse the frame,
     * but keep enough to say why. Without this a future receiver is indistinguishable from no
     * receiver at all, which is the single most misleading thing discovery can report.
     */
    @Test
    fun peekingTheVersionWorksOnAFrameParseRefuses() {
        val frame = ControlProtocol.build(key, ControlProtocol.TYPE_ANNOUNCE, nonce)
        frame[5] = 9

        assertNull(ControlProtocol.parse(frame, frame.size))
        assertEquals(9, ControlProtocol.peekVersion(frame, frame.size))
    }

    @Test
    fun peekingTheVersionIgnoresTrafficThatIsNotOurs() {
        val frame = ControlProtocol.build(key, ControlProtocol.TYPE_PROBE, nonce)
        frame[0] = 'X'.code.toByte()

        assertEquals(-1, ControlProtocol.peekVersion(frame, frame.size))
        assertEquals(-1, ControlProtocol.peekVersion(ByteArray(8), 8))
    }

    /**
     * A payload that ends inside the fixed header or inside the declared host name is malformed
     * and rejected. One that ends inside the version trailer is not: that is precisely what an
     * announce from a receiver predating the trailer looks like, and rejecting it would break
     * discovery against every older build.
     */
    @Test
    fun readAnnounceRejectsAPayloadTruncatedInsideTheNameButToleratesAMissingTrailer() {
        val payload = ControlProtocol.announcePayload(nonce, 49_500, "PC")

        assertNull(ControlProtocol.readAnnounce(payload.copyOf(ControlProtocol.NONCE_SIZE + 2)))
        assertNull(ControlProtocol.readAnnounce(payload.copyOf(ControlProtocol.NONCE_SIZE + 3 + 1)))

        val withoutTrailer = ControlProtocol.readAnnounce(payload.copyOf(payload.size - 1))
        assertNotNull(withoutTrailer)
        assertFalse(withoutTrailer!!.hasVersions)
        assertEquals("PC", withoutTrailer.hostName)
    }

    /**
     * The character cap and the byte cap are separate limits: 63 characters is the ceiling even
     * for a fully ASCII name, and the byte cap protects the frame from multi-byte names. The
     * wire contract pins both, so the constant the encoder uses is asserted here.
     */
    @Test
    fun theAnnounceNameHasTwoExplicitCapsThatCannotDrift() {
        assertEquals(63, ControlProtocol.ANNOUNCE_MAX_NAME_CHARS)
        assertEquals(96, ControlProtocol.ANNOUNCE_MAX_NAME_BYTES)
        assertTrue(ControlProtocol.ANNOUNCE_MAX_NAME_CHARS < ControlProtocol.ANNOUNCE_MAX_NAME_BYTES)
    }

    /**
     * The no-split guarantee: truncation must never cut a multi-byte UTF-8 sequence in half,
     * because a half-written sequence decodes to a replacement character — corruption that
     * reads as a bug. The byte cap here (5) is deliberately tight so the drop-last-character
     * loop is exercised.
     */
    @Test
    fun encodeCappedNeverSplitsAMultiByteCharacter() {
        val wide = "中" // U+4E2D, three UTF-8 bytes

        // "中中" is 6 bytes; the 5-byte cap must drop a whole character, not half of one.
        val capped = ControlProtocol.encodeCapped(wide + wide, maxChars = 63, maxBytes = 5)

        assertEquals(3, capped.size)
        assertEquals("中", String(capped, Charsets.UTF_8))
    }

    @Test
    fun encodeCappedHonoursTheCharacterCapBeforeTheByteCap() {
        val ascii = "A".repeat(100)

        val capped = ControlProtocol.encodeCapped(ascii, maxChars = 63, maxBytes = 96)

        assertEquals(63, String(capped, Charsets.UTF_8).length)
    }

    @Test
    fun readStatsDecodesTheFortyFiveByteReceiverReport() {
        val payload = statsPayload(
            packets = 1_234_567L,
            lost = 89L,
            late = 7L,
            rejected = 3L,
            trimmed = 2L,
            bufferMillis = 137,
            playing = true,
        )

        val stats = ControlProtocol.readStats(payload)

        assertNotNull(stats)
        assertEquals(1_234_567L, stats!!.packets)
        assertEquals(89L, stats.lost)
        assertEquals(7L, stats.late)
        assertEquals(3L, stats.rejected)
        assertEquals(2L, stats.trimmed)
        assertEquals(137, stats.bufferMillis)
        assertTrue(stats.playing)
    }

    @Test
    fun readStatsDecodesCountersAboveTheSignedThirtyTwoBitRange() {
        val payload = statsPayload(
            packets = 5_000_000_000L,
            lost = 0L,
            late = 0L,
            rejected = 0L,
            trimmed = 0L,
            bufferMillis = 0,
            playing = false,
        )

        val stats = ControlProtocol.readStats(payload)

        assertEquals(5_000_000_000L, stats!!.packets)
        assertFalse(stats.playing)
    }

    @Test
    fun readStatsRejectsAShortPayload() {
        assertNull(ControlProtocol.readStats(ByteArray(44)))
    }

    @Test
    fun configPayloadEncodesEachSettingAsASingleQuantisedByte() {
        val payload = ControlProtocol.configPayload(
            DspSettings(
                enabled = true,
                highPassHz = 85,
                gate = 60,
                compressor = 55,
                presenceDb = 3.5f,
                makeup = 70,
                noiseReduction = 40,
            ),
        )

        assertEquals(7, payload.size)
        assertEquals(1, payload[0].toInt())
        assertEquals(21, payload[1].toInt() and 0xff)
        assertEquals(60, payload[2].toInt() and 0xff)
        assertEquals(55, payload[3].toInt() and 0xff)
        assertEquals(35, payload[4].toInt() and 0xff)
        assertEquals(70, payload[5].toInt() and 0xff)
        assertEquals(40, payload[6].toInt() and 0xff)
    }

    @Test
    fun configPayloadClampsOutOfRangeSettingsIntoTheByteFields() {
        val payload = ControlProtocol.configPayload(
            DspSettings(
                enabled = false,
                highPassHz = 4_000,
                gate = 500,
                compressor = -20,
                presenceDb = 40f,
                makeup = 101,
                noiseReduction = -1,
            ),
        )

        assertEquals(0, payload[0].toInt())
        assertEquals(255, payload[1].toInt() and 0xff)
        assertEquals(100, payload[2].toInt() and 0xff)
        assertEquals(0, payload[3].toInt() and 0xff)
        assertEquals(200, payload[4].toInt() and 0xff)
        assertEquals(100, payload[5].toInt() and 0xff)
        assertEquals(0, payload[6].toInt() and 0xff)
    }

    @Test
    fun aConfigFrameSurvivesBuildParseAndVerifyUnchanged() {
        val dsp = DspSettings(enabled = true, highPassHz = 120, gate = 45)
        val payload = ControlProtocol.configPayload(dsp)

        val frame = ControlProtocol.build(key, ControlProtocol.TYPE_CONFIG, payload)
        val message = ControlProtocol.parse(frame, frame.size)

        assertEquals(ControlProtocol.TYPE_CONFIG, message!!.type)
        assertTrue(ControlProtocol.verify(key, message))
        assertArrayEquals(payload, message.payload)
    }

    /** Mirrors ControlProtocol.StatsPayload on the receiver, big endian throughout. */
    private fun statsPayload(
        packets: Long,
        lost: Long,
        late: Long,
        rejected: Long,
        trimmed: Long,
        bufferMillis: Int,
        playing: Boolean,
    ): ByteArray {
        val payload = ByteArray(45)
        fun putLong(offset: Int, value: Long) {
            for (index in 0 until 8) {
                payload[offset + index] = ((value ushr ((7 - index) * 8)) and 0xff).toByte()
            }
        }
        putLong(0, packets)
        putLong(8, lost)
        putLong(16, late)
        putLong(24, rejected)
        putLong(32, trimmed)
        for (index in 0 until 4) {
            payload[40 + index] = ((bufferMillis ushr ((3 - index) * 8)) and 0xff).toByte()
        }
        payload[44] = if (playing) 1 else 0
        return payload
    }
}
