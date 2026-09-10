package com.ryanspice.pocketmic

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * StreamingState is a singleton holding a MutableStateFlow<StreamingSnapshot>.
 * These tests verify the update composition model and the reset-to-defaults contract,
 * which the UI and the streaming service both depend on.
 */
class StreamingStateTest {

    @Test
    fun updateTransformsSnapshotUsingLatestState() {
        StreamingState.reset()

        StreamingState.update { it.copy(status = StreamStatus.STREAMING) }
        StreamingState.update { it.copy(packetsSent = 42L) }

        val snapshot = StreamingState.state.value
        assertEquals(StreamStatus.STREAMING, snapshot.status)
        assertEquals(42L, snapshot.packetsSent)
    }

    @Test
    fun resetReturnsToIdleDefaults() {
        StreamingState.update { it.copy(status = StreamStatus.ERROR) }
        StreamingState.update { it.copy(packetsSent = 99L) }
        StreamingState.update { it.copy(reconnects = 5) }

        StreamingState.reset()

        val snapshot = StreamingState.state.value
        assertEquals(StreamStatus.IDLE, snapshot.status)
        assertEquals(0L, snapshot.packetsSent)
        assertEquals(0, snapshot.reconnects)
        assertEquals(0f, snapshot.level)
        assertEquals("", snapshot.error)
        assertEquals(WifiLockMode.NONE, snapshot.wifiLockMode)
    }
}
