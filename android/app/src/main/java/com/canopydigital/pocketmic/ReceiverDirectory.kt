package com.canopydigital.pocketmic

/** Bounded, expiring LAN results. Replies from other PCs never replace the selected peer. */
internal class ReceiverDirectory(private val ttlMillis: Long = 5_000L) {
    private data class Entry(val receiver: PeerState.Found, val seenAt: Long)
    private val entries = linkedMapOf<String, Entry>()

    @Synchronized
    fun record(receiver: PeerState.Found, now: Long) {
        entries["${receiver.address}:${receiver.audioPort}"] = Entry(receiver, now)
        if (entries.size > 32) entries.remove(entries.keys.first())
    }

    @Synchronized
    fun snapshot(now: Long): List<PeerState.Found> {
        entries.entries.removeAll { now - it.value.seenAt >= ttlMillis }
        return entries.values.map { it.receiver }.sortedWith(
            compareByDescending<PeerState.Found> { it.keyMatches && it.audioProtocolMatches }
                .thenBy { it.address }.thenBy { it.audioPort },
        )
    }

    @Synchronized
    fun clear() = entries.clear()

    companion object {
        fun select(receivers: List<PeerState.Found>, host: String, port: Int,
            allowFallback: Boolean = true): PeerState.Found? {
            // Prefer the exact endpoint when multiple receivers share a PC. A pinned PC may
            // change ports, but only an authenticated, compatible announcement can move it.
            // Keep wrong-key replies for diagnostics; never silently switch to another PC.
            if (host.isNotBlank()) {
                val preferred = receivers.firstOrNull {
                    it.address == host && it.audioPort == port
                } ?: receivers.firstOrNull {
                    it.address == host && it.keyMatches && it.audioProtocolMatches
                } ?: receivers.firstOrNull { it.address == host && !it.keyMatches }
                if (preferred != null || !allowFallback) return preferred
            }
            return receivers.firstOrNull { it.keyMatches && it.audioProtocolMatches }
                ?: receivers.firstOrNull()
        }
    }
}
