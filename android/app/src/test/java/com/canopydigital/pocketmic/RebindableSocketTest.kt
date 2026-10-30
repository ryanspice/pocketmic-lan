package com.canopydigital.pocketmic

import java.io.Closeable
import java.io.IOException
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertSame
import org.junit.Assert.assertTrue
import org.junit.Test

class RebindableSocketTest {
    private class FakeSocket : Closeable {
        var closed = false
        override fun close() { closed = true }
    }

    @Test fun replacementClosesOldSocketAndKeepsNewRoute() {
        val old = FakeSocket()
        val replacement = FakeSocket()
        val holder = RebindableSocket("network-a", old)
        assertTrue(holder.updateNetwork("network-b") { replacement })
        assertTrue(old.closed)
        assertSame(replacement, holder.current())
        assertFalse(holder.updateNetwork("network-b") { error("must not reopen") })
        holder.close()
        assertTrue(replacement.closed)
    }

    @Test fun failedOpenRetainsExistingSocketAndCanRetry() {
        val old = FakeSocket()
        val holder = RebindableSocket("network-a", old)
        try { holder.updateNetwork("network-b") { throw IOException("down") } } catch (_: IOException) { }
        assertSame(old, holder.current())
        assertFalse(old.closed)
        assertTrue(holder.updateNetwork("network-b") { FakeSocket() })
        holder.close()
    }

    @Test fun closeDuringOpeningDiscardsCandidate() {
        val old = FakeSocket()
        val candidate = FakeSocket()
        val holder = RebindableSocket("network-a", old)
        assertFalse(holder.updateNetwork("network-b") { holder.close(); candidate })
        assertTrue(old.closed)
        assertTrue(candidate.closed)
        assertEquals(null, holder.current())
    }
}
