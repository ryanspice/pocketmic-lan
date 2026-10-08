package com.ryanspice.pocketmic

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

/**
 * The QR parser is the only input path that trusts external data without encryption.
 * These tests pin every branch of parseQrPairingData: scheme prefix, port defaulting,
 * key-length floor, and the port range ceiling.
 */
class QrParserTest {
    @Test fun rejectsMalformedExplicitPortInsteadOfSilentlyUsingDefault() {
        assertNull(parseQrPairingData("pmic://10.0.0.2:abc/POCKETMIC-TEST"))
        assertNull(parseQrPairingData("pmic://10.0.0.2:/POCKETMIC-TEST"))
    }

    @Test fun acceptsWhitespaceAndCaseInsensitiveSchemeFromScanner() {
        assertEquals(QrPairingData("10.0.0.2", 49520, "POCKETMIC-TEST"),
            parseQrPairingData("  PMIC://10.0.0.2:49520/POCKETMIC-TEST\n"))
    }

    @Test fun rejectsWhitespaceOnlyHost() {
        assertNull(parseQrPairingData("pmic:// :49500/POCKETMIC-TEST"))
    }

    @Test
    fun parseValidPmicUriWithHostPortAndKey() {
        val data = parseQrPairingData("pmic://192.168.1.25:49500/POCKETMIC-TEST")

        assertEquals("192.168.1.25", data!!.host)
        assertEquals(49_500, data.port)
        assertEquals("POCKETMIC-TEST", data.pairingKey)
    }

    @Test
    fun parseWithoutSchemePrefixAcceptsPlainHostPortKey() {
        val data = parseQrPairingData("192.168.1.25:49500/POCKETMIC-TEST")

        assertEquals("192.168.1.25", data!!.host)
        assertEquals(49_500, data.port)
        assertEquals("POCKETMIC-TEST", data.pairingKey)
    }

    @Test
    fun parseRejectsKeyShorterThanEightCharacters() {
        val data = parseQrPairingData("192.168.1.25:49500/SHORT")
        assertNull(data)
    }

    @Test
    fun parseDefaultPortWhenOmitted() {
        val data = parseQrPairingData("192.168.1.25/POCKETMIC-TEST")

        assertEquals("192.168.1.25", data!!.host)
        assertEquals(DEFAULT_PORT, data.port)
        assertEquals("POCKETMIC-TEST", data.pairingKey)
    }

    @Test
    fun parseRejectsPortOutOfRange() {
        val data = parseQrPairingData("192.168.1.25:99999/POCKETMIC-TEST")
        assertNull(data)
    }
}
