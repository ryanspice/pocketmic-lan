# PocketMic LAN — v3 Marketing Site

## Status: BUILT

v3 is the merged marketing site combining v1's design system with v2's architecture.

## Directory structure

```
dev/v3/
├── index.html              Homepage (v3 design — warm paper + gold)
├── styles.css              v3 design system (v1 tokens + v2 components)
├── app.js                  v3 JS (v1 scroll spy + v2 checklist/modal)
├── policy.html             Privacy & terms page
├── robots.txt, sitemap.xml, release.json, routes.json
├── assets/
│   ├── subsite.css         v2 design system (for subpages)
│   ├── subsite.js          v2 JS (for subpages)
│   ├── favicon.svg
│   ├── screenshots/        PNG + WebP product captures
│   └── social/             7 social graphics (1200x630 + 1080x1080)
├── use-cases/
│   ├── discord/index.html  Discord & gaming landing page
│   ├── obs/index.html      OBS & creators landing page
│   └── meetings/index.html Meetings & calls landing page
├── download/
│   ├── index.html          Download hub
│   ├── android/index.html  Android APK details
│   └── windows/index.html  Windows receiver details
├── setup/
│   ├── index.html          6-step interactive checklist
│   └── ready/index.html    Sound check confirmation
├── technical/index.html    Protocol, encryption, build instructions
├── troubleshooting/index.html  Diagnostic flowchart
├── release-notes/index.html    v0.1.4 release notes
├── privacy/index.html      Privacy & terms
└── marketing/
    ├── X-POSTS.md          12 standalone X drafts + 6-part thread
    ├── LINKEDIN-POSTS.md   6 LinkedIn drafts
    ├── CONTENT-PLAN.md     2-week editorial sequence
    ├── posts.json          24 structured post records
    ├── social-studio.html  Filterable copy workspace
    └── layouts/            7 editable HTML artwork files
```

## What was merged

| From v1 | From v2 |
|---------|---------|
| Warm paper + gold design tokens | 14-page multi-page architecture |
| shields.io live badges | "Your phone. Your next PC mic." copy |
| BMC button in hero | VB-CABLE upfront explainer |
| Comparison table | Interactive setup checklist |
| Voice mode section | Troubleshooting page |
| Measured performance (100/220/200ms) | Mobile sticky CTA bar |
| Device-frame screenshots | Social media kit (12 X + 6 LinkedIn + 7 graphics) |
| Dark mode (prefers-color-scheme) | Per-page SEO (OG/Twitter/JSON-LD) |
| FAQ (8 questions) | Honest copy ("early release", "debug-signed") |
| Download row layout | Breadcrumbs, print styles |

## Design system split

- **Homepage** (`index.html` + `styles.css` + `app.js`): v3 merged design
- **Subpages** (all other `index.html` files): v2 design via `assets/subsite.css` + `assets/subsite.js`
- Both share the same favicon, screenshots, and social graphics

## Deployment

Copy the contents of `dev/v3/` to the hosting server at `/sites/pocketmic-lan/`.
