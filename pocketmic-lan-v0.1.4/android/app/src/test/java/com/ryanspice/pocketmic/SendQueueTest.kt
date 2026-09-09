package com.ryanspice.pocketmic

import kotlinx.coroutines.channels.BufferOverflow
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.runBlocking
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * The send queue's drop-oldest policy, which is the whole "choppy" fix: the capture loop must
 * never block on the network, so when the sender cannot keep up the queue discards the oldest
 * buffered packet instead of stalling capture — bounded latency, freshest audio wins. The
 * eviction hook is also pinned, because the diagnostics "packets dropped" counter reads it.
 */
class SendQueueTest {
    private val capacity = 4

    private fun queue(onDrop: (ByteArray) -> Unit = {}): Channel<ByteArray> =
        Channel(capacity, BufferOverflow.DROP_OLDEST, onUndeliveredElement = onDrop)

    @Test
    fun packetsAreDeliveredInOrderWhileTheSenderKeepsUp() {
        runBlocking {
            val queue = queue()

            repeat(capacity) { index ->
                assertTrue(queue.trySend(byteArrayOf(index.toByte())).isSuccess)
            }

            for (index in 0 until capacity) {
                val packet = queue.receive()
                assertEquals(index.toByte(), packet[0])
            }
            queue.close()
        }
    }

    @Test
    fun whenFullTheOldestPacketIsDroppedNotTheNewest() {
        runBlocking {
            val queue = queue()

            repeat(capacity) { index ->
                queue.trySend(byteArrayOf(index.toByte()))
            }
            // Overflow: packet 4 evicts packet 0.
            queue.trySend(byteArrayOf(4))

            // Closed before draining: receiveCatching must return null once the buffer is
            // empty, or the drain loop would suspend forever waiting for a close that
            // could only come after it.
            queue.close()

            val remaining = buildList {
                while (true) {
                    val packet = queue.receiveCatching().getOrNull() ?: break
                    add(packet[0])
                }
            }

            assertEquals(listOf<Byte>(1, 2, 3, 4), remaining)
        }
    }

    @Test
    fun everyEvictedPacketIsReportedThroughTheUndeliveredHook() {
        runBlocking {
            var dropped = 0
            val queue = queue(onDrop = { dropped += 1 })

            repeat(capacity) { index ->
                queue.trySend(byteArrayOf(index.toByte()))
            }
            repeat(10) { index ->
                queue.trySend(byteArrayOf((capacity + index).toByte()))
            }

            // Ten pushes past capacity, ten evictions reported.
            assertEquals(10, dropped)
            queue.close()
        }
    }

    @Test
    fun aSlowConsumerNeverBlocksTheProducer() {
        runBlocking {
            val queue = queue()

            // Producer pushes far more than the consumer drains, without suspending even once.
            var produced = 0
            repeat(10_000) {
                if (queue.trySend(byteArrayOf(7)).isSuccess) produced += 1
            }

            // trySend never failed: DROP_OLDEST keeps accepting by evicting the oldest.
            assertEquals(10_000, produced)
            val head = queue.receive()
            assertNotNull(head)
            queue.close()
        }
    }
}