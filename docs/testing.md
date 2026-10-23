# Verification and acceptance

Status: current guide, reconciled 2026-10-15. Counts and receipts belong to exact candidates in [release records](releases/0.1.7/release.md); the checklists below are not assertions that tests have just run.

## Choose checks by change

- **Documentation:** `python tools/verify_docs.py`, `git diff --check`, and a review of affected package/script references. No full app rebuild is needed for ordinary text/link moves.
- **Version/release metadata:** `python scripts/verify_release_versions.py` plus relevant artifact validation and uploaded asset checks.
- **Protocol or shared receiver logic:** `python tools/verify_protocol.py` and `python tools/verify_receiver_logic.py`, then affected C#/Kotlin/Swift tests. Preserve [shared vectors](../tests/fixtures/vectors.json); inspect [the generator](../tests/gen_vectors.py) before regenerating fixtures.
- **Android:** use `scripts/build-android.ps1` for build, lint and JVM tests; add device checks for permission/service/network behaviour.
- **Windows engine/UI:** run the Release test suite with pinned native Opus staged as described in [development.md](development.md). Exercise the changed installed UI behaviour separately.
- **Windows packaging/installer:** complete modern/classic publish, resource/ZIP/hash checks and `scripts/test-windows-installer.ps1`. Install or update a user machine only within the authorized task.
- **Apple:** existing hosted simulator/protocol/build/localization/package jobs; see the [Apple plan](plans/0.1.8-apple.md) for physical acceptance.
- **Website:** `node --test scripts/site-release-contract.test.mjs`, `node scripts/validate-site-release.mjs`, affected JavaScript syntax checks and exact-payload desktop/mobile review. Verify stable remains the default and prerelease opt-in links retain their own versions and hashes. `tools/verify_web.py` applies only to the legacy demo.
- **Localization:** use the validators in [localization/](../localization/README.md) for the affected platform, followed by fluent/layout review before support claims.

The [CI job definitions](../.github/workflows/ci.yml) are the authoritative Android/Windows command sequence. The retired 0.1.4 source/hash checker is not a substitute for these tests.

## Physical audio checklist

Record candidate commit, app/build versions, artifact hashes, sender/receiver model and OS, codec, destination application, network conditions and actual session duration. Redact pairing keys and private details. Label each result `PASS`, `FAIL`, `NOT TESTED` or `BLOCKED`, with evidence and the missing prerequisite where applicable.

1. **Pairing and readiness:** permission grant/denial, correct/wrong key, QR/manual selection, custom ports, stale saved endpoints and receiver restart. Android auto-connect toggles must clear stale derived state while preserving credentials. Test Windows AutoListen on and off. Discovery must not start microphone capture.
2. **Delivery and lifecycle:** audible input/metering in the named destination app, repeated start/stop, supported background behaviour, disconnect/reconnect and a sustained session. Listening sockets, packet counts and a readiness indicator are separate from audible success.
3. **Codec and timing:** verify the codec actually used; test Windows PCM and Opus, Apple PCM and Mac's Opus incompatibility warning. Measure end-to-end latency, loss/jitter recovery, clock drift and power behaviour with a stated method before publishing performance claims.
4. **Windows controls:** manual buffer ownership versus adaptive effective target; live monitor-device changes and failed-open retention; explicit desktop DSP preset versus phone Custom settings; matching cable endpoints and all Windows default roles; restoration that preserves another user's/app's later changes; report export during active packets, cancel and error paths.
5. **Desktop lifecycle and appearance:** Start/second-launch activation, persistent tray, X hiding and explicit Exit; modern/classic selection; light/dark, high contrast, disabled transparency, older Windows, keyboard access and mixed-DPI layouts. Owner confirmations from 0.1.7 establish only their recorded scenarios.
6. **Apple driver and routing:** use [Mac setup](../macos/README.md) and the [tester report](../macos/TESTER-REPORT.md) for install, registration, selected-app input, underrun, reconnect, guarded uninstall and recovery.

For Android-to-Mac PCM acceptance, run longer than the former approximately 65-second timeout (at least 90 seconds). Check authenticated delivery feedback at the default and a custom audio port, then stop the Mac receiver and verify Android still enters reconnecting and eventually times out. A wrong key, rejected/replayed packet or unsupported Opus stream must not refresh delivery confirmation. Verify stop/restart does not send feedback from the previous receiver run. Mac feedback establishes authenticated reception only; separately record audible input in the destination app and driver routing.

Protocol tampering/replay rejection belongs to deterministic fixtures unless a dedicated physical test procedure is explicitly approved. Do not improvise packet injection into an ordinary user session.

## Release evidence

Freeze the candidate, then record commands/results, CI URLs, exact artifacts/digests, signing state, installation evidence and remaining physical gates. A UI source review, passing build, successful install, owner visual acceptance and measured latency are different claims. A failed or empty optional model review is not an approval. Never transfer evidence from an older binary solely because the product name or version string matches.

Historical detail remains in the [archived test plan](archive/0.1.x/TEST_PLAN.md), [manual checklist](archive/0.1.x/MANUAL-TESTING-0.1.6.md), [build evidence](archive/0.1.x/BUILD-EVIDENCE.md) and [control review](archive/0.1.x/DESKTOP-CONTROL-REVIEW.md). These are context for the scenarios above, not fresh coverage maps or active defect lists.
