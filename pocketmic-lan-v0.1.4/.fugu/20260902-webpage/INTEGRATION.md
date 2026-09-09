# Fugu batch — PocketMic LAN landing page (run 20260902-webpage)

> **2026-09-03 redesign (lead):** page rebuilt by the lead after review — warm-paper/gold design
> system, editorial type scale, inline-SVG datagram visual (24 B header / 960 B payload /
> 16 B GCM tag), timeline how-it-works, stat tiles with inverted corners (single-layer radial
> mask), CSS-only `:has()` tooltips + nav toggle, terminal-styled build blocks, dark mode.
> All claims identical to the W5-reviewed version (verify_final.py: balance OK, 1 h1, no
> heading skips, all 32 claim strings present, no forbidden strings). ~41 KB total (2 files).

Orchestrated via `Invoke-FuguLane.ps1` (pwsh; `powershell` 5.1 is broken for lane dispatch:
`Process.StandardOutput.ReadToEndAsync()` throws ObjectDisposedException on fast-exiting
children — observed on all 4 lanes, root-caused by bisect, fixed by using `pwsh` 7.6.5).

## Lanes dispatched (wave 1, parallel)
| # | Lane | Model | State | Deliverable |
|---|------|-------|-------|-------------|
| W1 | worker-pro | DeepSeek V4 Pro | done rc=0 | Fact sheet, 9.5 KB, every claim file:line-cited |
| W2 | worker-minimax | MiniMax M3 | done rc=0 (97 min; corrupt self-report) | Built page (3 files) |
| W3 | worker-flash | DeepSeek V4 Flash | done rc=0 | Claims audit (do-not-claim / must-include / contradictions) |
| W4 | worker-kimi-k26 | Kimi K2.6 | FAILED HTTP 404 | — (experimental lane; endpoint not deployed) |
| W4B | worker-nv-flash | DeepSeek V4 Flash | done rc=0 | IA + a11y + perf + copy-tone blueprint |

Wave 2: W5 worker-pro — final independent review of the built page (pending at write time).

## Worker results

### Worker 1 (worker-pro) — fact sheet
Result: full content spec in `results/w1.out.md` — 9 sections, all cited.
Evidence: read all 7 source docs to completion; every citation verified against line numbers.
Recommended action: use as the claims authority for the page. **Flags 4 README↔ROADMAP contradictions
at the product's core** (see Cross-worker conflicts).

### Worker 2 (worker-minimax) — page build
Result: `web/index.html` + `web/styles.css` + `web/app.js` (~21 KB total; budget was ≤150 KB).
Evidence: content independently verified claim-safe against W1/W3; structure repaired by lead
(see Safe-to-land items).
Recommended action: **do not trust the lane's self-report** — its final OutFile is corrupt
(claims "The web folder doesn't exist yet" and emits mangled tool-call text). The artifact is
real; the report is garbage. Lane took 97 min and looped on a systematic output defect: it
dropped `>` on closing tags and glued attribute values to closing tags across every rewrite.
Lead applied deterministic structural repair.

### Worker 3 (worker-flash) — claims audit
Result: `results/w3.out.md`. Verdict: "the docs describe a designed-and-source-verified
protocol, not a built or device-tested product."
Evidence: DO-NOT-CLAIM (7 items, cited), MUST-INCLUDE (4 items), CONTRADICTIONS (3),
PARAPHRASE RISKS (4 — e.g. "end-to-end encrypted" is false; any latency number must be
labeled design intent).
Recommended action: page must sell the wire protocol, not the binaries. Adopted.

### Worker 4 (worker-kimi-k26) — EXPERIMENTAL LANE
Result: FAILED — HTTP 404 from NVIDIA: `"Function '23d4f03a-…': Not found for account …"`.
Evidence: `B:\AI-Wiki.runtime\fugu-lanes\pocketmic-web-20260903-005459-171706\FAILURE.txt`.
Recommended action: keep the lane registered as experimental; **do not add to LastResortChain
until a probe succeeds** (matches the adapter's own note). Task re-routed to worker-nv-flash.

### Worker 4B (worker-nv-flash) — blueprint
Result: `results/w4b.out.md` — 8-block IA, a11y checklist (static vs browser), performance
budget (≤150 KB, zero third-party), 12-line copy tone guide, 8 no-browser smoke checks.
Evidence: README read in full (174 lines).
Recommended action: adopted — skip link → #main, one h1, limits as their own section,
no invented repo URL / license / min-version (footer uses LICENSE file facts only).

### Worker 5 (worker-pro) — final review (wave 2)
Result: `results/w5.out.md` — VERDICT: PASS-WITH-FIXES. Confirmed no claim violations on
"encrypted"/non-E2E wording, latency numbers labeled, no signed-artifact claim. Sharpest
finding: the hero's unqualified "low-latency" is the page's only true overclaim.
Fixes requested (all applied by lead 03:15):
1. title/meta/tagline: dropped unqualified "low-latency" → "encrypted phone microphone",
   tagline now carries the factual "48 kHz mono PCM16 in 10 ms UDP packets".
2. step 7 reworded: "PocketMic sends encrypted 10 ms UDP frames… physical E2E pending
   validation — see Current limits" (no present-tense working-product assertion).
3. boundary quote restored verbatim (README.md:149 "…does not make this a hardened internet
   voice service.").
4. wire-format facts added (480 samples/960 bytes per packet, 100 pps, 768 kbit/s raw,
   1000-byte datagram — PROTOCOL.md:7-12,30).
5. mobile nav now a pure-CSS toggle: hidden checkbox + label + `:has(.nav-toggle-input:checked)`
   — no JS required, keyboard focus ring via `:focus-visible + label`.
6. nav labels aligned to section headings; "#hero Overview" link removed (6 links, per w4b).
7. color-scheme meta → "light dark" (dark-mode block no longer dead).
8. touch targets ≥44px (nav-toggle, nav links).
Applied and verified: html.parser clean, 1 h1, no dup ids, all anchors resolve, zero external
URLs, `node --check app.js` OK, no leftover "low-latency"/aria-expanded strings, 21.3 KB total.
Browser render check deferred: Chrome requires a one-time "Allow remote debugging" click
(browser-exec popup) that only the owner can grant.

## Cross-worker conflicts
- **W1 and W3 independently converge**: README sells a working product; ROADMAP.md:25 says
  "Nothing in v0.1.2's end-to-end audio path has been observed working" (static analysis only),
  the v0.1.2 Windows receiver shipped with CS0246 compile errors (ROADMAP.md:27-31), and
  neither half built via the documented default path. W1 (4 contradictions) and W3 (3) agree.
  The page's framing must be the honest one (protocol design + documented limits), not README's
  optimistic tone. Implemented: hero tagline, performance section ("not measured on real
  hardware yet"), and a top-level Current limits section.
- W4 (kimi-k26 404) vs user expectation ("explicit experimental lane only") — consistent;
  the lane failed exactly as the adapter's comment predicts.

## Safe-to-land items
- `B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.3\web\index.html` — lead-repaired markup
  (rewritten 02:27): fixed SVG rect/path unclosed tags, 3× `aria-hidden="true</span>` value
  corruption, 7× `</code`/`</pre>`/`</dd>` unclosed blocks, `defer</script>` script tag,
  "300 ms safety" → "…safety ceiling" (README:136), skip link → `#main` (W4B spec).
  Content otherwise byte-parity with worker output. Verified: 1 h1, no dup ids, all 7 anchors
  resolve, zero external URLs, tag balance clean, `node --check app.js` OK, total 21 KB.
- `web/styles.css` — clean: CSS variables, `:has()` mobile nav (no-JS), `:focus-visible`
  global, prefers-reduced-motion, dark-mode AA palette, system font stack. No changes needed.
- `web/app.js` — 2 KB progressive enhancement only (year, scroll-spy, nav toggle), no-op
  without JS. `node --check` OK. No changes needed.
- Scratch `web/_probe.txt` (worker artifact) removed; web/ contains exactly 3 files.

## Lead decisions required
1. **Public framing** (from W1/W3): the page ships the honest framing — "source-verified
   protocol design, physical E2E testing pending". If the owner wants README-parity marketing
   instead, that is a false-public-claim risk per the docs' own words. Default: keep honest.
2. **Bandwidth figure**: W4B suggests a derived 768 kbit/s figure is fine (PROTOCOL.md:12
   states it; W1 cites it) — the page currently avoids numeric bandwidth claims; add if wanted.
3. **Repo URL / hosting**: no repo URL exists in README; the page links nothing external.
   Owner should decide where the page lives (current location: `pocketmic-lan-v0.1.3\web\`)
   and whether to add the real repo URL.
4. **Fugu adapter ops notes** (not page-blocking):
   - Invoke-FuguLane.ps1 must be run under `pwsh`, not `powershell` 5.1 (broken async pipe
     reads). Consider a guard at the top of the script.
   - PacketId collisions possible when lanes launch in the same millisecond (W3/W4 shared
     `-306`; W4's failure clobbered W3's META). Consider PID/random suffix in the default id.
   - worker-minimax took 97 min and produced a corrupt final self-report; its systematic
     `</tag` defect made its HTML unusable without lead repair. Consider a structure-smoke
     gate before accepting minimax build output.