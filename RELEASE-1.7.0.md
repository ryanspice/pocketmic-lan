# PocketMic LAN 1.7.0

Release scope approved by the owner on 2026-10-14; prepared 2026-10-15.

## Sequence and preserved work

- Archive the previous development candidate (`d5cfe74`) as `v0.1.6`. This is a source baseline, not a retroactive claim of signed installers or device acceptance.
- Consolidate the newer Apple/control/audio branch and the master QR/discovery fixes into `main`. Old review branches already represented by cherry-picks are recorded as merged without reintroducing their superseded implementations.
- Preserve the detached workspace patch and a pre-consolidation Git bundle locally. The detached startup/native-library fixes are included or superseded; its original worktree remains untouched. The sole untracked old-worktree plan matches the tracked plan byte-for-byte.
- Publish 1.7.0 as a development prerelease after the frozen candidate passes hosted Android, Windows, iOS, macOS and website checks. All app versions are 1.7.0; Android and Apple app builds are 10700, and the Mac driver uses dotted build 1.7.0. Apple remains a development preview.
- Target 1.7.1 for Apple development delivery: simulator and unsigned archive builds first, then physical iPhone/Mac interoperability, Mac driver install/uninstall/routing/recovery and explicit signing decisions.

## Changes

- Windows 11 22H2+ Mica title bar and gutter with high-contrast/older-Windows fallback; opaque native controls remain readable.
- Per-user Windows installer checks package hashes, preserves settings and the previous installation, and creates Desktop/Start Menu shortcuts. Explicit opening uses `--show`; configured minimized startup remains supported.
- Windows package includes pinned Opus 1.5.2 and licence notices. Receiver shutdown waits for its receive tasks before disposing the decoder.
- Combined Available PCs, QR permission flow, custom-port discovery, fresh authenticated announcements, persisted codec choice and the newer control/audio fixes.
- Android external pairing links require confirmation before replacing stored connection settings. `singleTop` delivers subsequent links to the existing activity. Scanning a QR code inside the app remains a deliberate pairing action.
- Refresh PCs restarts probing and retries failed control-socket initialization. Audio-socket ownership is established before network binding so failure cleanup closes it.
- During capture, the service owns the active receiver: reconnect changes the control port and audio endpoint together, uses an atomic host/port snapshot, and accepts statistics from the adopted endpoint. Stopping discovery after recovery preserves delivery confirmation until its normal timeout.
- Windows address lists scroll, and copying the first IP reports success or a busy clipboard instead of silently failing.
- Empty diagnostics correctly say they are waiting for audio, including when the receiver is already listening.
- Shared platform-version validation runs in CI. Legacy source-checksum/static-string checks remain historical and are not counted as current verification.

## Reviewer disposition

- Sol 6.1 high: found no previous Mica implementation in tracked history. The installed-app visual check caught GDI text disappearing with full-client glass; the correction limits glass to a DPI-scaled 12px gutter, preserves an opaque client center and adds a bitmap regression assertion. The corrected installed window was visually checked at normal and maximized sizes.
- Space Bunny: accepted pairing consent, activity intent routing, refresh and socket-cleanup findings. Automatic relaxation of a manually pinned receiver is deferred: a manual receiver choice must not silently select another PC.
- Cat Jam: accepted staging-cleanup and clear shortcut-failure reporting. Rejected allowing the entire release directory as a publish output: cleanup must remain restricted to generated children so backups and evidence are preserved.
- CodeCraft Opus 5.5: supplied-source review completed after filesystem tooling failed. Accepted address scrolling and copy feedback; retained the deliberately labelled "Copy first IP" behavior. Further DPI/layout claims require visual reproduction before changes.
- AgentRouter DeepSeek 4: accepted stale macOS verifier versions and added a tag/version gate. Rejected its driver-build mismatch claim: the driver explicitly uses the same dotted version for both plist fields. A future 1.7.1 plan does not change today's 1.7.0 artifact identity.
- GLM 5.3/NVIDIA: the first tool-based review did not return a final result within its bounded run. A smaller supplied-source review was requested; no unfinished review is counted as a pass. Any later findings require independent source verification.
- Apodex history was recovered, but the exact OpenRouter route has no active endpoints. No selector-only workaround is treated as restored service.
- [ChatGPT Pro release synthesis](https://chatgpt.com/g/g-p-6ab5e6a094d08191b68296fa49d39670-app-pocketmic-lan/c/6ad05202-5440-83ea-82b4-384e5bac0d1a): completed through the existing signed-in Edge session after T3 preview automation failed. Accepted the explicit development-release contract, exact-commit CI gate, Android identity/signing instructions, installer rollback testing and distinct physical-audio/Apple acceptance gates. This was a supplied-evidence synthesis, not a full source audit.

## Build and installation evidence

Local Windows Release tests: 179 passed, including native Opus, Mica lifecycle and opaque-center painting checks. Local Android JVM tests: 96 passed; lint and all three native ABI builds passed with the transport fixes. Installer fault injection confirmed that failed activation restores the previous installation and removes its staging directory; a corrupt payload is rejected before activation. Version, locale, protocol and website checks passed. The release tag is gated on all hosted workflows passing at the final commit; the GitHub release records that commit and its build receipts. Local logs and artifacts are under the ignored `release/audit-1.7/` and `release/` directories.

The Windows install preserves `%APPDATA%\PocketMic\receiver.json`. Extract the Windows ZIP and run `Install-PocketMic.cmd`; the installer creates Desktop and Start Menu shortcuts. Android uses `com.canopydigital.pocketmic`, which installs separately from v0.1.5's `com.ryanspice.pocketmic`; stop the old transmitter and pair the new app because settings are not migrated automatically. The release APK is debug-signed: independently built debug APKs may use different signing certificates and cannot necessarily update it. The Windows receiver is unsigned. Hashes detect corruption; they do not replace code signing.

## Open acceptance gates

- Physical end-to-end PCM/Opus audio, target-app/VB-CABLE routing, prolonged screen-locked sessions, reconnect behavior and measured latency require device receipts. Installation, process startup and unit tests do not establish those outcomes.
- Apple simulator/build/protocol/package checks do not establish physical installation or live virtual-microphone routing. TestFlight, App Store and notarization remain unconfigured.
- Kurdish drafts and newly added English fallback pairing text require fluent review; Windows localization is not implemented.
- The marketing site still describes the previous published release. This app release does not claim a website deployment or translated-site acceptance.
- A full WinUI/Fluent redesign is outside the targeted Mica correction; keep further UI improvements bounded by live visual/accessibility acceptance.
