package com.ryanspice.pocketmic

import org.junit.Assert.*
import org.junit.Test

class ReceiverDirectoryTest {
    private fun pc(ip: String, port: Int = DEFAULT_PORT, matches: Boolean = true) =
        PeerState.Found(ip, if (matches) "PC-$ip" else "", port, matches)

    @Test fun retainsMultiplePcsAndCustomPortsWithoutDuplicates() {
        val directory = ReceiverDirectory()
        directory.record(pc("10.0.0.2"), 0)
        directory.record(pc("10.0.0.3", 49520), 1)
        directory.record(pc("10.0.0.2"), 2)
        assertEquals(2, directory.snapshot(3).size)
        assertEquals(49520, directory.snapshot(3).last().audioPort)
    }

    @Test fun wrongKeyReplyDoesNotReplaceSelectedAuthenticatedPc() {
        val selected = pc("10.0.0.3", 49520)
        val directory = ReceiverDirectory()
        directory.record(selected, 0)
        directory.record(pc("10.0.0.2", 0, false), 1)
        assertEquals(selected, ReceiverDirectory.select(directory.snapshot(2), selected.address, selected.audioPort))
        assertEquals(2, directory.snapshot(2).size)
    }

    @Test fun vanishedReceiversExpireEvenWhileOtherPcsKeepReplying() {
        val directory = ReceiverDirectory()
        directory.record(pc("10.0.0.2"), 0)
        directory.record(pc("10.0.0.3"), 4000)
        assertEquals(listOf(pc("10.0.0.3")), directory.snapshot(5000))
        assertTrue(directory.snapshot(9000).isEmpty())
    }

    @Test fun automaticSelectionPrefersMatchingKeyOverEarlierWrongKeyReply() {
        val matches = pc("10.0.0.3")
        assertEquals(matches, ReceiverDirectory.select(listOf(pc("10.0.0.2", 0, false), matches), "", DEFAULT_PORT))
    }

    @Test fun qrOrManualSelectionNeverSwitchesToAnotherPc() {
        assertNull(ReceiverDirectory.select(listOf(pc("10.0.0.3")), "10.0.0.2", DEFAULT_PORT, allowFallback = false))
    }

    @Test fun autoSelectionCanRecoverFromAnOldSavedAddress() {
        val moved = pc("10.0.0.3", 49520)
        assertEquals(moved, ReceiverDirectory.select(listOf(moved), "10.0.0.2", DEFAULT_PORT))
    }

    @Test fun selectionIncludesPortWhenSeveralInstancesShareAnAddress() {
        val custom = pc("10.0.0.2", 49520)
        assertEquals(custom, ReceiverDirectory.select(listOf(pc(custom.address), custom), custom.address, custom.audioPort))
    }

    @Test fun clearingOnKeyChangeRemovesOldAuthenticationResults() {
        val directory = ReceiverDirectory()
        directory.record(pc("10.0.0.2"), 0)
        directory.clear()
        assertTrue(directory.snapshot(1).isEmpty())
    }
}
