package com.ryanspice.pocketmic

import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.security.MessageDigest
import javax.crypto.Cipher
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

object PacketCrypto {
    const val HEADER_SIZE = 24
    const val GCM_TAG_BYTES = 16
    const val VERSION: Byte = 1
    const val FLAG_ENCRYPTED: Byte = 1

    private val MAGIC = byteArrayOf(
        'P'.code.toByte(),
        'M'.code.toByte(),
        'I'.code.toByte(),
        'C'.code.toByte(),
    )

    fun deriveKey(pairingKey: String): SecretKeySpec {
        val digest = MessageDigest.getInstance("SHA-256")
            .digest(pairingKey.toByteArray(Charsets.UTF_8))
        return SecretKeySpec(digest, "AES")
    }

    fun buildHeader(sessionId: Long, sequence: Int, sampleRate: Int): ByteArray =
        ByteBuffer.allocate(HEADER_SIZE)
            .order(ByteOrder.BIG_ENDIAN)
            .put(MAGIC)
            .put(VERSION)
            .put(FLAG_ENCRYPTED)
            .putShort(HEADER_SIZE.toShort())
            .putLong(sessionId)
            .putInt(sequence)
            .putInt(sampleRate)
            .array()

    /**
     * Reuses the cipher, nonce, header, and packet buffers for the lifetime of one stream.
     * The returned packet is overwritten by the next call and must be sent synchronously.
     */
    class Encryptor(
        private val key: SecretKeySpec,
        private val sessionId: Long,
        private val sampleRate: Int,
        private val pcmBytes: Int,
    ) {
        private val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        private val nonce = ByteArray(12)
        private val packet = ByteArray(HEADER_SIZE + pcmBytes + GCM_TAG_BYTES)

        init {
            require(pcmBytes > 0) { "PCM payload must not be empty." }

            MAGIC.copyInto(packet, destinationOffset = 0)
            packet[4] = VERSION
            packet[5] = FLAG_ENCRYPTED
            writeShortBigEndian(packet, 6, HEADER_SIZE)
            writeLongBigEndian(packet, 8, sessionId)
            writeIntBigEndian(packet, 20, sampleRate)

            writeLongBigEndian(nonce, 0, sessionId)
        }

        fun encrypt(sequence: Int, pcm: ByteArray): ByteArray {
            require(pcm.size == pcmBytes) {
                "Expected $pcmBytes PCM bytes but received ${pcm.size}."
            }

            writeIntBigEndian(packet, 16, sequence)
            writeIntBigEndian(nonce, 8, sequence)

            cipher.init(Cipher.ENCRYPT_MODE, key, GCMParameterSpec(128, nonce))
            cipher.updateAAD(packet, 0, HEADER_SIZE)
            val written = cipher.doFinal(pcm, 0, pcm.size, packet, HEADER_SIZE)
            check(written == pcmBytes + GCM_TAG_BYTES) {
                "AES-GCM produced $written bytes; expected ${pcmBytes + GCM_TAG_BYTES}."
            }

            return packet
        }
    }

    /** Compatibility helper used by protocol tests and small one-off callers. */
    fun encrypt(
        key: SecretKeySpec,
        sessionId: Long,
        sequence: Int,
        header: ByteArray,
        pcm: ByteArray,
    ): ByteArray {
        require(header.contentEquals(buildHeader(sessionId, sequence, readIntBigEndian(header, 20)))) {
            "Header does not match the supplied session and sequence."
        }

        val encryptor = Encryptor(key, sessionId, readIntBigEndian(header, 20), pcm.size)
        return encryptor.encrypt(sequence, pcm).copyOfRange(HEADER_SIZE, HEADER_SIZE + pcm.size + GCM_TAG_BYTES)
    }

    private fun writeShortBigEndian(target: ByteArray, offset: Int, value: Int) {
        target[offset] = ((value ushr 8) and 0xff).toByte()
        target[offset + 1] = (value and 0xff).toByte()
    }

    private fun writeIntBigEndian(target: ByteArray, offset: Int, value: Int) {
        target[offset] = ((value ushr 24) and 0xff).toByte()
        target[offset + 1] = ((value ushr 16) and 0xff).toByte()
        target[offset + 2] = ((value ushr 8) and 0xff).toByte()
        target[offset + 3] = (value and 0xff).toByte()
    }

    private fun writeLongBigEndian(target: ByteArray, offset: Int, value: Long) {
        for (index in 0 until Long.SIZE_BYTES) {
            val shift = (Long.SIZE_BYTES - 1 - index) * Byte.SIZE_BITS
            target[offset + index] = ((value ushr shift) and 0xff).toByte()
        }
    }

    private fun readIntBigEndian(source: ByteArray, offset: Int): Int {
        require(source.size >= offset + Int.SIZE_BYTES) { "Header is too short." }
        return ((source[offset].toInt() and 0xff) shl 24) or
            ((source[offset + 1].toInt() and 0xff) shl 16) or
            ((source[offset + 2].toInt() and 0xff) shl 8) or
            (source[offset + 3].toInt() and 0xff)
    }
}
