package com.ryanspice.pocketmic

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertEquals
import org.junit.Assert.assertSame
import org.junit.Test
import java.nio.ByteBuffer
import java.nio.ByteOrder
import javax.crypto.Cipher
import javax.crypto.spec.GCMParameterSpec

class PacketCryptoTest {
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
}
