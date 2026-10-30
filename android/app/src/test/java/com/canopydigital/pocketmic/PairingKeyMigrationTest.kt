package com.canopydigital.pocketmic

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class PairingKeyMigrationTest {
    @Test fun legacyKeyIsPersistedBeforeRemoval() {
        val events = mutableListOf<String>()
        assertTrue(migrateLegacyPairingKey("legacy-key", null, "fallback", {
            events += "write:$it"; true
        }, { events += "remove" }))
        assertEquals(listOf("write:legacy-key", "remove"), events)
    }

    @Test fun existingSecureKeyWinsOverStalePlaintext() {
        var written = ""
        assertTrue(migrateLegacyPairingKey("old-key", "current-key", "fallback", {
            written = it; true
        }, {}))
        assertEquals("current-key", written)
    }

    @Test fun failedSecureWriteRetainsLegacyCopy() {
        var removed = false
        assertFalse(migrateLegacyPairingKey("legacy-key", null, "fallback", { false }, { removed = true }))
        assertFalse(removed)
    }

    @Test fun blankStoresUseTheSelectedFallback() {
        var written = ""
        assertTrue(migrateLegacyPairingKey("", "", "generated-key", { written = it; true }, {}))
        assertEquals("generated-key", written)
    }
}
