package com.ryanspice.pocketmic

import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * CaptureMode.fromWireValue() maps the string the receiver puts on the wire to the local
 * enum. An unknown value must fall back to CLEAN rather than crashing — a newer phone and
 * an older receiver must degrade gracefully, not throw.
 */
class CaptureModeTest {

    @Test
    fun fromWireValueReturnsCorrectMode() {
        assertEquals(CaptureMode.CLEAN, CaptureMode.fromWireValue("clean"))
        assertEquals(CaptureMode.VOICE, CaptureMode.fromWireValue("voice"))
        assertEquals(CaptureMode.CUSTOM, CaptureMode.fromWireValue("custom"))
    }

    @Test
    fun fromWireValueFallsBackToCleanForUnknown() {
        assertEquals(CaptureMode.CLEAN, CaptureMode.fromWireValue("unknown"))
        assertEquals(CaptureMode.CLEAN, CaptureMode.fromWireValue(null))
    }
}
