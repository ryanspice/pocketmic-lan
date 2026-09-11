package com.ryanspice.pocketmic

/**
 * Kotlin-side wrapper for the native libopus encoder.
 *
 * The JNI layer in `opus_jni.c` does the actual encoding; this class owns the
 * lifecycle (create / encode / destroy) and exposes a friendly API to
 * [MicStreamingService].
 *
 * Construction is fallible: the native library may fail to initialise the
 * encoder (e.g. if the NDK-compiled libopus was not bundled). Callers should
 * handle a null return from [create] and fall back to PCM16.
 */
class OpusEncoder private constructor(private val handle: Long) {

    companion object {
        /**
         * Creates a new encoder targeting 48 kHz mono VOIP at the given bitrate.
         *
         * @param sampleRate  Sample rate in Hz (must be 8000, 12000, 16000, 24000, or 48000).
         * @param channels    Number of audio channels (1 for mono).
         * @param bitrate     Target bitrate in bits per second (32_000–48_000 recommended).
         * @return an encoder, or null if native initialisation failed.
         */
        fun create(sampleRate: Int = 48_000, channels: Int = 1, bitrate: Int = 48_000): OpusEncoder? {
            val handle = nativeCreate(sampleRate, channels, bitrate)
            return if (handle != 0L) OpusEncoder(handle) else null
        }

        init {
            System.loadLibrary("pocketmic_opus")
        }
    }

    /**
     * Encodes one frame of PCM16 mono audio into Opus.
     *
     * @param pcm  Interleaved PCM16 little-endian samples (already in native byte order
     *             on Android/ARM).
     * @param pcmLength  Number of samples (not bytes). For a 10 ms frame at 48 kHz this is 480.
     * @param maxOutputBytes  Upper bound on the encoded output. 512 is more than sufficient
     *                        for any 10 ms Opus frame.
     * @return the Opus-encoded bytes, or null on error.
     */
    fun encode(pcm: ShortArray, pcmLength: Int, maxOutputBytes: Int = 512): ByteArray? {
        if (handle == 0L) return null
        return nativeEncode(handle, pcm, pcmLength, maxOutputBytes)
    }

    /**
     * Releases the native encoder. Must be called when streaming stops.
     */
    fun release() {
        if (handle != 0L) {
            nativeDestroy(handle)
        }
    }

    // -- JNI declarations ---------------------------------------------------

    private external fun nativeCreate(sampleRate: Int, channels: Int, bitrate: Int): Long
    private external fun nativeEncode(handle: Long, pcm: ShortArray, pcmLength: Int, maxOutputBytes: Int): ByteArray?
    private external fun nativeDestroy(handle: Long)
}
