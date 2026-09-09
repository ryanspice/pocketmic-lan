# PocketMic LAN — marketing, release funnel, and product-proof redesign

**Status:** Proposed; implementation is blocked pending owner confirmation.
**Date:** 2026-09-03
**Working root:** `B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.3`
**Scope:** GitHub/release-link foundation, real app screenshots, canonical website redesign, campaign-funnel integration, responsive/a11y/SEO verification.
**No-live-change rule:** Do not push, publish, create a release, change repository visibility, or deploy during this pass.

## 1. Inputs and established facts

### Current canonical page
- `web/index.html`
- `web/styles.css`
- `web/app.js`
- Existing strengths to retain: warm paper/gold identity, dark mode, claim-safe protocol details, no-cloud/LAN positioning, datagram illustration, current-limits disclosure, lightweight static architecture.
- Current checks pass: balanced HTML, one `h1`, no heading skips, all anchors resolve, required claims are present, forbidden claims absent, CSS-only mobile navigation exists, and `node --check web/app.js` passes.

### Additive funnel inputs
- `B:\Temp\@Browser\phone-mic-funnel.tar.gz`
  - General acquisition funnel: direct benefit hero, three-step mechanism, use cases, segmentation, early-access form, FAQ, thank-you and privacy pages.
- `B:\Temp\@Browser\phone-mic-cat-funnel.tar.gz`
  - Campaign funnel: “My Cat Ate My Mic,” confessional/share loop, referral and UTM attribution, lead capture, campaign thank-you/privacy pages.
- `B:\Temp\@Browser\index(20260903-214226).html`
  - Standalone cat-campaign HTML variant.
- `B:\Temp\@Browser\README(20260903-213610).md`
  - General funnel notes.
- `B:\Temp\@Browser\README(20260903-214228).md`
  - Cat-campaign notes.
- `B:\Temp\@Browser\social-card.png`
  - Strong campaign creative; campaign-only rather than the canonical product card.

### GitHub state
- GitHub CLI is authenticated as `ryanspice` with `repo` and `read:org` scopes.
- `canopydigital/pocketmic-lan` does not currently exist, so the intended name is available at planning time.
- The current `v0.1.3` folder is not presently a Git repository.
- The visible `v0.1.3` tree currently contains the website/Fugu material but no discoverable Android or Windows source tree. DeepSeek is separately scanning/changing core application code; its final working root and changed-file packet must be reconciled before initialization or screenshots.

## 2. Product and content decisions

1. **Canonical site stays product-first.** It should be the durable PocketMic LAN product/release site, not the cat campaign.
2. **Cat concept becomes a campaign route.** Preserve its memorable editorial identity under a clearly separated campaign page such as `web/campaigns/my-cat-ate-my-mic/`.
3. **Use the general funnel as a content/IA donor, not as a wholesale visual replacement.** Bring its clear value proposition, use cases, three-step flow, FAQ, and segmentation ideas into the existing warm-paper/gold system.
4. **Use real product screenshots only.** Do not fabricate Android/Windows UI or present CSS mockups as screenshots.
5. **Keep claims bounded by the shipping implementation.** Unsupported app compatibility, low-latency, virtual-input, pairing, and transport statements stay qualified until the relevant build/test proves them.
6. **Downloads point to GitHub tagged-release assets.** The site may contain the final URLs before a release exists, but local verification must label the expected pre-release 404 state rather than pretending the downloads work.
7. **No fake lead capture.** A production-looking form must not silently save only to `localStorage`. If no endpoint/privacy workflow is approved, use a transparent early-access/demo treatment or omit the form from the canonical page.

## 3. GitHub and local repository foundation

Execute only after confirmation and after DeepSeek's working-root handoff.

1. Re-scan the selected unified source root and obtain DeepSeek's changed-file list so concurrent work is not overwritten.
2. Confirm that `v0.1.3` is the intended unified root. If app source remains in `v0.1.2`, consolidate deliberately into a version-neutral root instead of creating a website-only repository by accident.
3. Prepare a surgical `.gitignore` before staging anything. Exclude SDK caches, Gradle outputs, `.NET` `bin/obj`, generated releases, local Fugu/result scratch, IDE files, browser captures, and credentials; retain source, scripts, docs, and intentional web assets.
4. Create an **empty private repository** without source import or push:
   - `gh repo create canopydigital/pocketmic-lan --private --description "Encrypted Android-to-Windows microphone over a private LAN"`
5. Initialize the approved local root and set:
   - `origin = git@github.com:canopydigital/pocketmic-lan.git`
6. Do **not** add, commit, tag, release, push, publish, or change visibility in this pass.
7. Read back the remote with `gh repo view canopydigital/pocketmic-lan` and `git remote -v` before reporting success.
8. Before public launch, explicitly decide when to make the repo public; direct unauthenticated website downloads will not work while the repository is private.

### Planned link contract
Use one centralized link map in the website JavaScript/data layer or clearly marked constants in static HTML:

- Repository: `https://github.com/canopydigital/pocketmic-lan`
- README / detailed “How it works”: `https://github.com/canopydigital/pocketmic-lan#how-it-works`
- Releases page: `https://github.com/canopydigital/pocketmic-lan/releases`
- Stable Android download: `https://github.com/canopydigital/pocketmic-lan/releases/latest/download/<verified-apk-filename>`
- Stable Windows download: `https://github.com/canopydigital/pocketmic-lan/releases/latest/download/<verified-windows-zip-filename>`
- Version-pinned URLs for release notes/examples: `https://github.com/canopydigital/pocketmic-lan/releases/download/v<verified-version>/<verified-filename>`

Artifact names and version are derived from the actual successful builds after the DeepSeek pass; do not guess `v0.1.3` filenames from the directory name.

## 4. Real app build and screenshot pass

### Coordination gate
- Wait for DeepSeek to finish or provide a stable checkpoint.
- Inventory its touched Android, Windows, protocol, build, and browser-use files.
- Keep website work separate from those files unless a screenshot-critical defect requires an agreed app patch.

### Build verification
1. Locate the authoritative Android and Windows source roots and their documented SDK requirements.
2. Run the repository's local Android build script/Gradle task and record the real APK path/version.
3. Run the Windows publish/build script and record the real executable/ZIP path/version.
4. Run targeted existing tests for protocol/core logic.
5. If either build fails, fix only a narrow, understood blocker after reporting it; do not design screenshots around a non-running fake.

### Screenshot set
Capture clean, reproducible states at native resolution:

- Android: initial setup/pairing screen.
- Android: microphone permission or readiness state where appropriate.
- Android: active streaming/level state, only if genuinely functional.
- Windows: receiver start/pairing view.
- Windows: destination/output selection view.
- Windows: connected/receiving state, only if genuinely functional.
- Optional paired composition: Android and Windows captures shown together in the website, while preserving each original screenshot.

### Capture rules
- Prefer emulator/device `adb` capture for Android and an actual running Windows process for desktop.
- Remove personal IPs, pairing keys, usernames, device IDs, and unrelated desktop content.
- Do not retouch UI behavior or invent status indicators.
- Store optimized website derivatives under `web/assets/screenshots/`; preserve lossless originals outside the shipped web folder.
- Supply width/height, meaningful alt text, lazy loading below the fold, and WebP/PNG fallbacks as appropriate.
- Add a short reproducibility note to the project README documenting build/version/state used for each image.

## 5. Canonical marketing-page redesign

### A. Header
- Keep the PocketMic LAN mark and restrained technical styling.
- Desktop navigation: `How it works`, `Use cases`, `Product`, `Security`, `Install`, `Limits`.
- Add a clear release/download action once links are known.
- Keep version visible on desktop; move it into the mobile menu if the narrow header becomes crowded.
- Add repository/source link where it does not compete with the primary download action.

### B. Hero
- Replace the awkward current headline with a direct, claim-safe Android-to-Windows proposition.
- Lead with the user outcome; move packet format details out of the main lede.
- Primary actions:
  1. Android release asset.
  2. Windows release asset.
- Tertiary text link: `See how it works` → GitHub README `#how-it-works`, per owner direction.
- Show a real Android + Windows product composition.
- Retain a smaller protocol/datagram proof element or move it immediately below the product visual.
- Keep `No account`, `No cloud`, `Private LAN`, and honest readiness status visible.

### C. Three-step workflow
Use the owner-approved sequence, with device responsibility explicit:

1. **Start the receiver** — launch the Windows companion and confirm the listening address/port.
2. **Pair Android** — enter/confirm the receiver and pairing credentials on the phone.
3. **Select the destination** — choose the Windows playback/virtual-cable destination used by Discord, Teams, OBS, games, or another verified app.

Keep the existing seven-step technical instructions in the README/install documentation rather than the marketing flow.

### D. Use cases
Add an outcome-driven strip or section before protocol details:
- Emergency/backup microphone.
- Gaming and voice chat.
- Meetings and remote work.
- Streaming/recording.

Only name applications whose route has been verified. Otherwise label them as intended use cases and link to current limitations.

### E. Product proof
- Pair real screenshots with concise annotations.
- Show setup and destination selection rather than decorative mock UI.
- Include one honest readiness/status badge: e.g. source release, tested build, or physical validation pending—based on the actual post-DeepSeek state.

### F. Feature regrouping
Replace the ten equal cards with grouped capability blocks:
- Android capture.
- Windows receiver and destination routing.
- Private LAN transport.
- Authenticated encryption.
- Stream resilience and controls.
- Source/build verification.

Use varied card spans or a two-column editorial layout so the final row is intentional rather than a lone card.

### G. Performance and protocol
- Keep the datagram visualization and exact wire facts.
- Separate **fixed design parameters** from **measured results**.
- Do not make latency a benefit until measured end to end.
- Collapse secondary protocol detail into a compact disclosure/details region on the marketing page and link to README/protocol documentation for depth.

### H. Security and limitations
- Retain the LAN boundary and do-not-port-forward warning.
- Keep the limits first-class, but shorten their visual treatment.
- Add links to repository protocol/security documentation when those files exist in the new repository.

### I. Install/release section
- Replace build-only conversion with two platform cards:
  - Android APK/release action.
  - Windows receiver ZIP/release action.
- Keep “Build from source” as a secondary path linked to README.
- State checksum/signing/install-warning status exactly as the produced artifacts support.
- Explain the optional VB-CABLE route without implying PocketMic itself exposes a native virtual microphone if it does not.

### J. FAQ and final CTA
- Adapt only verified questions from the general funnel.
- Remove prototype/editorial warnings from public copy.
- End with the two real release actions plus source/README fallback.
- Avoid a long lead form unless a real endpoint, consent record, privacy notice, and abuse controls are approved.

## 6. Campaign-funnel integration

### General funnel
- Treat `phone-mic-funnel` as an IA/content reference rather than a separate public duplicate of the canonical site.
- Merge the best pieces into the canonical page: benefit-led copy, use cases, three-step flow, FAQ, and clear funnel actions.
- Do not import Google Fonts if the canonical zero-third-party/system-font constraint remains preferred.
- Remove unverified claims such as “great mic,” direct selectable virtual microphone behavior, broad application support, and low latency unless current tests prove them.

### “My Cat Ate My Mic” campaign
- Integrate as a self-contained campaign route with shared PocketMic brand tokens where practical.
- Keep the cat/editorial visual system campaign-specific; do not replace the canonical product identity.
- Update the campaign's product reveal to the exact receiver → Android pairing → destination flow.
- Route product CTAs to the verified release assets/repository instead of placeholder early-access language when builds exist.
- Keep referral/UTM capture and Web Share/clipboard behavior only after privacy review.
- Do not ship a localStorage-only form as if it were a real submission.
- Keep the campaign disclaimer that the cat story is POV creative rather than a fabricated founder testimonial.
- Use `social-card.png` for this campaign only; create a separate canonical PocketMic product social card from real product visuals.

### Attached-file update deliverables
After integrating and verifying the project copies:
- Update both attached README documents with the real repository, README, release, and local route references.
- Update `index(20260903-214226).html` links and metadata.
- Update both archive contents consistently, then rebuild deterministic revised archives next to the originals.
- Preserve the original attachments; write revised artifacts with an `-integrated` suffix unless the owner explicitly asks to overwrite them.
- Ensure campaign `og:image` uses an absolute final public URL before launch; keep a clearly marked placeholder while private/local.

## 7. Missing publication essentials

Add or finish:
- Canonical favicon set and web manifest if installability is desired.
- Canonical product social card distinct from the cat card.
- Open Graph/Twitter metadata and canonical URL placeholders.
- README-linked release/install documentation.
- Real privacy/contact details before enabling lead capture.
- `robots.txt` and sitemap only when the deployment hostname is known.
- Accessible skip link and landmarks on campaign pages.
- Download-link state/labels that distinguish unavailable prerelease assets from live assets.
- Optional lightweight UTM attribution with no hidden personal-data collection.

## 8. Responsive and accessibility redesign

- Validate at 320, 360, 390, 500, 768, 1024, 1440, and a wide desktop viewport.
- Use one-column screenshot and fact layouts on narrow phones.
- Prevent long artifact names, commands, AES labels, and datagram legends from forcing overflow.
- Keep every interactive target at least 44×44 CSS pixels.
- Verify keyboard order, mobile navigation, details/FAQ, share controls, forms, and focus visibility.
- Preserve reduced-motion behavior and no-JS access to navigation/content.
- Test both light and dark themes; do not assume OS dark mode is the primary design.
- Check contrast for muted copy, gold labels, buttons, warning states, and disabled/download-unavailable states.
- Run semantic HTML checks and an automated accessibility scan, followed by keyboard/manual inspection.

## 9. Verification matrix

### Website/static checks
- HTML parse/tag balance, exactly one `h1` per page, unique IDs, all internal anchors resolved.
- `node --check` for every shipped JavaScript file.
- No placeholder domains, emails, endpoints, fake success messages, or unsupported claims.
- All local assets return HTTP 200; favicon no longer returns 404.
- No unexpected third-party requests.
- Page weight and image dimensions recorded.

### Link checks
- Repository and README links read back correctly from all canonical/campaign pages and attached revised bundles.
- Release URLs match actual artifact filenames.
- Before any GitHub release exists, explicitly expect/record release-asset 404s.
- After a later approved release, re-run checks and require HTTP 200 before launch.

### Product checks
- Android and Windows builds complete with real command output.
- Targeted protocol/core tests pass.
- Screenshots match the built revision and do not expose secrets.
- Destination-routing copy matches observed behavior.

### Browser/human checkpoint
- Capture full-page desktop and narrow-mobile renders of canonical and cat campaign pages.
- Provide a short visual checkpoint: hero clarity, screenshot authenticity, three-step comprehension, CTA prominence, campaign/product brand separation, and mobile density.
- Do not deploy until owner approves this visual pass.

## 10. Execution slices

1. **Coordination and root reconciliation**
   - Collect DeepSeek handoff, re-run status/tree inspection, select unified source root, document overlap risks.
2. **GitHub shell only**
   - Create empty private org repo, initialize local metadata/remote, verify, no commit/push.
3. **Build and screenshot evidence**
   - Build both apps, run targeted tests, capture and sanitize real screenshots.
4. **Canonical page structural redesign**
   - Hero, release links, product proof, three-step workflow, use cases, grouped features.
5. **Technical/install/FAQ closeout**
   - Condense protocol/security/limits, add honest install/release paths and FAQ.
6. **Campaign integration**
   - Add campaign route, apply exact flow, links, privacy-safe share behavior, campaign social card.
7. **Attached artifact refresh**
   - Update READMEs/standalone HTML, rebuild suffixed archives, verify parity.
8. **Responsive/a11y/SEO verification**
   - Browser matrix, link checks, accessibility, asset checks, screenshots.
9. **Owner review gate**
   - Present changed files, real build/test output, screenshots, unresolved risks, and exact proposed release URLs. Stop before commit/push/deploy/release.

## 11. Acceptance criteria for this pass

- Empty `canopydigital/pocketmic-lan` repository exists and is verified, but nothing has been pushed or released.
- Correct local source root is initialized and connected without overwriting concurrent DeepSeek work.
- Actual Android and Windows builds are attempted and results are reported truthfully.
- Real app screenshots replace mock product UI where usable builds exist.
- Canonical hero provides direct platform/release actions and README “See how it works.”
- Public workflow is exactly receiver → pair Android → select destination.
- Use cases, product proof, grouped features, FAQ, and final CTA form a coherent conversion path.
- Technical credibility and current-limit honesty are retained without dominating the first screen.
- Cat funnel is campaign-only, functional, privacy-safe, and clearly connected to PocketMic LAN.
- Both attachment bundles and their READMEs/standalone HTML have consistent revised links in preserved, suffixed outputs.
- Desktop/mobile/light/dark screenshots and automated checks are available for owner review.
- No push, tag, GitHub release, deployment, or public visibility change occurs.

## 12. Residual decisions at confirmation

Defaults are proposed so implementation can proceed without another clarification round:

- **Repository visibility:** create private now; publicize only in a later explicit launch pass.
- **Repository name:** `canopydigital/pocketmic-lan`.
- **Campaign route:** `/campaigns/my-cat-ate-my-mic/`.
- **Lead capture:** disabled/transparent until a real endpoint and privacy workflow are approved.
- **Attachment handling:** preserve originals and emit `-integrated` replacements.
- **App-source root:** choose only after DeepSeek's handoff; do not assume the website-only `v0.1.3` folder is complete.
