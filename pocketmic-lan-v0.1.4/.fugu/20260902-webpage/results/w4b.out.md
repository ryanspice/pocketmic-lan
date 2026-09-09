Verdict: The README already scopes every claim the page can make — build the site as a faithful restatement of README.md:1-174 with its six documented limits promoted to a top-level section, and the page cannot lie.
1. IA
- Order (8 blocks, as specified): hero, how-it-works, features, performance, security, build/install, current-limits, footer.
- Nav anchors (6 links only): #how-it-works, #features, #performance, #security, #build, #limits. Hero needs no anchor; footer is a landmark, not a nav target.
- Hierarchy: one h1 in hero (product name + one-sentence promise). Exactly one h2 per named section. h3 only as subgroups:
  - how-it-works: setup flow (README.md:80-87), VB-CABLE routing (README.md:92-96), firewall/port (README.md:99-112)
  - features: transmitter, receiver, resilience, tooling (README.md:9-17)
  - security: crypto, boundary (README.md:142-149)
  - build/install: requirements, build, install (README.md:21-76)
- No skipped levels, no h4+. Skip link targets main#main. Section headings must match anchor text exactly.
2. A11Y checklist
[static = checkable in no-browser review; browser = needs a real browser/AT]
- Landmarks [static]: exactly one header, one nav[aria-label], one main#main, one footer; every section carries a heading or aria-labelledby.
- Skip link [static]: first link in body, href="#main", visually hidden until :focus-visible.
- Heading order [static]: one h1; sequential h2/h3 with zero skips.
- Contrast AA [static]: every text/background pair >= 4.5:1 (>= 3:1 for >= 24px or 18.66px bold), computed from hex values — include focus and hover states.
- :focus-visible [static]: visible focus style on every link/button/input; grep confirms no outline:none without a replacement.
- Keyboard reachability [browser]: real tab order and no focus traps. Static proxy checks only: no tabindex>0, no aria-hidden on focusable content.
- prefers-reduced-motion [static + browser]: any animation/transition must be wrapped in the media query (static); actual effect needs a browser.
- Icon-only controls [static]: every control with no visible text has aria-label; grep-able.
- Tooltip via :has() [static + browser]: CSS-only pattern, e.g. a control with aria-describedby plus `.tooltip` shown via `:hover`/`:focus-visible` using `:has()` — zero JS (static); hover/focus triggering needs a browser.
- Form/button semantics [static]: real <button>, label for every input, no div-onclick; pairing-key and port inputs get aria-describedby hints.
- Cannot check without a browser: actual focus visibility/tab order, screen-reader output, tooltip hover behavior, 400% zoom reflow, reduced-motion effect, touch-target feel (44px min is statically checkable in CSS).
3. Performance checklist (no-build static page)
- Payload budget: <= 300 KB total transfer (target <= 150 KB): one HTML file + one CSS file + inline SVGs. Zero third-party requests, zero cookies, zero tracking.
- Images: inline SVG for all icons and the one topology diagram; no raster files; every SVG gets viewBox + intrinsic sizing to kill CLS.
- Fonts: system stack only — system-ui, -apple-system, "Segoe UI", Roboto, sans-serif. No webfonts, no font-display, no preloads.
- Third parties: none. No CDNs, analytics, Google Fonts, embeds. All CSS in one file; render-blocking limited to that one stylesheet.
- JS: zero required. At most one inline script <= 2 KB (e.g. current year), placed after content; no frameworks, no polyfills, no async/defer orchestration needed.
- Rendering: LCP is hero text + SVG; avoid backdrop-filter and heavy box-shadow; smooth scrolling only via CSS scroll-behavior inside prefers-reduced-motion.
4. Copy tone guide (12 lines)
Do:
- Name the version and say plainly "no signed release" in build/install copy (README.md:157).
- Say "Windows receiver only" at the top of install and again in limits (README.md:153).
- State manual IPv4 entry + pairing key; no discovery or QR (README.md:154).
- Give the real numbers: 48 kHz mono PCM16, 10 ms UDP packets, 100 packets/s, 40 ms prebuffer, <= 100 ms concealment, 140 ms trim threshold (README.md:11, 128, 132-134).
- Say PCM costs more bandwidth than Opus (README.md:155).
- Say physical Android/Windows/Wi-Fi/VB-CABLE end-to-end testing is pending (README.md:158).
Don't:
- No "production-ready", "just works", or anything implying a signed distribution exists.
- No adaptive jitter/clock-recovery claims — settings are intentionally fixed and conservative (README.md:138).
- No internet-hardening claims; scope is "trusted private LAN only", no port-forwarding (README.md:147, 149).
- No superlatives (seamless, effortless, best) and no unqualified "low latency" without the numbers.
- Don't bury limits in a footnote — they are their own section.
- Don't imply an APK/EXE was built in an environment lacking the SDKs (README.md:5).
5. No-browser smoke checks (8)
1. Broken anchors: extract all href="#..." and assert each has a matching id="", and every nav anchor resolves. e.g. grep -o 'href="#[^"]*"' index.html | sort -u | while read a; do id=${a#href="#}; id=${id%\"}; grep -q "id=\"$id\"" index.html || echo "broken: $id"; done
2. One h1: grep -c '<h1' index.html must print 1.
3. Heading order: grep -oE '<h[1-6]' index.html | tr -d '<' | awk 'NR>1 && $1>p+1 {print "skip at line", NR} {p=$1}' — expect no output.
4. Duplicate ids: grep -o 'id="[^"]*"' index.html | sort | uniq -d — must be empty.
5. Tag balance: for header, nav, main, section, footer — open count equals close count (grep -c '<section' vs '</section>').
6. Missing alt: grep -o '<img[^>]*>' index.html | grep -vc 'alt=' — must be 0; alt="" only for decorative images.
7. Oversized assets: find . -name '*.svg' -size +100k (flag); total page weight via wc -c on html+css; grep -oE 'https?://[^"]*' index.html | grep -v '^https://github.com' — any other external URL is a violation.
8. Control hygiene: grep 'tabindex="[1-9]' must be empty; grep -o '<button[^>]*>' index.html | grep -vc 'aria-label' flags icon-only buttons missing labels.
Evidence:
- README.md:3 (product definition), :11-12 (codec/encryption), :13-16 (feature list), :80-87 (use flow), :99-100 (default port 49500, Private networks only), :126-138 (performance profile), :142-149 (security/boundary), :151-158 (six current limits), :160-174 (docs to link from footer). File read in full, 174 lines, no truncation.
Gaps:
- CWD is v0.1.3 but the handed context source is the v0.1.2 README; I did not read v0.1.3. Product facts may have drifted.
- No exact PCM bandwidth figure is stated — only "more than Opus". If an exact number is wanted: 48,000 x 2 bytes = ~93.8 KiB/s payload + 24-byte header + GCM tag per 10 ms packet (100 pps) ~= 97-98 KB/s; label any such figure as derived, not from the README.
- No license, no Android min-version, no repo URL in the README — footer copy must not invent them.
Next gate:
- Decide whether the site states a computed bandwidth figure (derived, labeled) or keeps copy qualitative ("more bandwidth than Opus") — the rest of the blueprint is unambiguous.
Sharpest claim: The six-line limits list (README.md:151-158) is the page's credibility engine — the site is honest exactly where it refuses to out-claim the README.
REFRAME: scoped honesty as the product's differentiator, not its flaw.
session_id: 20260903_010316_440deb