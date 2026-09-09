#!/usr/bin/env python3
"""Checks the receiver's uint32 sequencing and bounded jitter-buffer policy.

The constants here mirror AudioPipeline.cs (100 ms prebuffer, 220 ms high-water mark,
20-packet concealment ceiling, 300 ms provider buffer). The policy question this tool answers:
can a maximum concealable gap ever overflow the provider buffer?

In the running receiver, concealment replaces audio the output has already consumed: a 200 ms
gap means the WaveOut consumed ~200 ms while packets were missing, then the loop adds up to
200 ms of concealment plus one real frame. Net growth is one frame. The one case where the
buffer grows by the full gap is before playback has started (buffer below the 100 ms prebuffer,
nothing consuming), so the binding constraint is:

    (prebuffer - one frame) + full concealment gap < provider buffer

with the high-water trim keeping the steady-state queue at or below 220 ms during playback.
"""

from __future__ import annotations

import sys

UINT32_MASK = 0xFFFF_FFFF
PACKET_MS = 10
PREBUFFER_MS = 100
HIGH_WATER_MS = 220
MAX_CONCEALED_GAP = 20
PROVIDER_BUFFER_MS = 300


def signed_delta(sequence: int, expected: int) -> int:
    raw = (sequence - expected) & UINT32_MASK
    return raw - 0x1_0000_0000 if raw & 0x8000_0000 else raw


def classify(last: int, current: int) -> tuple[str, int]:
    expected = (last + 1) & UINT32_MASK
    delta = signed_delta(current, expected)
    if delta < 0:
        return "late", -delta
    if delta > 0:
        return "gap", delta
    return "next", 0


def trim_buffer(buffer_ms: int) -> tuple[int, int]:
    trimmed = 0
    while buffer_ms > HIGH_WATER_MS:
        buffer_ms -= PACKET_MS
        trimmed += 1
    return buffer_ms, trimmed


def should_handle_fault(*, current_run: int, event_run: int, active: bool, handling: bool, closing: bool) -> bool:
    return active and not handling and not closing and current_run == event_run


def should_apply_status(*, current_run: int, event_run: int, active: bool) -> bool:
    return active and current_run == event_run


def main() -> int:
    assert classify(10, 11) == ("next", 0)
    assert classify(10, 13) == ("gap", 2)
    assert classify(10, 9)[0] == "late"
    assert classify(0xFFFF_FFFE, 0xFFFF_FFFF) == ("next", 0)
    assert classify(0xFFFF_FFFF, 0) == ("next", 0)
    assert classify(0, 0xFFFF_FFFF)[0] == "late"

    # Playback begins only after ten complete packets.
    assert PREBUFFER_MS // PACKET_MS == 10

    # Steady-state trimming keeps the queue at or below the 220 ms high-water mark.
    at_high_water, trims = trim_buffer(HIGH_WATER_MS)
    assert at_high_water == HIGH_WATER_MS and trims == 0
    over_high_water, trims = trim_buffer(HIGH_WATER_MS + 3 * PACKET_MS)
    assert over_high_water == HIGH_WATER_MS and trims == 3

    # During playback a gap is concealment minus what the output consumed (net one frame),
    # so the queue after a worst-case gap never exceeds high-water + one frame.
    assert HIGH_WATER_MS + PACKET_MS < PROVIDER_BUFFER_MS

    # Before playback starts nothing is consumed, so the worst case is the queue sitting just
    # below the prebuffer threshold plus the full concealment ceiling. Still under 300 ms.
    worst_before_playback = (PREBUFFER_MS - PACKET_MS) + MAX_CONCEALED_GAP * PACKET_MS
    assert worst_before_playback == 290
    assert worst_before_playback < PROVIDER_BUFFER_MS

    # A queued failure from an old receiver run must never stop its replacement.
    assert should_handle_fault(current_run=4, event_run=4, active=True, handling=False, closing=False)
    assert not should_handle_fault(current_run=5, event_run=4, active=True, handling=False, closing=False)
    assert not should_handle_fault(current_run=4, event_run=4, active=False, handling=False, closing=False)
    assert not should_handle_fault(current_run=4, event_run=4, active=True, handling=True, closing=False)
    assert not should_handle_fault(current_run=4, event_run=4, active=True, handling=False, closing=True)

    # Queued telemetry from a stopped or replaced run must not repaint the UI.
    assert should_apply_status(current_run=5, event_run=5, active=True)
    assert not should_apply_status(current_run=6, event_run=5, active=True)
    assert not should_apply_status(current_run=5, event_run=5, active=False)

    print("PASS: sequence rollover, buffering, latency trimming, and stale-run guards")
    return 0


if __name__ == "__main__":
    sys.exit(main())