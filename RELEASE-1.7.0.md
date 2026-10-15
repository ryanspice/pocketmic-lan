# PocketMic LAN 1.7.0

Release scope approved by the owner on 2026-10-14; prepared 2026-10-15.

## Sequence and preserved work

- Archive the previous development candidate (`d5cfe74`) as `v0.1.6`. This is a source baseline, not a retroactive claim of signed installers or device acceptance.
- Consolidate the newer Apple/control/audio branch and the master QR/discovery fixes into `main`. Old review branches already represented by cherry-picks are recorded as merged without reintroducing their superseded implementations.
- Preserve the detached workspace patch and a pre-consolidation Git bundle locally. The detached startup/native-library fixes are included or superseded; its original worktree remains untouched. The sole untracked old-worktree plan matches the tracked plan byte-for-byte.
- Publish 1.7.0 as a development prerelease after the frozen candidate passes hosted Android, Windows, iOS, macOS and website checks. All app versions are 1.7.0; Android and Apple app builds are 10700, and the Mac driver uses dotted build 1.7.0. Apple remains a development preview.
- Target 1.7.1 for Apple development delivery: simulator and unsigned archive builds first, then physical iPhone/Mac interoperability, Mac driver install/uninstall/routing/recovery and explicit signing decisions.

## Changes

- Windows 11 22H2+ WinUI 3 receiver with a Mica canvas, transparent page containers, native Fluent controls and system theme resources. Older Windows retains the classic interface; `--classic --show` selects it explicitly.
- Android-derived Windows executable, tray and shortcut icons. The tray stays present; X hides the window, and subsequent launches restore the same receiver through current-user/session activation. Explicit Exit stops audio and restores owned microphone assignments.
- Per-user Windows installer checks package hashes, preserves settings and the previous installation, and creates Desktop/Start Menu shortcuts. Explicit opening uses `--show`; configured minimized startup remains supported.
- Windows package includes pinned Opus 1.5.2 and licence notices. Receiver shutdown waits for its receive tasks before disposing the decoder.
- Combined Available PCs, QR permission flow, custom-port discovery, fresh authenticated announcements, persisted codec choice and the newer control/audio fixes.
- Android external pairing links require confirmation before replacing stored connection settings. `singleTop` delivers subsequent links to the existing activity. Scanning a QR code inside the app remains a deliberate pairing action.
- Refresh PCs restarts probing and retries failed control-socket initialization. Audio-socket ownership is established before network binding so failure cleanup closes it.
- During capture, the service owns the active receiver: reconnect changes the control port and audio endpoint together, uses an atomic host/port snapshot, and accepts statistics from the adopted endpoint. Stopping discovery after recovery preserves delivery confirmation until its normal timeout.
- Phone DSP updates also use the service-adopted receiver and port after discovery stops or a custom-port recovery. Queued updates recheck key, socket and destination before sending so stale UI fields cannot route them to the previous endpoint.
- Windows address lists scroll, and copying the first IP reports success or a busy clipboard instead of silently failing.
- Empty diagnostics correctly say they are waiting for audio, including when the receiver is already listening.
- A manually selected PC can recover to a new authenticated, compatible port on the same host. Exact endpoint selection still wins when several receivers share a PC; manual selection never jumps to a different host.
- A LAN-network binding failure during Wi-Fi handover keeps the already-bound control socket usable on the default route. Actual port failures still close the socket and report an error.
- Installer shutdown waits are bounded, and inaccessible unrelated same-named processes do not abort an update before activation.
- Shared platform-version validation runs in CI. Legacy source-checksum/static-string checks remain historical and are not counted as current verification.

## First review synthesis

- Sol 6.1 high: found no previous Mica implementation in tracked history. The installed-app visual check caught GDI text disappearing with full-client glass; the correction limits glass to a DPI-scaled 12px gutter, preserves an opaque client center and adds a bitmap regression assertion. The corrected installed window was visually checked at normal and maximized sizes.
- Sol 6.1 high follow-up: independently confirmed the final DSP routing issue and implemented the bounded fix with four focused destination-resolution regressions. Those tests do not claim physical slider delivery or exhaustive asynchronous race coverage.
- Space Bunny: accepted pairing consent, activity intent routing, refresh and socket-cleanup findings. Automatic relaxation of a manually pinned receiver is deferred: a manual receiver choice must not silently select another PC.
- Cat Jam: accepted staging-cleanup and clear shortcut-failure reporting. Rejected allowing the entire release directory as a publish output: cleanup must remain restricted to generated children so backups and evidence are preserved.
- CodeCraft Opus 5.5: supplied-source review completed after filesystem tooling failed. Accepted address scrolling and copy feedback; retained the deliberately labelled "Copy first IP" behavior. Further DPI/layout claims require visual reproduction before changes.
- AgentRouter DeepSeek 4: accepted stale macOS verifier versions and added a tag/version gate. Rejected its driver-build mismatch claim: the driver explicitly uses the same dotted version for both plist fields. A future 1.7.1 plan does not change today's 1.7.0 artifact identity.
- GLM 5.3/NVIDIA: the first tool-based review did not return a final result within its bounded run. A second, smaller supplied-source task terminated with "completed without an assistant result." Neither attempt is counted as a completed review or passing evidence.
- Apodex history was recovered, but the exact OpenRouter route has no active endpoints. No selector-only workaround is treated as restored service.
- [ChatGPT Pro release synthesis](https://chatgpt.com/g/g-p-6ab5e6a094d08191b68296fa49d39670-app-pocketmic-lan/c/6ad05202-5440-83ea-82b4-384e5bac0d1a): completed through the existing signed-in Edge session after T3 preview automation failed. Accepted the explicit development-release contract, exact-commit CI gate, Android identity/signing instructions, installer rollback testing and distinct physical-audio/Apple acceptance gates. This was a supplied-evidence synthesis, not a full source audit.

## Second review synthesis

Fresh, bounded reviews examined the 1.7.0 candidate at `5fc174e`, including the first findings, their dispositions and unresolved objections. Review claims were checked against source and deterministic tests before integration.

- **Space Bunny / Android:** accepted same-host authenticated port recovery and control-network binding failure isolation. The proposed single selection predicate would have regressed exact-port preference when multiple instances share a PC; integration instead preserves exact endpoint priority before considering a compatible authenticated port on the same host. The final patch was independently rereviewed with no confirmed defect. Both low-confidence test-compilation questions were resolved by the successful 105-test build. Cross-PC switching for manual/QR selections remains prohibited. A generic dual-Wi-Fi/cellular-routing allegation was not reproduced and was withdrawn by the reviewer.
- **Cat Jam / Windows installer:** accepted bounded process-exit waits and tolerance of inaccessible unrelated same-named processes. Fault injection covers both, alongside activation rollback and corruption rejection. Rejected deleting an unexpected destination during rollback: activation is a same-volume sibling-directory move, no partial-copy reproduction was supplied, and deleting a newly appearing directory could destroy unrelated data. The previous-install backup remains preserved.
- **CodeCraft Opus 5.5 / Mica:** independently identified the stale native frame on HRESULT failure. Sol 6.1 high produced failing regressions, then implemented the bounded cleanup and retained-gutter fix. Root reviewed integration and ran the full Windows suite. See the separate Mica record below for actual-native versus injected versus visual evidence.
- **AgentRouter DeepSeek v4 Flash / Apple:** no confirmed new defect in the supplied release-version verifier, Mac artifact verifier and iOS/macOS workflows. Python optimization guards and an extra iOS artifact-version check were suggestions, not reproduced release failures; retain them for the 1.7.1 hardening pass. Its hypothetical ZIP symlink concern does not demonstrate a write-through in Python's current extraction path and is not counted as a confirmed bug.
- **GLM 5.3 / NVIDIA:** a third, narrowly scoped source-only attempt still supplied no final review and was stopped. This is not a successful review or an approval. Apodex remains unavailable at the previously checked route; OpenJEV was not present in the available routing catalog.

## Windows Mica

[Dedicated implementation, defect reproduction and verification record](WINDOWS-MICA-1.7.0.md). The final modern interface uses a native WinUI Mica backdrop across the window canvas. Native Fluent controls and translucent theme-resource cards replace the earlier WinForms body. The classic fallback retains its bounded native Mica gutter and opaque controls.

Owner feedback on `29e7ae9` identified readable controls but no visible Mica despite successful API tests. A native 32-bit DIB reproduction found the gutter was painted opaque by GDI+, masking the material. Native zero-alpha gutter painting corrected that fallback; sixteen Mica cases exercise the actual native paint boundary. The owner then requested a full native Mica canvas, leading to the WinUI front end. Its first installed launch exposed missing published XAML resources; explicit app XBF/PRI publishing and fail-fast package checks corrected the crash. The owner confirmed the corrected appearance and subsequently confirmed that closing leaves the tray icon and Start Menu activation restores the window.

## Final Windows review and integration

- The native Windows implementation agent and a separate independent reviewer examined startup, SHOW/EXIT dispatch, stop coordination, monitor transitions, reversible routing and stale queued updates. The current native review agents used the inherited Astra runtime; these rounds are not claimed as additional Sol or Opus runs.
- Root review corrected clipboard/QR failure handling, explicit SHOW during auto-start, successful monitor-device persistence, queued updates after stop and restart during teardown. The independent source pass found no further actionable defect; installed execution then exposed and verified the publish-resource correction.
- Activation tests reproduced disconnected-client listener poisoning and callback-failure lock handling. Both are corrected; the full Windows suite now has 211 passing tests. Live SHOW returned the exact OK acknowledgement, a second installed launch retained the existing process, and graceful EXIT completed before replacing the development preview.
- The package includes the modern runtime recursively, verifies hashes and safe relative paths, retains the previous installation, and requests graceful installed-app shutdown before bounded legacy handling. Fault injection covers nested staging, unsafe paths, corruption, rollback and shutdown behavior.

## Build and installation evidence

Local Windows Release tests: 211 passed, including native Opus, activation lifecycle and 16 classic Mica cases. Local Android JVM tests: 105 passed; lint and all three native ABI builds passed with the transport and DSP fixes. Full modern/classic publishing and installer fault-injection checks passed. Version, locale, protocol and website checks passed. The release tag is gated on all hosted workflows passing at the final commit; the GitHub release records that commit and its build receipts. Local logs and artifacts are under the ignored `release/audit-1.7/` and `release/` directories.

The Windows install preserves `%APPDATA%\PocketMic\receiver.json`. Extract the Windows ZIP and run `Install-PocketMic.cmd`; the installer creates Desktop and Start Menu shortcuts. Android uses `com.canopydigital.pocketmic`, which installs separately from v0.1.5's `com.ryanspice.pocketmic`; stop the old transmitter and pair the new app because settings are not migrated automatically. The release APK is debug-signed: independently built debug APKs may use different signing certificates and cannot necessarily update it. The Windows receiver is unsigned. Hashes detect corruption; they do not replace code signing.

## Open acceptance gates

- Authenticated PCM packet delivery and phone delivery confirmation were observed on the connected OnePlus and Windows receiver. Audible target-app/VB-CABLE routing, sustained Opus audio, prolonged screen-locked sessions and measured latency still require physical receipts.
- Apple simulator/build/protocol/package checks do not establish physical installation or live virtual-microphone routing. TestFlight, App Store and notarization remain unconfigured.
- Kurdish drafts and newly added English fallback pairing text require fluent review; Windows localization is not implemented.
- The marketing site still describes the previous published release. This app release does not claim a website deployment or translated-site acceptance.
- Real high contrast, transparency-disabled settings, older Windows, mixed-DPI monitors and keyboard/screen-reader acceptance remain manual Windows checks. Mica and tray/Start behavior were confirmed by the owner; no automated final screenshot is claimed after Computer Use was stopped with Escape.
