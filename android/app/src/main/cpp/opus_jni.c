/**
 * opus_jni.c – Thin JNI wrapper around libopus for PocketMic Android encoding.
 *
 * Exposes three functions:
 *   nativeCreate(sampleRate, channels, bitrate)  → long (encoder handle)
 *   nativeEncode(handle, pcmShorts, pcmLength, maxOutputBytes) → byte[]
 *   nativeDestroy(handle)
 *
 * The encoder runs in VOIP mode with FEC enabled, which is the right trade-off
 * for real-time voice over a LAN: low delay, tolerable packet loss recovery,
 * and a bitrate that keeps every frame comfortably under 150 bytes.
 */

#include <jni.h>
#include <stdlib.h>
#include <string.h>
#include <opus.h>
#include <android/log.h>

#define TAG "PocketMic"
#define LOGE(...) __android_log_print(ANDROID_LOG_ERROR, TAG, __VA_ARGS__)

/* Maximum encoded frame size at 48 kHz mono, 48 kbit/s VOIP. Opus cannot
   exceed 4000 bytes for a single frame, but in practice 10 ms at 48 kbit/s
   is well under 200 bytes. 512 gives ample headroom. */
#define MAX_ENCODED_BYTES 512

typedef struct {
    OpusEncoder *encoder;
    int channels;
} PocketMicOpusEncoder;

JNIEXPORT jlong JNICALL
Java_com_canopydigital_pocketmic_OpusEncoder_nativeCreate(
    JNIEnv *env, jobject thiz, jint sampleRate, jint channels, jint bitrate)
{
    int err;
    if ((sampleRate != 8000 && sampleRate != 12000 && sampleRate != 16000 &&
         sampleRate != 24000 && sampleRate != 48000) ||
        channels < 1 || channels > 2 || bitrate < 500 || bitrate > 512000) {
        return 0;
    }

    OpusEncoder *enc = opus_encoder_create(
        (opus_int32)sampleRate,
        channels,
        OPUS_APPLICATION_VOIP,
        &err);

    if (err != OPUS_OK || enc == NULL) {
        LOGE("opus_encoder_create failed: %s", opus_strerror(err));
        return 0;
    }

    /* Bitrate ceiling. */
    opus_encoder_ctl(enc, OPUS_SET_BITRATE(bitrate));

    /* Complexity 10 = maximum quality. The encoder is per-packet at 10 ms,
       so the CPU cost is bounded and the phone can afford it. */
    opus_encoder_ctl(enc, OPUS_SET_COMPLEXITY(10));

    /* Forward error correction: Opus in-band FEC adds redundant data to
       every frame so the decoder can recover from a single lost packet.
       On a LAN the loss rate is low, but the overhead is negligible and
       the quality improvement when a loss does happen is dramatic. */
    opus_encoder_ctl(enc, OPUS_SET_INBAND_FEC(1));

    /* Signal type hint: VOIP application already does this, but be explicit. */
    opus_encoder_ctl(enc, OPUS_SET_SIGNAL(OPUS_SIGNAL_VOICE));

    /* 10 ms frames at 48 kHz = 480 samples. Opus defaults to this but
       pinning it prevents surprises from library updates. */
    opus_encoder_ctl(enc, OPUS_SET_EXPERT_FRAME_DURATION(OPUS_FRAMESIZE_10_MS));

    PocketMicOpusEncoder *handle = (PocketMicOpusEncoder *)malloc(sizeof(PocketMicOpusEncoder));
    if (handle == NULL) {
        opus_encoder_destroy(enc);
        return 0;
    }
    handle->encoder = enc;
    handle->channels = channels;
    return (jlong)(intptr_t)handle;
}

JNIEXPORT jbyteArray JNICALL
Java_com_canopydigital_pocketmic_OpusEncoder_nativeEncode(
    JNIEnv *env, jobject thiz, jlong handle, jshortArray pcm, jint pcmLength,
    jint maxOutputBytes)
{
    if (handle == 0 || pcm == NULL || pcmLength <= 0 ||
        maxOutputBytes <= 0 || maxOutputBytes > MAX_ENCODED_BYTES) return NULL;

    PocketMicOpusEncoder *owner = (PocketMicOpusEncoder *)(intptr_t)handle;
    if (owner->encoder == NULL || owner->channels < 1 || owner->channels > 2) return NULL;

    jsize arrayLength = (*env)->GetArrayLength(env, pcm);
    if (arrayLength <= 0 || pcmLength > arrayLength / owner->channels) return NULL;

    jshort *pcmBuf = (*env)->GetShortArrayElements(env, pcm, NULL);
    if (pcmBuf == NULL) return NULL;

    unsigned char *outBuf = (unsigned char *)malloc((size_t)maxOutputBytes);
    if (outBuf == NULL) {
        (*env)->ReleaseShortArrayElements(env, pcm, pcmBuf, JNI_ABORT);
        return NULL;
    }

    int encodedBytes = opus_encode(
        owner->encoder,
        pcmBuf,
        (int)pcmLength,
        outBuf,
        (int)maxOutputBytes);

    (*env)->ReleaseShortArrayElements(env, pcm, pcmBuf, JNI_ABORT);

    if (encodedBytes < 0) {
        LOGE("opus_encode failed: %s", opus_strerror(encodedBytes));
        free(outBuf);
        return NULL;
    }

    jbyteArray result = (*env)->NewByteArray(env, encodedBytes);
    if (result != NULL) {
        (*env)->SetByteArrayRegion(env, result, 0, encodedBytes, (jbyte *)outBuf);
    }

    free(outBuf);
    return result;
}

JNIEXPORT void JNICALL
Java_com_canopydigital_pocketmic_OpusEncoder_nativeDestroy(
    JNIEnv *env, jobject thiz, jlong handle)
{
    if (handle != 0) {
        PocketMicOpusEncoder *owner = (PocketMicOpusEncoder *)(intptr_t)handle;
        if (owner->encoder != NULL) {
            opus_encoder_destroy(owner->encoder);
            owner->encoder = NULL;
        }
        free(owner);
    }
}
