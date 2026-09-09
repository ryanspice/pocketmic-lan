ROLE: bounded web-design reviewer (experimental lane — non-critical deliverable). Read-only — NEVER edit files. Return a blueprint another worker will follow.

TASK: A static single-page landing site will be built for "PocketMic LAN" — an Android phone used as an encrypted low-latency microphone for a Windows PC on the same LAN (48 kHz mono PCM16 in 10 ms UDP packets, AES-256-GCM, pairing-key auth). Produce the design/quality blueprint so the page is accessible, honest, and fast.

CONTEXT SOURCE (read this one file for product facts):
- B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.2\README.md

DELIVERABLE — exactly five sections:
1. IA: recommended section order + heading hierarchy + nav anchor names (≤8 sections; hero, how-it-works, features, performance, security, build/install, current-limits, footer). One h1 only.
2. A11Y CHECKLIST: concrete testable items — landmarks, skip link, heading order, contrast AA, :focus-visible, keyboard reachability, prefers-reduced-motion, icon-only controls needing aria-label, tooltip pattern using :has() without JS, form/button semantics. Say which items a no-browser review can and cannot check.
3. PERFORMANCE CHECKLIST for a no-build static page: payload budget guidance, inline SVG vs external images, font strategy (system stack), no render-blocking third parties, minimum JS.
4. COPY TONE GUIDE: 8-12 do/don't lines for an honest technical voice that surfaces the product's documented limits (no signed release, manual IP entry, PCM bandwidth, untested physical E2E) instead of hiding them.
5. NO-BROWSER SMOKE CHECKS: 6-10 grep/parse checks a reviewer can run in a terminal to catch broken anchors, unclosed tags, missing alt text, duplicated ids, oversized assets.

RULES: no page copy (tone guidelines only, no full sentences for the site), no code required (checklists/guidelines only). Return only the blueprint. Stop when done.