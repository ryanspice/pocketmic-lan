# PocketMic LAN — v3 Marketing Site Plan

## What is v3

Merged marketing site combining **v1's design system and live integrations** with **v2's architecture, honesty, and multi-page structure**.

---

## Source inventory

| Source | Path | What it contributes |
|--------|------|-------------------|
| v1 (our site) | `dev/v1/` | Warm paper + gold design, shields.io live badges, BMC hero button, comparison table, voice mode section, performance data, FAQ, dark mode toggle, device-frame screenshots |
| v2 (redesign) | `dev/v2/` | 15-page architecture, audience funnels, setup checklist, troubleshooting, technical page, social kit, honest copy, VB-CABLE explainer, mobile CTA bar, screenshot modal, breadcrumbs, per-page SEO |
| Shared | `dev/v3/assets/` | Screenshots (PNG + WebP), social graphics (7 PNGs), favicon |

---

## Design system: keep v1

The warm paper + gold system is distinctive and matches the Canopy Digital brand. v2's charcoal-green is generic. Keep:

- **Tokens**: `--bg:#f4f1ea`, `--paper:#fbfaf6`, `--gold:#b88a3b`, `--teal:#3d7a6b`
- **Dark mode**: `prefers-color-scheme` toggle (user choice, not forced)
- **Typography**: system font stack, editorial scale
- **Components**: inverted-corner tiles, CSS-only tooltips, terminal blocks, datagram visual
- **Device frames**: phone notch + desktop titlebar for screenshots

---

## Page structure: adopt v2

| Page | Route | Source | Notes |
|------|-------|--------|-------|
| Homepage | `/` | Merge | v1 design + v2 hero copy + v2 audience cards + v1 comparison/voice/perf |
| Discord | `/use-cases/discord/` | v2 | Re-skin with v1 design tokens |
| OBS | `/use-cases/obs/` | v2 | Re-skin with v1 design tokens |
| Meetings | `/use-cases/meetings/` | v2 | Re-skin with v1 design tokens |
| Download hub | `/download/` | Merge | v2 structure + v1 download row style + live badges |
| Download Android | `/download/android/` | v2 | Re-skin |
| Download Windows | `/download/windows/` | v2 | Re-skin |
| Setup guide | `/setup/` | v2 | Re-skin, keep interactive checklist |
| Sound check | `/setup/ready/` | v2 | Re-skin |
| Troubleshooting | `/troubleshooting/` | v2 | Re-skin |
| Technical | `/technical/` | Merge | v2 structure + v1 performance data + voice mode section |
| Release notes | `/release-notes/` | v2 | Re-skin |
| Privacy | `/privacy/` | v2 | Re-skin |
| Policy (compat) | `/policy.html` | v1 | Keep existing |

**Total: 14 pages** (v2 had 15, we merge get-started chooser into homepage)

---

## Content decisions

### Hero
- **Copy**: v2's "Your phone. Your next PC mic." — punchier
- **Chips**: v2's "Android → Windows" + "v0.1.4 · Early release" (honest)
- **Proof row**: v2's "No account" / "Encrypted audio" / "No cloud relay" with SVG icons
- **CTA**: v1's Download + See how it works + BMC button
- **Badges**: v1's shields.io live badges (auto-update)
- **Product stage**: v2's dual-screenshot layout with device frames from v1

### VB-CABLE section
- Adopt v2's "Meet the cable that isn't a cable" — upfront, visual, with route diagram
- This is a significant improvement over v1's buried mention

### Voice mode
- Keep v1's dedicated section (Voice / Clean / Custom)
- v2 doesn't have this — it's a differentiator

### Comparison table
- Keep v1's table (PocketMic vs Bluetooth/USB/Cloud)
- v2 doesn't have this — it helps visitors decide

### Performance
- Keep v1's measured numbers (100/220/200ms)
- Add v2's honest framing: "A 10 ms packet is not a 10 ms end-to-end latency claim"

### FAQ
- Merge both: v2's honest answers + v1's technical depth
- Keep v1's "Does it work without internet?" and "Will there be a delay?"
- Add v2's "Is the Android app on Google Play?" (debug-signed honesty)

### Download section
- v1's row layout (icon + title + action) — cleaner than v2's cards
- v1's live version badges from shields.io
- v2's honest labels: "debug-signed APK", "self-contained ZIP"

### Mobile CTA
- Adopt v2's sticky bottom bar "Get the apps"
- v1 doesn't have this — it's a conversion win

### Social kit
- Adopt v2's complete kit: 12 X posts, 6 LinkedIn posts, 7 graphics, content plan
- v1 has nothing — this is free marketing

---

## Technical approach

### Single HTML file per page
- v2's approach: each page is a standalone HTML file with shared CSS/JS
- No build step required for the marketing site
- CSS and JS are shared across all pages

### CSS
- Start with v1's `styles.css` as base
- Add v2's components: routing visual, setup checklist, product stage, mobile CTA, breadcrumbs
- Add print styles from v2
- Add forced-colors support from v2

### JS
- Start with v1's `app.js` (scroll spy, nav toggle, year)
- Add v2's: toast notifications, screenshot modal, setup checklist progress, campaign URL parsing, local-only events
- Keep v1's progressive enhancement (works without JS)

### SEO
- Per-page OG/Twitter/JSON-LD (from v2)
- Keep v1's live shields.io badges
- Keep v1's canonical URLs pointing to canopydigital.ca

### Assets
- Screenshots: both PNG (v1) and WebP (v2) — serve WebP with PNG fallback
- Social graphics: v2's 7 PNGs
- Favicon: v2's SVG
- OG image: v1's PNG (for social previews)

---

## Implementation order

1. **Homepage** — merge both, this is the most important page
2. **CSS base** — v1 tokens + v2 components
3. **JS base** — v1 enhancements + v2 features
4. **Download page** — v2 structure + v1 rows + live badges
5. **Setup page** — v2 checklist re-skinned
6. **Audience pages** — Discord, OBS, Meetings (re-skin v2)
7. **Technical page** — merge v1 data + v2 structure
8. **Troubleshooting** — re-skin v2
9. **Remaining pages** — release notes, privacy, policy
10. **Social kit** — copy v2's posts and graphics
11. **Deploy** — to canopydigital.ca/sites/pocketmic-lan/

---

## Questions for Ryan

1. **Dark mode**: v2 forces dark. v1 offers toggle. Which do you prefer for v3?
2. **Homepage length**: v2 is shorter (hero + use cases + how it works + routing + features + FAQ + CTA). v1 is longer (hero + use cases + how it works + voice mode + features + screenshots + comparison + performance + security + FAQ + build + limits + download). Which sections to keep?
3. **Audience pages**: v2 has separate Discord/OBS/Meetings pages. Do you want these, or keep them as sections on the homepage?
4. **Setup checklist**: v2 has an interactive checklist with progress. Worth the JS complexity?
5. **Social kit**: v2 has 12 X posts + 6 LinkedIn posts + 7 graphics. Want me to include these as-is?
6. **BMC button**: v1 has it in the hero. v2 has it in the footer. Where should it go in v3?
7. **Comparison table**: v1 has it, v2 doesn't. Keep it?
8. **Performance section**: v1 has measured numbers, v2 is vaguer. Keep the hard numbers?
