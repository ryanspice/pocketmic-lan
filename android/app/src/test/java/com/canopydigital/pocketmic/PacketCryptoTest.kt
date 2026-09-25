package com.canopydigital.pocketmic

import java.io.File
import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertSame
import org.junit.Test
import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.util.Properties
import javax.crypto.Cipher
import javax.crypto.spec.GCMParameterSpec

class PacketCryptoTest {
    @Test
    fun pcmAndOpusPacketsMatchSharedCrossLanguageFixtures() {
        val vectors = loadSharedProtocolVectors()
        val key = PacketCrypto.deriveKey(vectors.getProperty("pairing_key"))
        val sessionId = vectors.getProperty("session_id").toLong()
        val sequence = vectors.getProperty("sequence").toInt()
        val sampleRate = vectors.getProperty("sample_rate").toInt()

        assertArrayEquals(hex(vectors, "key_hex"), key.encoded)

        val pcm = hex(vectors, "v1.pcm_payload_hex")
        val pcmPacket = PacketCrypto.Encryptor(
            key, sessionId, sampleRate, pcm.size, AudioCodec.PCM,
        ).encrypt(sequence, pcm)
        assertArrayEquals(hex(vectors, "v1.datagram_hex"), pcmPacket)

        // The payload is a deterministic protocol fixture, not an audio-quality sample.
        val opus = hex(vectors, "v2.opus_payload_hex")
        val opusPacket = PacketCrypto.Encryptor(
            key, sessionId, sampleRate, pcm.size, AudioCodec.OPUS,
        ).encrypt(sequence, opus)
        assertArrayEquals(hex(vectors, "v2.datagram_hex"), opusPacket)
    }

    @Test
    fun optimizedEncryptorProducesDecryptableProtocolPacketAndReusesBuffer() {
        val sessionId = 0x0102030405060708L
        val sampleRate = 48_000
        val pcm = ByteArray(960) { index -> (index * 31).toByte() }
        val key = PacketCrypto.deriveKey("POCKETMIC-TEST")
        val encryptor = PacketCrypto.Encryptor(key, sessionId, sampleRate, pcm.size)

        val first = encryptor.encrypt(0x11223344, pcm)
        assertEquals(1_000, first.size)
        assertArrayEquals(
            byteArrayOf('P'.code.toByte(), 'M'.code.toByte(), 'I'.code.toByte(), 'C'.code.toByte()),
            first.copyOfRange(0, 4),
        )

        val header = first.copyOfRange(0, PacketCrypto.HEADER_SIZE)
        val ciphertext = first.copyOfRange(PacketCrypto.HEADER_SIZE, first.size)
        val nonce = ByteBuffer.allocate(12)
            .order(ByteOrder.BIG_ENDIAN)
            .putLong(sessionId)
            .putInt(0x11223344)
            .array()
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.DECRYPT_MODE, key, GCMParameterSpec(128, nonce))
        cipher.updateAAD(header)
        assertArrayEquals(pcm, cipher.doFinal(ciphertext))

        val second = encryptor.encrypt(0x11223345, pcm)
        assertSame(first, second)
    }

    @Test
    fun compatibilityHelperReturnsCiphertextAndTagOnly() {
        val sessionId = 7L
        val sequence = 9
        val sampleRate = 48_000
        val pcm = ByteArray(960) { it.toByte() }
        val key = PacketCrypto.deriveKey("POCKETMIC-TEST")
        val header = PacketCrypto.buildHeader(sessionId, sequence, sampleRate)

        val encrypted = PacketCrypto.encrypt(key, sessionId, sequence, header, pcm)

        assertEquals(pcm.size + PacketCrypto.GCM_TAG_BYTES, encrypted.size)
    }

    private fun loadSharedProtocolVectors(): Properties {
        var directory: File? = File(System.getProperty("user.dir"))
        while (directory != null) {
            val fixture = File(directory, "tests/fixtures/protocol-vectors.properties")
            if (fixture.isFile) {
                return Properties().apply {
                    fixture.inputStream().use { load(it) }
                }
            }
            directory = directory.parentFile
        }
        throw AssertionError("Could not locate tests/fixtures/protocol-vectors.properties from user.dir")
    }

    private fun hex(properties: Properties, key: String): ByteArray =
        properties.getProperty(key)?.chunked(2)?.map { it.toInt(16).toByte() }?.toByteArray()
            ?: throw AssertionError("Missing protocol fixture property: $key")
}
