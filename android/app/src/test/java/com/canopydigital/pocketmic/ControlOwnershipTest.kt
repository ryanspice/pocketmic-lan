package com.canopydigital.pocketmic

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ControlOwnershipTest {
    @Test fun streamKeepsControlAliveWhenUiLeavesAndLastOwnerReleasesIt() {
        val owners = ControlOwnership()
        val ui = Any()
        val stream = Any()
        owners.acquireUi(ui)
        assertTrue(owners.idleAllowed)
        owners.acquireStream(stream)
        assertFalse(owners.idleAllowed)
        owners.releaseUi(ui)
        assertFalse(owners.unused)
        owners.releaseStream(stream)
        assertFalse(owners.ownsStream(stream))
        assertTrue(owners.unused)
    }

    @Test fun rotationAndDuplicateEventsCannotDropAnotherOwnersLease() {
        val owners = ControlOwnership()
        val oldUi = Any()
        val newUi = Any()
        owners.acquireUi(oldUi)
        owners.acquireUi(oldUi)
        owners.acquireUi(newUi)
        owners.releaseUi(oldUi)
        owners.releaseUi(oldUi)
        assertTrue(owners.ownsUi(newUi))
        assertTrue(owners.idleAllowed)
    }

    @Test fun aRetiredRunCannotActForItsReplacementInTheSameService() {
        val owners = ControlOwnership()
        val service = Any()
        val old = Any()
        val current = Any()
        owners.acquireStream(service)
        owners.beginRun(service, old)
        owners.beginRun(service, current)
        assertFalse(owners.ownsRun(service, old))
        assertTrue(owners.ownsRun(service, current))
        owners.releaseStream(service)
        assertFalse(owners.ownsRun(service, current))
    }
}
