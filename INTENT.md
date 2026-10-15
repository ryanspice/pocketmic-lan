# PocketMic LAN intent

Status: current product direction, reconciled 2026-10-15. Implementation and release status live in the [README](README.md) and [roadmap](docs/plans/roadmap.md).

## Purpose

Make a phone useful as a microphone for a desktop application over a trusted private LAN. Pairing, connection state, routing and recovery should be understandable without an account or a PocketMic cloud relay. This describes the PocketMic audio hop; a destination application may still use its own internet services.

## Product priorities

1. Correct audio transport, authenticated state changes and safe start/stop/recovery come before added features.
2. Users retain control of receiver selection, microphone permissions, settings and audio routes. Preserve their choices across updates and restore only routes the application still owns.
3. Desktop integration should feel native: a persistent tray entry, predictable activation and a complete installable package. On supported Windows, use a WinUI Mica canvas with native Fluent controls and transparent page containers; retain a usable classic fallback.
4. Cross-platform reach must have explicit compatibility boundaries. Android/Windows is the established development-release path; iOS/macOS are development previews. PCM provides the current shared Apple audio path. Publish only support claims justified by the relevant tests.
5. Localization is a staged product capability: target, translated, reviewed and supported are different states. Keep Canadian English as the source/fallback and obtain fluent/layout review before claiming support.
6. Prefer maintainable, bounded improvements and existing platform facilities. Measure latency, quality and reliability before making performance claims or replacing working audio architecture.

## Current boundaries

- Private-LAN voice use; no public-internet receiver or relay service.
- Windows destination-app routing currently uses a separately installed virtual cable. Do not describe that third-party route as a shipped PocketMic-owned Windows driver.
- Build success, a simulator run and protocol fixtures do not establish physical microphone routing or sustained audio quality.
- Unsigned/debug packages and CI previews are not store distribution or notarized releases.
- Linux, an Android consumer role, USB transport, new capture backends and additional DSP remain future work subject to the roadmap and explicit scope decisions.
- No automatic updater, analytics or telemetry project is authorized by this intent document.

## Change control

The [roadmap](docs/plans/roadmap.md) owns sequencing and deferred ideas; the [Apple plan](docs/plans/0.1.8-apple.md) owns the next bounded milestone. Preserve historical rationale in [the archive](docs/archive/0.1.x/README.md), but reconcile it against current source before adopting it. Update this document when the owner changes product direction, not for every implementation detail or test run.
