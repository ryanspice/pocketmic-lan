package com.ryanspice.pocketmic

import android.content.Context
import android.content.SharedPreferences
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKeys
import java.security.SecureRandom

/** The default audio port. One source of truth for every default on the phone. */
const val DEFAULT_PORT = 49_500

/** The control channel always sits one port above; leave 65535 available for that companion port. */
const val MIN_PORT = 1

const val MAX_PORT = 65_534

const val MIN_PAIRING_KEY_LENGTH = 8

enum class CaptureMode(val wireValue: String) {
    CLEAN("clean"),
    VOICE("voice"),

    /**
     * Uses the cleanest capture source the phone offers — UNPROCESSED where supported, falling
     * back to VOICE_RECOGNITION and then MIC — with no phone-side processing of any kind. The
     * PC does all the shaping: the receiver's processing chain is driven by the sliders, which
     * are pushed over the control channel and take effect live while streaming.
     *
     * This is deliberately NOT the same capture path as [VOICE]: VOICE asks the device's own
     * voice-communication processing to condition the signal, while CUSTOM wants the rawest
     * input so the PC-side chain is the only processing in the path. Stacking a slider-driven
     * chain on top of vendor-processed audio would be the same double-processing that made the
     * old VOICE path sound wrong.
     */
    CUSTOM("custom");

    companion object {
        fun fromWireValue(value: String?): CaptureMode =
            entries.firstOrNull { it.wireValue == value } ?: CLEAN
    }
}

/** Receiver-side voice processing, remote-controlled from the phone. */
data class DspSettings(
    val enabled: Boolean = true,
    val highPassHz: Int = 85,
    val gate: Int = 60,
    val compressor: Int = 60,
    val presenceDb: Float = 3.5f,
    val makeup: Int = 60,
    val noiseReduction: Int = 50,
)

data class MicConfig(
    val host: String,
    val port: Int,
    val pairingKey: String,
    val captureMode: CaptureMode,
    val gain: Float,
) {
    /**
     * The three connection rules both the service and the activity must agree on, in one
     * place so they cannot drift. Returns a user-facing message, or null when valid.
     */
    fun validateConnection(): String? = when {
        host.isBlank() -> "Enter the Windows PC IP address."
        port !in MIN_PORT..MAX_PORT -> "Port must be between $MIN_PORT and $MAX_PORT."
        pairingKey.length < MIN_PAIRING_KEY_LENGTH ->
            "Pairing key must be at least $MIN_PAIRING_KEY_LENGTH characters."

        else -> null
    }
}

/** Interface preferences that never travel to the streaming service. */
data class UiPrefs(
    val autoConnect: Boolean,
    val fabCorner: FabCorner,
)

/** The action button snaps to a corner so it can never sit over a text field. */
enum class FabCorner {
    BOTTOM_END,
    BOTTOM_START,
    TOP_END,
    TOP_START;

    companion object {
        fun fromName(value: String?): FabCorner =
            entries.firstOrNull { it.name == value } ?: BOTTOM_END
    }
}

object AppPrefs {
    private const val PREFS = "pocketmic"
    private const val HOST = "host"
    private const val PORT = "port"
    private const val KEY = "pairing_key"
    private const val MODE = "capture_mode"
    private const val GAIN = "gain"
    private const val AUTO_CONNECT = "auto_connect"
    private const val FAB_CORNER = "fab_corner"

    /**
     * The pairing key is stored encrypted at rest via Android Keystore-backed
     * EncryptedSharedPreferences. Other settings (host, port, mode, gain) are not sensitive
     * and stay in plain SharedPreferences for simplicity.
     */
    private fun encryptedPrefs(context: Context): SharedPreferences {
        val masterKey = MasterKeys.getOrCreate(MasterKeys.AES256_GCM_SPEC)
        return EncryptedSharedPreferences.create(
            "pocketmic_secure",
            masterKey,
            context,
            EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
            EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM,
        )
    }

    private fun plainPrefs(context: Context): SharedPreferences =
        context.getSharedPreferences(PREFS, Context.MODE_PRIVATE)

    fun loadUi(context: Context): UiPrefs {
        val prefs = plainPrefs(context)
        return UiPrefs(
            autoConnect = prefs.getBoolean(AUTO_CONNECT, true),
            fabCorner = FabCorner.fromName(prefs.getString(FAB_CORNER, null)),
        )
    }

    fun saveAutoConnect(context: Context, enabled: Boolean) {
        plainPrefs(context).edit().putBoolean(AUTO_CONNECT, enabled).apply()
    }

    fun loadDsp(context: Context): DspSettings {
        val prefs = plainPrefs(context)
        val defaults = DspSettings()
        return DspSettings(
            enabled = prefs.getBoolean("dsp_enabled", defaults.enabled),
            highPassHz = prefs.getInt("dsp_hpf", defaults.highPassHz),
            gate = prefs.getInt("dsp_gate", defaults.gate),
            compressor = prefs.getInt("dsp_comp", defaults.compressor),
            presenceDb = prefs.getFloat("dsp_presence", defaults.presenceDb),
            makeup = prefs.getInt("dsp_makeup", defaults.makeup),
            noiseReduction = prefs.getInt("dsp_nr", defaults.noiseReduction),
        )
    }

    fun saveDsp(context: Context, dsp: DspSettings) {
        plainPrefs(context).edit()
            .putBoolean("dsp_enabled", dsp.enabled)
            .putInt("dsp_hpf", dsp.highPassHz)
            .putInt("dsp_gate", dsp.gate)
            .putInt("dsp_comp", dsp.compressor)
            .putFloat("dsp_presence", dsp.presenceDb)
            .putInt("dsp_makeup", dsp.makeup)
            .putInt("dsp_nr", dsp.noiseReduction)
            .apply()
    }

    fun saveFabCorner(context: Context, corner: FabCorner) {
        plainPrefs(context).edit().putString(FAB_CORNER, corner.name).apply()
    }

    fun load(context: Context): MicConfig {
        val plain = plainPrefs(context)
        val secure = encryptedPrefs(context)

        // Migrate: if the key exists in plain prefs but not encrypted, move it.
        val plainKey = plain.getString(KEY, null)
        val storedKey = secure.getString(KEY, null)
            ?: plainKey?.also { secure.edit().putString(KEY, it).apply() }
            ?: ""

        val key = storedKey.ifBlank { generatePairingKey() }
        if (storedKey.isBlank()) {
            secure.edit().putString(KEY, key).apply()
            // Clear the old plaintext copy.
            plain.edit().remove(KEY).apply()
        }
        return MicConfig(
            host = plain.getString(HOST, "").orEmpty(),
            port = plain.getInt(PORT, DEFAULT_PORT),
            pairingKey = key,
            // Voice is the default because it is the one source that never silently degrades:
            // VOICE_COMMUNICATION is the vendor-tuned voice path on every handset, whereas the
            // raw paths below it (UNPROCESSED is unavailable on many phones) fall back to
            // partly processed sources without the app being able to tell.
            captureMode = CaptureMode.fromWireValue(
                plain.getString(MODE, CaptureMode.VOICE.wireValue),
            ),
            gain = plain.getFloat(GAIN, 1.0f).coerceIn(0.5f, 3.0f),
        )
    }

    fun save(context: Context, config: MicConfig) {
        plainPrefs(context).edit()
            .putString(HOST, config.host.trim())
            .putInt(PORT, config.port)
            .putString(MODE, config.captureMode.wireValue)
            .putFloat(GAIN, config.gain)
            .apply()
        // Pairing key goes to encrypted storage only.
        encryptedPrefs(context).edit()
            .putString(KEY, config.pairingKey)
            .apply()
    }

    fun generatePairingKey(): String {
        // 32 symbols with I, O, 0 and 1 removed so a typed or read-aloud key cannot be
        // misread (I/l/1 and O/0 are the classic transcription failures). 12 characters from
        // this alphabet is ~60 bits of entropy, which the v0.1.2 derivation (single unsalted
        // SHA-256) stretches adequately for a trusted private LAN.
        val alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"
        val random = SecureRandom()
        return buildString(PAIRING_KEY_LENGTH) {
            repeat(PAIRING_KEY_LENGTH) {
                append(alphabet[random.nextInt(alphabet.length)])
            }
        }
    }

    private const val PAIRING_KEY_LENGTH = 12
}
