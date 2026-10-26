package com.canopydigital.pocketmic

import org.junit.Assert.assertEquals
import org.junit.Test

class DeliveryProgressTest {
    private fun stats(packets: Long, session: Long? = null, sequence: Int? = null) =
        ReceiverStats(packets, 0, 0, 0, 0, 0, true, session, sequence)

    @Test fun currentSessionReceiptRejectsOtherRunsDuplicatesAndReordering() {
        val delivery = DeliveryProgress().apply { reset(42) }
        delivery.observe(stats(500, 41, 50), 100)
        assertEquals(0L, delivery.lastProgressAtMillis)
        delivery.observe(stats(501, 42, 0), 200)
        delivery.observe(stats(502, 42, 1), 300)
        delivery.observe(stats(502, 42, 1), 400)
        delivery.observe(stats(501, 42, 0), 500)
        assertEquals(300L, delivery.lastProgressAtMillis)
        delivery.observe(stats(600), 600) // no downgrade to unqualified statistics
        assertEquals(300L, delivery.lastProgressAtMillis)
    }

    @Test fun receiverCounterResetDoesNotMatterToSessionSequence() {
        val delivery = DeliveryProgress().apply { reset(42) }
        delivery.observe(stats(999, 42, 100), 100)
        delivery.observe(stats(1, 42, 101), 200)
        assertEquals(200L, delivery.lastProgressAtMillis)
    }

    @Test fun sequenceRolloverAdvancesAndNewRunClearsConfirmation() {
        val delivery = DeliveryProgress().apply { reset(42) }
        delivery.observe(stats(1, 42, -1), 100)
        delivery.observe(stats(2, 42, 0), 200)
        assertEquals(200L, delivery.lastProgressAtMillis)
        delivery.reset(43)
        delivery.observe(stats(3, 42, 1), 300)
        assertEquals(0L, delivery.lastProgressAtMillis)
    }

    @Test fun legacyBaselineDuplicatesAndSingleReorderedSampleDoNotProveDelivery() {
        val delivery = DeliveryProgress().apply { reset(42) }
        delivery.observe(stats(100), 100)
        assertEquals(0L, delivery.lastProgressAtMillis)
        delivery.observe(stats(101), 200)
        delivery.observe(stats(100), 300)
        delivery.observe(stats(101), 400)
        assertEquals(200L, delivery.lastProgressAtMillis)
    }

    @Test fun legacyResetRequiresRepeatedForwardProgress() {
        val delivery = DeliveryProgress().apply { reset(42) }
        delivery.observe(stats(100), 100)
        delivery.observe(stats(1), 200)
        delivery.observe(stats(2), 300)
        assertEquals(0L, delivery.lastProgressAtMillis)
        delivery.observe(stats(3), 400)
        assertEquals(400L, delivery.lastProgressAtMillis)
    }

    @Test fun deliveryRequiresBothFreshControlAndProgressAndPreservesGiveUpWindow() {
        assertEquals(LinkAction.ENTER_RECONNECTING,
            ReconnectPolicy.evaluateDelivery(true, 10000, 1000, 9900, 0, 7000))
        assertEquals(LinkAction.ENTER_RECONNECTING,
            ReconnectPolicy.evaluateDelivery(true, 10000, 1000, 7000, 0, 9900))
        assertEquals(LinkAction.RESUME_STREAMING,
            ReconnectPolicy.evaluateDelivery(false, 10000, 1000, 9900, 8000, 9950))
        assertEquals(LinkAction.GIVE_UP,
            ReconnectPolicy.evaluateDelivery(false, 70000, 1000, 0, 10000, 69900))
        assertEquals(LinkAction.CONTINUE,
            ReconnectPolicy.evaluateDelivery(true, 1500, 1000, 0, 0, 0))
    }

    @Test fun receiptParserReadsExactOffsetsAndKeepsLegacyPacketsCompatible() {
        val bytes = java.nio.ByteBuffer.allocate(58).order(java.nio.ByteOrder.BIG_ENDIAN)
        bytes.putLong(0, 900)
        bytes.put(45, 1)
        bytes.putLong(46, 0x0102030405060708L)
        bytes.putInt(54, -1)
        val parsed = ControlProtocol.readStats(bytes.array())!!
        assertEquals(0x0102030405060708L, parsed.sessionId)
        assertEquals(-1, parsed.sequence)
        assertEquals(null, ControlProtocol.readStats(bytes.array().copyOf(45))!!.sessionId)
    }
}
