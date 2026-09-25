package com.canopydigital.pocketmic

/**
 * Kotlin-side owner for the native libopus encoder. Library loading is deferred until Opus is
 * requested so an absent optional ABI library can fall back to PCM instead of crashing class load.
 */
class OpusEncoder private constructor(private var handle: Long, private val channels: Int) : java.io.Closeable {

    /** Encodes one Opus frame; [pcmLength] is samples per channel. */
    fun encode(pcm: ShortArray, pcmLength: Int, maxOutputBytes: Int = OpusInputContract.MAX_OUTPUT_BYTES): ByteArray? {
        if (handle == 0L || !OpusInputContract.isValid(pcm.size, pcmLength, channels, maxOutputBytes)) return null
        return try {
            nativeEncode(handle, pcm, pcmLength, maxOutputBytes)
        } catch (_: UnsatisfiedLinkError) {
            null
        }
    }

    /** Safe to call more than once; encoding and destruction are owned by the service coroutine. */
    fun release() {
        if (handle == 0L) return
        val ownedHandle = handle
        handle = 0L
        try {
            nativeDestroy(ownedHandle)
        } catch (_: UnsatisfiedLinkError) {
            // The service still relinquishes the Java-side handle if a native symbol is missing.
        }
    }

    override fun close() = release()

    companion object {
        @Volatile private var libraryLoaded = false
        @Volatile private var libraryLoadFailed = false

        /** Returns null for a missing optional library, unsupported parameters, or native init failure. */
        fun create(sampleRate: Int = 48_000, channels: Int = 1, bitrate: Int = 48_000): OpusEncoder? {
            if (sampleRate !in setOf(8_000, 12_000, 16_000, 24_000, 48_000) || channels !in 1..2 || bitrate <= 0) return null
            if (!ensureLibraryLoaded()) return null

            return try {
                val handle = nativeCreate(sampleRate, channels, bitrate)
                if (handle != 0L) OpusEncoder(handle, channels) else null
            } catch (_: UnsatisfiedLinkError) {
                null
            }
        }

        @Synchronized
        private fun ensureLibraryLoaded(): Boolean {
            if (libraryLoaded) return true
            if (libraryLoadFailed) return false

            return try {
                System.loadLibrary("pocketmic_jni")
                libraryLoaded = true
                true
            } catch (_: UnsatisfiedLinkError) {
                libraryLoadFailed = true
                false
            }
        }

        @JvmStatic private external fun nativeCreate(sampleRate: Int, channels: Int, bitrate: Int): Long
        @JvmStatic private external fun nativeEncode(handle: Long, pcm: ShortArray, pcmLength: Int, maxOutputBytes: Int): ByteArray?
        @JvmStatic private external fun nativeDestroy(handle: Long)
    }
}
