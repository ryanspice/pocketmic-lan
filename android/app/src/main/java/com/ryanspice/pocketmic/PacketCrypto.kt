package com.ryanspice.pocketmic

import java.nio.ByteBuffer
import java.nio.ByteOrder
import java.security.MessageDigest
import javax.crypto.Cipher
import javax.crypto.spec.GCMParameterSpec
import javax.crypto.spec.SecretKeySpec

object PacketCrypto {
    const val HEADER_SIZE = 24
    const val HEADER_SIZE_V2 = 28
    const val GCM_TAG_BYTES = 16
    const val VERSION: Byte = 1
    const val VERSION_V2: Byte = 2
    const val FLAG_ENCRYPTED: Byte = 1
    const val FLAG_OPUS: Byte = 2

    private val MAGIC = byteArrayOf(
        'P'.code.toByte(),
        'M'.code.toByte(),
        'I'.code.toByte(),
        'C'.code.toByte(),
    )

    /**
     * The codec carried in a packet, derived from the version byte and flags.
     */
    // Use AudioCodec from MicConfig.kt for the codec type.

    fun deriveKey(pairingKey: String): SecretKeySpec {
        val digest = MessageDigest.getInstance("SHA-256")
            .digest(pairingKey.toByteArray(Charsets.UTF_8))
        return SecretKeySpec(digest, "AES")
    }

    /**
     * Builds a header for protocol v1 (PCM16, fixed 960-byte payload).
     */
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
     * Builds a header for protocol v2 (Opus codec, variable-length payload).
     *
     * v2 header layout (28 bytes):
     * ```
     *   0..3   magic "PMIC"
     *   4      version = 2
     *   5      flags: bit 0 = encrypted, bit 1 = opus
     *   6..7   header length = 28
     *   8..15  session ID
     *   16..19 sequence
     *   20..23 sample rate
     *   24..27 payload length (plaintext, before encryption)
     * ```
     */
    fun buildHeaderV2(sessionId: Long, sequence: Int, sampleRate: Int, payloadLength: Int): ByteArray =
        ByteBuffer.allocate(HEADER_SIZE_V2)
            .order(ByteOrder.BIG_ENDIAN)
            .put(MAGIC)
            .put(VERSION_V2)
            .put((FLAG_ENCRYPTED or FLAG_OPUS).toByte())
            .putShort(HEADER_SIZE_V2.toShort())
            .putLong(sessionId)
            .putInt(sequence)
            .putInt(sampleRate)
            .putInt(payloadLength)
            .array()

    /**
     * Peeks the protocol version and codec from a raw datagram without
     * touching the AEAD.
     */
    fun peekVersion(data: ByteArray): Byte {
        return if (data.size >= 5 && data[0] == MAGIC[0] && data[1] == MAGIC[1] &&
            data[2] == MAGIC[2] && data[3] == MAGIC[3]
        ) {
            data[4]
        } else {
            0
        }
    }

    /**
     * Returns the codec indicated by the version byte and flags.
     */
    fun codecFromHeader(data: ByteArray): AudioCodec {
        if (data.size < 6) return AudioCodec.PCM
        val version = data[4]
        if (version == VERSION_V2) {
            val flags = data[5].toInt()
            return if ((flags and FLAG_OPUS.toInt()) != 0) AudioCodec.OPUS else AudioCodec.PCM
        }
        return AudioCodec.PCM
    }

    /**
     * Returns the header size for the given version.
     */
    fun headerSizeForVersion(version: Byte): Int = when (version) {
        VERSION_V2 -> HEADER_SIZE_V2
        else -> HEADER_SIZE
    }

    /**
     * Reads the payload length from a v2 header. For v1 this always returns
     * [fixedPayloadSize] (the known PCM size).
     */
    fun payloadLength(data: ByteArray, fixedPayloadSize: Int): Int {
        if (data.size < 4) return 0
        val version = data[4]
        if (version == VERSION_V2 && data.size >= HEADER_SIZE_V2) {
            return readIntBigEndian(data, 24)
        }
        return fixedPayloadSize
    }

    /**
     * Reuses the cipher, nonce, header, and packet buffers for the lifetime of one stream.
     * The returned packet is overwritten by the next call and must be sent synchronously.
     *
     * @param codec  PCM or OPUS – determines the header version and payload handling.
     */
    class Encryptor(
        private val key: SecretKeySpec,
        private val sessionId: Long,
        private val sampleRate: Int,
        private val pcmBytes: Int,
        private val codec: AudioCodec = AudioCodec.PCM,
    ) {
        private val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        private val nonce = ByteArray(12)

        /**
         * Maximum packet size. For PCM this is fixed; for Opus we allocate
         * enough for the largest possible Opus frame.
         */
        val maxPacketSize: Int = when (codec) {
            AudioCodec.PCM -> HEADER_SIZE + pcmBytes + GCM_TAG_BYTES
            AudioCodec.OPUS -> HEADER_SIZE_V2 + MAX_OPUS_FRAME_BYTES + GCM_TAG_BYTES
        }

        /**
         * Reusable packet buffer, large enough for the worst-case Opus frame.
         */
        private val packet = ByteArray(maxPacketSize)

        init {
            require(pcmBytes > 0) { "PCM payload must not be empty." }
            writeLongBigEndian(nonce, 0, sessionId)
        }

        /**
         * Encrypts a frame and returns the complete datagram.
         *
         * For PCM: fixed-size 960-byte payload, protocol v1.
         * For Opus: variable-length payload, protocol v2 with payload length in header.
         *
         * @param sequence  Packet sequence number.
         * @param pcm       PCM16 bytes (for PCM mode) or Opus-encoded bytes (for Opus mode).
         * @return the complete datagram ready to send.
         */
        fun encrypt(sequence: Int, pcm: ByteArray): ByteArray {
            when (codec) {
                AudioCodec.PCM -> {
                    require(pcm.size == pcmBytes) {
                        "Expected $pcmBytes PCM bytes but received ${pcm.size}."
                    }
                    // Write v1 header into packet
                    MAGIC.copyInto(packet, destinationOffset = 0)
                    packet[4] = VERSION
                    packet[5] = FLAG_ENCRYPTED
                    writeShortBigEndian(packet, 6, HEADER_SIZE)
                    writeLongBigEndian(packet, 8, sessionId)
                    writeIntBigEndian(packet, 16, sequence)
                    writeIntBigEndian(packet, 20, sampleRate)
                    writeIntBigEndian(nonce, 8, sequence)

                    cipher.init(Cipher.ENCRYPT_MODE, key, GCMParameterSpec(128, nonce))
                    cipher.updateAAD(packet, 0, HEADER_SIZE)
                    val written = cipher.doFinal(pcm, 0, pcm.size, packet, HEADER_SIZE)
                    check(written == pcmBytes + GCM_TAG_BYTES) {
                        "AES-GCM produced $written bytes; expected ${pcmBytes + GCM_TAG_BYTES}."
                    }
                    return packet
                }

                AudioCodec.OPUS -> {
                    // Write v2 header
                    MAGIC.copyInto(packet, destinationOffset = 0)
                    packet[4] = VERSION_V2
                    packet[5] = (FLAG_ENCRYPTED or FLAG_OPUS).toByte()
                    writeShortBigEndian(packet, 6, HEADER_SIZE_V2)
                    writeLongBigEndian(packet, 8, sessionId)
                    writeIntBigEndian(packet, 16, sequence)
                    writeIntBigEndian(packet, 20, sampleRate)
                    writeIntBigEndian(packet, 24, pcm.size) // payload length
                    writeIntBigEndian(nonce, 8, sequence)

                    cipher.init(Cipher.ENCRYPT_MODE, key, GCMParameterSpec(128, nonce))
                    cipher.updateAAD(packet, 0, HEADER_SIZE_V2)
                    val written = cipher.doFinal(pcm, 0, pcm.size, packet, HEADER_SIZE_V2)
                    check(written == pcm.size + GCM_TAG_BYTES) {
                        "AES-GCM produced $written bytes; expected ${pcm.size + GCM_TAG_BYTES}."
                    }
                    return packet.copyOf(HEADER_SIZE_V2 + pcm.size + GCM_TAG_BYTES)
                }
            }
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

    /** Maximum encoded Opus frame size at 48 kHz mono. Opus cannot exceed this. */
    private const val MAX_OPUS_FRAME_BYTES = 512

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
