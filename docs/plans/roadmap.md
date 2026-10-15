# Current roadmap

Status: active. Reconciled 2026-10-15 against release source `3ef192e` and the completed 0.1.7 release/installation receipts. This is the single current sequencing document; earlier milestone numbers are historical.

## Completed baseline: 0.1.7 development prerelease

The previous source candidate is preserved as `v0.1.6`; the consolidated release is `v0.1.7`. Branch consolidation, Windows/Android packaging, release publication and the recorded installations are completed work. Do not repeat them because a queued continuation or an optional review failed.

The [release record](../releases/0.1.7/release.md) owns the implementation changes, reviewer dispositions and dated test evidence. It includes modern Windows Mica, classic fallback, shared Android-derived icons, tray/single-instance activation, complete XAML/runtime/Opus packaging, installer preservation, and Android pairing/discovery/endpoint/DSP fixes. Owner-confirmed Mica and tray/Start behaviour remain acceptance for that exact unchanged build.

## Next bounded milestone: 0.1.8 Apple development delivery

Follow the [Apple plan](0.1.8-apple.md). Reuse the existing simulator/unsigned archive and universal Mac app/driver workflows. The next work is physical interoperability, driver install/routing/recovery, reproducible tester delivery and explicit signing decisions. Do not describe the existing CI artifacts as a future implementation task, or change app versions until preparing an actual candidate.

## Acceptance still open

These are unresolved validation scopes, not a claim that all corresponding features are broken:

- Physical Android/iPhone to Windows/Mac PCM interoperability; Windows Opus audio and sustained-session behaviour.
- Measured end-to-end latency, audible destination-app routing, clock drift, jitter/loss recovery and battery/network policy behaviour on named devices.
- Windows live audio controls: manual/automatic buffering, monitor changes, DSP preset ownership, reversible cable routing and active-session report export. Earlier source fixes and tests do not close every physical scenario.
- Windows high contrast, transparency disabled, older Windows, mixed DPI, keyboard/accessibility and extended sessions. Preserve existing owner visual/tray confirmations without expanding their scope.
- Locale fluency, RTL/text expansion and platform coverage; use [catalog status](../../localization/README.md), not the target count, for support claims.
- Marketing deployment: `dev/v3/` is the CI candidate; source integration does not prove its deployment. Reconcile its actual release metadata, inspect the exact payload and obtain the required visual/live checks for any future deployment.

Track results using [testing.md](../testing.md). Record `NOT TESTED` or `BLOCKED` with a concrete prerequisite; do not inherit a historical pass from a different candidate.

## Synthesis of older work

- **Completed or superseded:** old branch consolidation/release preparation, native Opus packaging, the identified transport/desktop fixes incorporated in 0.1.7, and the move from a classic Mica border to a modern full canvas. See [release dispositions](../releases/0.1.7/release.md).
- **Still open:** the physical audio, Mac driver, locale and marketing acceptance items above. The [old master plan](../archive/0.1.x/MASTER-PLAN.md) retains detailed D1–D5 and discovery scenarios; [testing.md](../testing.md) is the current checklist.
- **Historical evidence:** [build receipts](../archive/0.1.x/BUILD-EVIDENCE.md), [performance audit](../archive/0.1.x/PERFORMANCE_AUDIT.md), and the [0.1.6 manual checklist](../archive/0.1.x/MANUAL-TESTING-0.1.6.md) identify their original artifacts, not current binaries.
- **Proposals requiring revalidation:** the old [LinkQuality integration plan](../archive/0.1.x/PLAN-LinkQualityPolicy-Integration.md), [research synthesis](../archive/0.1.x/RESEARCH-SYNTHESIS.md) and [implementation handoff](../archive/0.1.x/LUNA-IMPLEMENTATION-HANDOFF.md). Do not apply their code snippets or stale line numbers as a fresh patch list.
- **Retired verification:** the 0.1.4 source-string/checksum checker and manifest are [archived together](../archive/0.1.x/legacy-verification/README.md). Current validators and platform tests replace their use as release gates.

## Deferred product ideas

Preserve the intended later Android consumer/client role and Linux receiver direction. Choose pairing/roles, Linux audio backend, packaging and acceptance criteria before implementation. Other older proposals—USB transport, Oboe capture, new DSP, multi-device/recording features, PocketMic-owned Windows virtual routing, production signing, automatic updates and community tooling—remain a backlog, not commitments to obsolete 0.2/0.3/1.0 version numbers.

Changes to crypto or nonce construction require a concrete threat/compatibility case and independent validation. Old research enthusiasm, compression estimates and review prompts are not evidence of a current defect or an approved redesign.

## Separate repository-hygiene follow-up

Before deleting or restructuring `web/`, `web-v2-compare/`, ignored version copies or any registered worktree, inventory callers/assets, dirty state, unique commits, active use and backups. Preserve `release/` receipts and rollback material. No website relocation, worktree removal or cache purge is part of the documentation cleanup.
