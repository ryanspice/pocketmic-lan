package com.ryanspice.pocketmic

import java.security.MessageDigest
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec

/**
 * PocketMic control channel, v1.
 *
 * Separate from the audio stream and carried on UDP `audioPort + 1`. It answers the three
 * questions the audio path structurally cannot: where is the receiver, does it share our
 * pairing key, and is anything actually arriving.
 *
 * Every datagram is a fixed [FRAME_SIZE] bytes, zero padded. Fixed size is deliberate: a
 * reply can never be larger than the request that triggered it, so the discovery responder
 * cannot be used as a traffic amplifier.
 *
 * ```
 * 0   5   magic "PMCTL"
 * 5   1   version
 * 6   1   message type
 * 7   1   reserved (0)
 * 8   2   payload length, big endian
 * 10  214 payload area, zero padded
 * 224 32  HMAC-SHA256 over bytes 0..223
 * ```
 */
object ControlProtocol {
    const val FRAME_SIZE = 256
    const val HMAC_SIZE = 32
    const val HEADER_SIZE = 10
    const val SIGNED_SIZE = FRAME_SIZE - HMAC_SIZE
    const val MAX_PAYLOAD = SIGNED_SIZE - HEADER_SIZE
    const val NONCE_SIZE = 16
    const val VERSION: Byte = 1

    /**
     * Hard cap on the UTF-8 length of the host name inside an announce. The name is variable
     * length and the version trailer follows it, so without a ceiling here a long multi-byte
     * machine name could push the trailer out of the frame — which would silently turn version
     * negotiation off for exactly the machines whose names are unusual.
     */
    const val ANNOUNCE_MAX_NAME_BYTES = 96

    /** The name is also capped by characters, not only bytes; see [encodeCapped]. */
    const val ANNOUNCE_MAX_NAME_CHARS = 63

    /** Cap on the build-version string. "0.1.2-preview" and its like fit comfortably. */
    const val ANNOUNCE_MAX_BUILD_BYTES = 16

    /** Receiver front ends, mirroring ReceiverFrontEnd in the C# receiver. */
    const val FRONT_END_UNKNOWN: Byte = 0
    const val FRONT_END_CLASSIC: Byte = 1
    const val FRONT_END_MODERN: Byte = 2

    const val TYPE_PROBE: Byte = 1
    const val TYPE_ANNOUNCE: Byte = 2
    const val TYPE_STATS: Byte = 3
    const val TYPE_CONFIG: Byte = 4

    private val MAGIC = byteArrayOf(
        'P'.code.toByte(), 'M'.code.toByte(), 'C'.code.toByte(),
        'T'.code.toByte(), 'L'.code.toByte(),
    )

    /** The control channel always sits one port above the audio stream. */
    fun controlPort(audioPort: Int): Int = audioPort + 1

    /**
     * Domain separated from the audio key. Reusing one key for AES-GCM and HMAC would be
     * sloppy key hygiene even though the primitives differ, and the derivation is one hash.
     */
    fun deriveControlKey(pairingKey: String): ByteArray {
        val master = MessageDigest.getInstance("SHA-256")
            .digest(pairingKey.toByteArray(Charsets.UTF_8))
        return mac(master, "pocketmic-control-v1".toByteArray(Charsets.US_ASCII))
    }

    private fun mac(key: ByteArray, data: ByteArray): ByteArray =
        Mac.getInstance("HmacSHA256").run {
            init(SecretKeySpec(key, "HmacSHA256"))
            doFinal(data)
        }

    fun build(controlKey: ByteArray, type: Byte, payload: ByteArray): ByteArray {
        require(payload.size <= MAX_PAYLOAD) {
            "Control payload of ${payload.size} exceeds $MAX_PAYLOAD bytes."
        }
        val frame = ByteArray(FRAME_SIZE)
        MAGIC.copyInto(frame, 0)
        frame[5] = VERSION
        frame[6] = type
        frame[7] = 0
        frame[8] = ((payload.size ushr 8) and 0xff).toByte()
        frame[9] = (payload.size and 0xff).toByte()
        payload.copyInto(frame, HEADER_SIZE)

        val tag = Mac.getInstance("HmacSHA256").run {
            init(SecretKeySpec(controlKey, "HmacSHA256"))
            update(frame, 0, SIGNED_SIZE)
            doFinal()
        }
        tag.copyInto(frame, SIGNED_SIZE)
        return frame
    }

    /**
     * Structural parse that deliberately does *not* verify the HMAC. Discovery must be able
     * to tell "a receiver replied but our key is wrong" apart from "nothing replied" — those
     * are completely different problems for the user, and collapsing them into silence is
     * exactly the failure mode this channel exists to eliminate.
     */
    fun parse(frame: ByteArray, length: Int): ControlMessage? {
        if (length != FRAME_SIZE) return null
        for (index in MAGIC.indices) {
            if (frame[index] != MAGIC[index]) return null
        }
        if (frame[5] != VERSION) return null

        val payloadLength = ((frame[8].toInt() and 0xff) shl 8) or (frame[9].toInt() and 0xff)
        if (payloadLength > MAX_PAYLOAD) return null

        return ControlMessage(
            type = frame[6],
            payload = frame.copyOfRange(HEADER_SIZE, HEADER_SIZE + payloadLength),
            frame = frame.copyOf(length),
        )
    }

    /**
     * Reads the framing version out of anything carrying the control magic, without parsing or
     * authenticating the rest. Returns -1 when the datagram is not a PocketMic control frame.
     *
     * [parse] refuses a frame whose version is not ours, which is correct — the phone must not
     * act on a message it cannot fully understand. But refusing it silently makes a future v2
     * receiver indistinguishable from no receiver at all, which is the exact failure this
     * channel exists to eliminate. Same discipline as `AudioPipeline.TryDecrypt` on the receiver:
     * reject before doing any work, but keep enough to say why.
     */
    fun peekVersion(frame: ByteArray, length: Int): Int {
        if (length != FRAME_SIZE || frame.size < FRAME_SIZE) return -1
        for (index in MAGIC.indices) {
            if (frame[index] != MAGIC[index]) return -1
        }
        return frame[5].toInt() and 0xff
    }

    fun verify(controlKey: ByteArray, message: ControlMessage): Boolean {
        val expected = Mac.getInstance("HmacSHA256").run {
            init(SecretKeySpec(controlKey, "HmacSHA256"))
            update(message.frame, 0, SIGNED_SIZE)
            doFinal()
        }
        var difference = 0
        for (index in 0 until HMAC_SIZE) {
            difference = difference or (expected[index].toInt() xor message.frame[SIGNED_SIZE + index].toInt())
        }
        return difference == 0
    }

    // ---- payload codecs -------------------------------------------------------------

    fun probePayload(nonce: ByteArray): ByteArray {
        require(nonce.size == NONCE_SIZE) { "Probe nonce must be $NONCE_SIZE bytes." }
        return nonce.copyOf()
    }

    /**
     * The discovery reply. Mirrors ControlProtocol.AnnouncePayload on the receiver byte for byte.
     *
     * ```
     * 0    16  probe nonce, echoed back
     * 16   2   audio port, big endian
     * 18   1   host name length in bytes
     * 19   n   host name, UTF-8
     * 19+n 1   audio protocol version
     * 20+n 1   control protocol version
     * 21+n 1   receiver front end
     * 22+n 1   build version length in bytes
     * 23+n m   build version, UTF-8
     * ```
     *
     * The version trailer sits after the variable-length name rather than at a fixed offset so
     * that a phone built before it existed still reads the nonce, port and name exactly as it
     * always did and simply ignores what follows. Discovery keeps working across the upgrade;
     * only the version reporting is missing on the old build.
     */
    fun announcePayload(
        nonce: ByteArray,
        audioPort: Int,
        hostName: String,
        audioProtocolVersion: Byte = PacketCrypto.VERSION,
        frontEnd: Byte = FRONT_END_UNKNOWN,
        buildVersion: String = "",
    ): ByteArray {
        val name = encodeCapped(hostName, ANNOUNCE_MAX_NAME_CHARS, ANNOUNCE_MAX_NAME_BYTES)
        val build = encodeCapped(buildVersion, ANNOUNCE_MAX_BUILD_BYTES, ANNOUNCE_MAX_BUILD_BYTES)

        val payload = ByteArray(NONCE_SIZE + 2 + 1 + name.size + 3 + 1 + build.size)
        nonce.copyInto(payload, 0)
        payload[NONCE_SIZE] = ((audioPort ushr 8) and 0xff).toByte()
        payload[NONCE_SIZE + 1] = (audioPort and 0xff).toByte()
        payload[NONCE_SIZE + 2] = name.size.toByte()
        name.copyInto(payload, NONCE_SIZE + 3)

        val trailer = NONCE_SIZE + 3 + name.size
        payload[trailer] = audioProtocolVersion
        payload[trailer + 1] = VERSION
        payload[trailer + 2] = frontEnd
        payload[trailer + 3] = build.size.toByte()
        build.copyInto(payload, trailer + 4)
        return payload
    }

    /**
     * UTF-8 bytes for a string clipped to [maxChars] characters and then to [maxBytes] bytes,
     * dropping whole characters rather than splitting one. A half-written multi-byte sequence
     * decodes to a replacement character, which reads as corruption rather than as truncation.
     *
     * Internal rather than private so the no-split guarantee is testable.
     */
    internal fun encodeCapped(value: String, maxChars: Int, maxBytes: Int): ByteArray {
        var clipped = value.take(maxChars)
        var bytes = clipped.toByteArray(Charsets.UTF_8)
        while (bytes.size > maxBytes && clipped.isNotEmpty()) {
            clipped = clipped.dropLast(1)
            bytes = clipped.toByteArray(Charsets.UTF_8)
        }
        return bytes
    }

    /**
     * Decodes an announce from either side of the version trailer's introduction. An older
     * receiver's announce yields [Announce.hasVersions] false rather than an invented version
     * number: not knowing and matching are different answers, and only one of them is honest.
     */
    fun readAnnounce(payload: ByteArray): Announce? {
        if (payload.size < NONCE_SIZE + 3) return null
        val port = ((payload[NONCE_SIZE].toInt() and 0xff) shl 8) or
            (payload[NONCE_SIZE + 1].toInt() and 0xff)
        val nameLength = payload[NONCE_SIZE + 2].toInt() and 0xff
        if (payload.size < NONCE_SIZE + 3 + nameLength) return null

        val nonce = payload.copyOfRange(0, NONCE_SIZE)
        val hostName = String(payload, NONCE_SIZE + 3, nameLength, Charsets.UTF_8)
        val trailer = NONCE_SIZE + 3 + nameLength

        if (payload.size < trailer + 4) {
            return Announce(nonce = nonce, audioPort = port, hostName = hostName)
        }

        val buildLength = payload[trailer + 3].toInt() and 0xff
        val build = if (payload.size >= trailer + 4 + buildLength) {
            String(payload, trailer + 4, buildLength, Charsets.UTF_8)
        } else {
            ""
        }

        return Announce(
            nonce = nonce,
            audioPort = port,
            hostName = hostName,
            hasVersions = true,
            audioProtocolVersion = payload[trailer].toInt() and 0xff,
            controlProtocolVersion = payload[trailer + 1].toInt() and 0xff,
            frontEnd = payload[trailer + 2].toInt() and 0xff,
            buildVersion = build,
        )
    }

    /**
     * DSP settings pushed from the phone to the receiver. Every field is a single byte so the
     * message stays tiny and versionable; the phone is the remote control, the PC does the work.
     */
    fun configPayload(dsp: DspSettings): ByteArray = byteArrayOf(
        if (dsp.enabled) 1 else 0,
        (dsp.highPassHz / 4).coerceIn(0, 255).toByte(),
        dsp.gate.coerceIn(0, 100).toByte(),
        dsp.compressor.coerceIn(0, 100).toByte(),
        (dsp.presenceDb * 10).toInt().coerceIn(0, 200).toByte(),
        dsp.makeup.coerceIn(0, 100).toByte(),
        dsp.noiseReduction.coerceIn(0, 100).toByte(),
    )

    fun readStats(payload: ByteArray): ReceiverStats? {
        if (payload.size < 45) return null
        fun long(offset: Int): Long {
            var value = 0L
            for (index in 0 until 8) {
                value = (value shl 8) or (payload[offset + index].toLong() and 0xff)
            }
            return value
        }
        return ReceiverStats(
            packets = long(0),
            lost = long(8),
            late = long(16),
            rejected = long(24),
            trimmed = long(32),
            bufferMillis = ((payload[40].toInt() and 0xff) shl 24) or
                ((payload[41].toInt() and 0xff) shl 16) or
                ((payload[42].toInt() and 0xff) shl 8) or
                (payload[43].toInt() and 0xff),
            playing = (payload[44].toInt() and 1) == 1,
        )
    }
}

class ControlMessage(
    val type: Byte,
    val payload: ByteArray,
    internal val frame: ByteArray,
)

/**
 * A decoded discovery announce.
 *
 * [hasVersions] is false for a receiver built before the version trailer existed, in which case
 * the version fields carry no information and must not be compared against anything — reporting
 * "version mismatch" for a receiver that simply never stated its version would be worse than
 * saying nothing.
 */
data class Announce(
    val nonce: ByteArray,
    val audioPort: Int,
    val hostName: String,
    val hasVersions: Boolean = false,
    val audioProtocolVersion: Int = 0,
    val controlProtocolVersion: Int = 0,
    val frontEnd: Int = ControlProtocol.FRONT_END_UNKNOWN.toInt(),
    val buildVersion: String = "",
) {
    /**
     * Whether this phone and that receiver speak the same audio protocol. Unknown versions are
     * treated as compatible: an older receiver that never announced one has, by definition, the
     * only version that existed when it was built, and that is the one this phone speaks.
     */
    val audioProtocolMatches: Boolean
        get() = !hasVersions || audioProtocolVersion == (PacketCrypto.VERSION.toInt() and 0xff)

    /** Human-readable receiver front end, for diagnostics and the mismatch message. */
    val frontEndName: String
        get() = when (frontEnd.toByte()) {
            ControlProtocol.FRONT_END_CLASSIC -> "classic"
            ControlProtocol.FRONT_END_MODERN -> "modern"
            else -> "unknown"
        }

    override fun equals(other: Any?): Boolean =
        other is Announce && audioPort == other.audioPort && hostName == other.hostName &&
            hasVersions == other.hasVersions &&
            audioProtocolVersion == other.audioProtocolVersion &&
            controlProtocolVersion == other.controlProtocolVersion &&
            frontEnd == other.frontEnd && buildVersion == other.buildVersion &&
            nonce.contentEquals(other.nonce)

    override fun hashCode(): Int {
        var result = nonce.contentHashCode()
        result = result * 31 + audioPort
        result = result * 31 + hostName.hashCode()
        result = result * 31 + hasVersions.hashCode()
        result = result * 31 + audioProtocolVersion
        result = result * 31 + controlProtocolVersion
        result = result * 31 + frontEnd
        result = result * 31 + buildVersion.hashCode()
        return result
    }
}

data class ReceiverStats(
    val packets: Long,
    val lost: Long,
    val late: Long,
    val rejected: Long,
    val trimmed: Long,
    val bufferMillis: Int,
    val playing: Boolean,
)
