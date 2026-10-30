package com.canopydigital.pocketmic

/** Delete the legacy copy only after the selected secure key was durably written. */
internal fun migrateLegacyPairingKey(
    legacy: String,
    secure: String?,
    fallback: String,
    persistSecure: (String) -> Boolean,
    removeLegacy: () -> Unit,
): Boolean {
    val selected = secure?.takeIf { it.isNotBlank() }
        ?: legacy.takeIf { it.isNotBlank() }
        ?: fallback
    if (!persistSecure(selected)) return false
    removeLegacy()
    return true
}
