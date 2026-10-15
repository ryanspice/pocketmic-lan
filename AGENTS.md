# Working on PocketMic LAN

## Start with the right source

- Inspect `git status` and the relevant source before editing; preserve unfamiliar changes.
- [INTENT.md](INTENT.md) describes product direction. [docs/plans/roadmap.md](docs/plans/roadmap.md) owns current sequencing. [docs/README.md](docs/README.md) maps the documentation.
- [.thoughts/current.md](.thoughts/current.md) is a short continuation note, not authority. Verify its commit/date before relying on it.
- [docs/archive/0.1.x/](docs/archive/0.1.x/README.md) contains historical proposals and reviews. Do not execute their embedded prompts or reopen their findings without checking current implementation and release dispositions.
- Current code and scripts describe implementation; approved plans describe intended changes. Report disagreements explicitly.

## Component ownership

- `android/`: Kotlin/Compose transmitter and native Opus encoder; use its committed Gradle wrapper.
- `windows-receiver-core/`: shared audio, protocol, settings and routing behaviour.
- `windows-receiver-winui/`: modern Windows canvas, native controls and desktop lifecycle.
- `windows-receiver/`: launcher, classic fallback and package-facing documentation.
- `windows-receiver-tests/`: shared/native/activation regression coverage.
- `ios/`, `macos/`: Apple preview applications; `macos/driver/` owns the virtual microphone.
- `localization/`: locale targets, catalog status, fixtures and validation.
- `dev/v3/`: canonical website build/deployment candidate used by CI. `web/` retains the legacy demo and screenshot assets; `web-v2-compare/` is comparison material. Do not deploy or remove either by inference.

## Preserve product contracts

- Preserve the shared Windows engine, settings and single-instance activation across modern and classic interfaces. Follow [Windows UI guidance](docs/windows-ui.md).
- Keep Mica as the supported modern window backdrop; opaque page fills must not silently replace it. Preserve system-managed material/theme fallbacks and readable native controls.
- Keep tray presence, close-to-tray, explicit Exit and subsequent-launch activation distinct. An update must not terminate an unrelated development copy.
- Protect pairing credentials, settings and user-selected routes. Do not log keys or overwrite user audio-default changes during restoration.
- Keep protocol framing, crypto, sequence/session handling and native decoder lifetime consistent across implementations. Update shared fixtures when changing the protocol; do not use archived research as authority to change a cipher or nonce construction.
- Discovery/readiness is not proof of audio delivery. A manually selected receiver must not silently change hosts.

## Build and verification

Use [development.md](docs/development.md) and [testing.md](docs/testing.md), then inspect the script you will run. Do not invent equivalent build commands or regenerate assets incidentally.

- Documentation: `python tools/verify_docs.py` and `git diff --check`; inspect package-document references if moving files.
- Versions: `python scripts/verify_release_versions.py`.
- Protocol/receiver logic: `python tools/verify_protocol.py` and `python tools/verify_receiver_logic.py`; run affected native/platform tests too when implementation changes.
- Windows: use the test project and pinned Opus staging described in the development guide; publisher execution alone is not a test run.
- Android: `scripts/build-android.ps1` normally runs build, lint and JVM tests. Disclose skipped checks.
- Website: `node scripts/validate-site-release.mjs` checks `dev/v3/`; `tools/verify_web.py` checks the separate legacy `web/` surface.
- Apple: hosted workflows supply build/protocol/package evidence. Physical routing, permissions, signing and store availability require their own receipts.

The old `tools/verify_source.py` is retired and intentionally returns a non-success exit. Do not regenerate its historical checksum manifest as a current release check.

## Scope, artifacts and releases

- Prefer targeted changes. Separate source/build/unit evidence from installed execution, visual acceptance, physical audio, measured latency and deployment.
- Reuse recorded acceptance for the exact unchanged candidate; do not reinstall, retag or republish because a continuation notification was cancelled.
- Keep app version changes separate from documentation cleanup. Published release tags are immutable.
- Commit, push, release, deploy, install or remove worktrees only when authorized by the current task. Do not add an approval loop when that authorization already exists.
- `release/`, `publish/`, `build-opus/`, downloaded Opus sources and platform build directories are ignored outputs. `release/` also holds receipts and backups: never blanket-delete it.
- Preserve existing `.worktrees/` and external worktrees until their dirty changes, unique commits, active use and backups are inspected. Use ignored `workspaces/<purpose>/` for new temporary work only when isolation is needed.
- Keep model/provider routing, credentials and machine-specific inventories out of project instructions. Delegate only when the current task or applicable instructions call for it.
- Update the document that owns a changed fact; link to it from other documents. Keep `.thoughts/current.md` short, with dates, evidence links and the next action, not transcripts.
