# PocketMic v0.1.4 performance and reliability audit

Date: 2026-08-04

## Patched findings

| Area | Finding | Release action |
|---|---|---|
| Android crypto | Cipher, header, nonce, ciphertext, and datagram arrays were allocated repeatedly in the 10 ms loop | Reusable stream-scoped encryptor and packet buffer |
| Android UI | Input level/state was updated too frequently | Meter and packet telemetry reduced to 10 Hz |
| Android capture | Recorder buffer had an unconditional 80 ms floor | Use Android minimum or four 10 ms packets, whichever is larger |
| Android reads | A short positive read could have produced a partial packet | Fill exactly 480 samples before encryption/send |
| Android compatibility | A source could initialize but still fail when recording actually started; optional effects can also fail by device | Start-test each source in order, fall back on failure, and keep optional effects non-fatal |
| Android lifecycle | Cancellation could wait behind a blocking microphone read, and teardown could call service APIs after destruction | Stop active `AudioRecord` when cancelling and guard post-destroy cleanup |
| Android restart | A cancelled but unfinished stream could overlap a replacement job | Reject another start until the current job fully completes |
| Android UDP | A connected UDP socket could fail when the PC port was not open yet | Unconnected datagrams keep transmitting until the receiver starts |
| Android addressing | A hostname could resolve to IPv6 while the Windows listener is IPv4-only | Select an IPv4 result explicitly or show a clear error |
| Android configuration | A newly generated default key was not persisted until Start was pressed | Persist it on first load |
| Android notification | Foreground notification used an unsuitable full-colour launcher asset | Added a monochrome status-bar microphone icon |
| Windows allocations | Packet buffers and UI strings/closures were recreated too frequently | Reuse loop-scoped arrays and only construct UI updates after the 250 ms throttle is claimed |
| Windows validation | Unexpected packet lengths reached deeper parsing/decryption | Require the exact 1000-byte datagram first |
| Windows startup | Playback began against an empty buffer | Wait for a 40 ms prebuffer |
| Windows packet order | Naive ordering could fail around unsigned sequence rollover | Modular uint32 delta handling |
| Windows packet loss | Missing packets could produce discontinuities | Insert bounded silence for gaps up to 10 packets |
| Windows drift | Queue growth could approach the 300 ms provider limit | Drop oldest complete packets above 140 ms |
| Windows resync | Restart/session changes could interact with asynchronous `Stop` behaviour | Pause, clear, and re-prebuffer |
| Windows output failure | Device removal could silently stop audio while packets kept arriving | Handle `PlaybackStopped` and shut down with an error |
| Windows cleanup | Concurrent stop/fault/close paths and queued failures from an old run could affect a replacement run | Shared async stop task, restart blocking during teardown, output-instance checks, and run-generation guards for both faults and queued telemetry |
| Build scripts | Android release path assumed the wrong unsigned APK name | Accept Gradle's signed or unsigned release filename |
| Build scripts | Native publish failure was not explicitly checked | Check `$LASTEXITCODE` and expected executable |
| Firewall | Helper was fixed to one port and did not repair an existing rule | Parameterized, idempotent private-profile rule |

## Hot-path result

The production packet format remains 1000 bytes. The reusable Kotlin encryptor compiled independently and completed an AES-GCM encrypt/decrypt smoke test. On repeated JVM smoke runs it processed well over 100,000 packets per second; the application needs 100 packets per second. That number is only a source-level headroom check—not an Android device latency benchmark.

## Deliberate non-changes

- No Opus dependency was added. PCM remains easier to inspect and isolate during first-device testing.
- No aggressive 10–20 ms Windows target was claimed. Driver, virtual-cable, Wi-Fi, and handset scheduling must be measured first.
- No adaptive jitter controller was added. The fixed 40/140 ms policy is bounded and understandable for the initial field test.

## Remaining validation risk

No static review can guarantee zero bugs. The remaining meaningful risk is integration behaviour that only appears with real Android microphone hardware, Windows audio drivers, Wi-Fi power management, firewall policy, and VB-CABLE. The package is now structured to expose those failures rather than silently hiding them.

## Addendum — 2026-09-04 production-release pass

The audit's "deliberate non-changes" (fixed 40/140 ms policy) were superseded by real-device
measurement: the receiver now ships a 100 ms prebuffer, a 220 ms high-water mark (derived
from the prebuffer, adjustable 40–300 ms), and a 20-packet (200 ms) concealment ceiling —
see `AudioPipeline.cs`. This pass additionally:

- removed the software NS/AGC/AEC stacked on the VOICE_COMMUNICATION source (the double-DSP
  that made the default path hissy), leaving exactly one processing layer in Voice mode;
- moved sending off the capture coroutine into a bounded drop-oldest queue, so a socket
  stall can no longer overrun the 40 ms hardware buffer, and added capture-overrun /
  read-stall / send-stall / drop telemetry to the diagnostics screen;
- replaced the constant Wi-Fi lock mode with a network-fed policy (band + RSSI + receiver
  loss), re-evaluated while streaming and on reconnect;
- closed the discovery replay hole by validating the probe-nonce echo, and moved every
  liveness timeout to the monotonic clock;
- fixed `build-android.ps1` to strip stray PATH quotes that killed Gradle test workers.

Hardware confirmation of the two symptom fixes remains the one unclosed item; the new
diagnostics counters are the measurement instrument for it.
