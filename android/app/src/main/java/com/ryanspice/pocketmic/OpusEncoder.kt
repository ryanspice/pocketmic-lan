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
 *
 * Must call [release] when done. After release, the encoder is unusable.
 */
class OpusEncoder private constructor(private var handle: Long) : java.io.Closeable {

    /**
     * Encodes one frame of PCM16 mono audio into Opus.
     *
     * @param pcm  Interleaved PCM16 little-endian samples.
     * @param pcmLength  Number of samples (not bytes). For a 10 ms frame at 48 kHz this is 480.
     * @param maxOutputBytes  Upper bound on the encoded output. 512 is sufficient.
     * @return the Opus-encoded bytes, or null on error.
     */
    fun encode(pcm: ShortArray, pcmLength: Int, maxOutputBytes: Int = 512): ByteArray? {
        if (handle == 0L) return null
        return nativeEncode(handle, pcm, pcmLength, maxOutputBytes)
    }

    /**
     * Releases the native encoder. Must be called when streaming stops.
     * Safe to call multiple times.
     */
    fun release() {
        if (handle != 0L) {
            nativeDestroy(handle)
            handle = 0L
        }
    }

    /**
     * Closeable implementation — delegates to [release].
     */
    override fun close() = release()

    companion object {
        /**
         * Creates a new encoder targeting 48 kHz mono VOIP at the given bitrate.
         */
        fun create(sampleRate: Int = 48_000, channels: Int = 1, bitrate: Int = 48_000): OpusEncoder? {
            val handle = nativeCreate(sampleRate, channels, bitrate)
            return if (handle != 0L) OpusEncoder(handle) else null
        }

        init {
            System.loadLibrary("pocketmic_jni")
        }

        // JNI declarations — must be in companion for static linkage
        @JvmStatic private external fun nativeCreate(sampleRate: Int, channels: Int, bitrate: Int): Long
        @JvmStatic private external fun nativeEncode(handle: Long, pcm: ShortArray, pcmLength: Int, maxOutputBytes: Int): ByteArray?
        @JvmStatic private external fun nativeDestroy(handle: Long)
    }
}
