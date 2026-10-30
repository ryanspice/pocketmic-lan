package com.canopydigital.pocketmic

import java.io.Closeable

/** One sender updates the route; close can race a route change without reopening it. */
internal class RebindableSocket<N, S : Closeable>(initialNetwork: N, initialSocket: S) : Closeable {
    private var network = initialNetwork
    private var socket: S? = initialSocket

    @Synchronized fun current(): S? = socket

    fun updateNetwork(next: N, open: (N) -> S): Boolean {
        val previous = synchronized(this) {
            if (socket == null || network == next) return false
            network
        }
        val candidate = open(next)
        val old = synchronized(this) {
            if (socket == null || network != previous) {
                candidate.close()
                return false
            }
            val replaced = socket
            socket = candidate
            network = next
            replaced
        }
        old?.close()
        return true
    }

    override fun close() {
        val old = synchronized(this) { socket.also { socket = null } }
        old?.close()
    }
}
