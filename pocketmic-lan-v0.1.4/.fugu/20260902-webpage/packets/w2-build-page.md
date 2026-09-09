ROLE: bounded front-end implementer. Code-change permission GRANTED, but ONLY under B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.3\web\ — you may create exactly three files there: index.html, styles.css, app.js. Never touch anything else, never run builds/installs, never add dependencies.

TASK: Build a self-contained static landing page for "PocketMic LAN v0.1.2" — an open project that turns an Android phone into an encrypted low-latency microphone for a Windows PC on the same LAN. The page must be fully offline-capable: no frameworks, no build step, no CDN/font downloads, system font stack, inline SVG only.

CONTENT SOURCES (read only these; all product claims MUST trace to them — do not add, soften, or invent anything):
- B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.2\README.md (primary: features, use flow, build, limits)
- B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.2\PROTOCOL.md (security details)
- B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.2\PERFORMANCE_AUDIT.md (performance numbers)
- B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.2\CHANGELOG.md (version history)
- B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.2\LICENSE (footer)

REQUIRED SECTIONS, in order:
1. header/nav with anchor links to every section
2. hero: what it is + tagline + key facts (48 kHz mono PCM16 in 10 ms UDP packets, AES-256-GCM, LAN-only)
3. how it works: the numbered 7-step flow (receiver, IP, pairing key, device, start) exactly as documented
4. features grid (max 10, from README "What is included")
5. performance (documented numbers only: prebuffer 40 ms, silence concealment ≤100 ms, latency trim >140 ms, 10 Hz meter, buffer reuse, fallback audio-source test)
6. security: key = SHA-256(pairing key), AES-256-GCM, random 64-bit stream session + unsigned 32-bit sequence nonce, authenticated 24-byte header, Android backup disabled, private LAN only + the explicit warning that encryption is NOT a hardened internet voice service (no port forwarding)
7. build & install: Windows 11 requirements (Android Studio/SDK 36, JDK 17+, .NET 8), the two build scripts, expected outputs (PocketMic-v0.1.2-debug.apk, PocketMicReceiver-win-x64.zip), adb install, firewall private-networks note (default UDP 49500), VB-CABLE virtual-mic routing
8. current limits (the documented list, surfaced honestly: Windows receiver only, manual IPv4 entry, PCM bandwidth, fixed prebuffer, no signed release/installer/updater, E2E physical testing pending)
9. footer: version v0.1.2 + LICENSE (MIT) line

DESIGN RULES (owner standards — follow strictly):
- CSS-first: no JS for layout, state, or visibility; app.js only for tiny progressive enhancements (e.g. year, scroll-spy class) and must be defensive/no-op without JS.
- Clean light "paper" aesthetic, one restrained accent color; prefer modern CSS (:has() allowed for hover/tooltip, mask-composite for any inverted corners, CSS variables).
- Semantic landmarks: header/nav/main/section/footer; one h1; heading order sane; skip-to-content link.
- Accessible: WCAG AA contrast, visible :focus-visible states, prefers-reduced-motion respected, all interactive elements keyboard-reachable, alt text on every image, aria-labels where icon-only.
- Responsive: mobile-first, nav collapses gracefully without JS dependency.
- No external URLs required for core function; any links to repo docs are relative-style anchors or plain text paths.

VERIFICATION (run these after writing, via your terminal tool):
- python -c "from html.parser import HTMLParser" parse of index.html (or equivalent well-formedness check)
- node --check app.js (or python -m py_compile equivalent) — syntax check
- every href="#..." has a matching id in index.html (grep both lists and diff)
- list the three files with sizes
Report exactly what you verified and what you could NOT verify (no browser rendering available).

OUTPUT: end with a short summary: files created (paths), checks run + results, claims sources used, residual risk. Stop when the three files exist and checks pass — do not add extra files, READMEs, or a build system.