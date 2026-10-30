package com.canopydigital.pocketmic

import java.io.IOException
import org.junit.Assert.assertEquals
import org.junit.Test

class SenderTelemetryTest {
    @Test fun countsOnlySuccessfulSubmissionsAndSeparatesFailures() {
        val telemetry = SenderTelemetry()
        telemetry.send { }
        telemetry.send { throw IOException("network unavailable") }
        telemetry.send { }
        assertEquals(2L, telemetry.sent)
        assertEquals(1L, telemetry.errors)
    }
}
