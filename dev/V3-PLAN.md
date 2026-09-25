# PocketMic LAN — v3 Marketing Site

## Status: INTEGRATED LOCALLY · NOT DEPLOYED

The selected cream-and-green marketing homepage is integrated into the canonical `dev/v3/` deployment payload. The multi-page guides remain on the shared subsite design system. CI and Lighthouse now target this same directory. Live deployment and visual/product acceptance remain open for the project owner.

## Directory structure

```
dev/v3/
├── index.html              Selected marketing homepage (cream + green)
├── assets/marketing-home.css Homepage-only layout additions
├── assets/marketing-home.js Homepage theme, demo, navigation, and CTA behavior
├── styles.css, app.js      Retained prior shared files; not used by the selected homepage
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
├── release-notes/index.html    v0.1.5 release notes
├── privacy/index.html      Privacy & terms
└── marketing/
    ├── X-POSTS.md          12 standalone X drafts + 6-part thread
    ├── LINKEDIN-POSTS.md   6 LinkedIn drafts
    ├── CONTENT-PLAN.md     2-week editorial sequence
    ├── posts.json          24 structured post records
    ├── social-studio.html  Filterable copy workspace
    └── layouts/            7 editable HTML artwork files
```

## Release-candidate changes

- The approved cream-and-green homepage replaces the older v3 homepage in `dev/v3/index.html`.
- Existing setup, download, technical, troubleshooting, use-case, privacy, roadmap, and release-note pages remain in the payload; their shared assets and homepage anchors resolve locally.
- Homepage download links open the checked-in detail pages, which show the latest published v0.1.5 assets until v0.1.6 is actually released.
- CTA and outbound-link hooks dispatch local `pocketmic:conversion` browser events; they do not send analytics to a service.
- Protocol/configuration figures are documented separately from end-to-end latency, for which no current measurement is established.
- The homepage uses the Canadian English source copy and a clearly labelled US spelling preview; other site translations remain incomplete.

## Design system split

- **Homepage** (`index.html` + `assets/marketing-home.css` + `assets/marketing-home.js`): selected candidate with shared CTA hooks, locale preference, and local Signal Lab behavior
- **Subpages** (all other public pages): shared design via `assets/subsite.css` + `assets/subsite.js`
- Both share the same favicon, screenshots, and social graphics

The site defaults to Canadian English (`en-CA`). The `en-US` option is a spelling preview, not a complete translated locale. The locale catalog tracks 21 targets, but only reviewed translations may be advertised as supported. The homepage numbers are protocol/configuration facts; no current end-to-end latency benchmark is published.

The homepage uses locally checked-in download detail pages that read the current published release manifest. It does not fetch GitHub release or star APIs at page load. CTA and outbound-link hooks emit local browser events only; this build does not send analytics events to a service.

## Deployment

After the v0.1.6 tag and manual site acceptance, stage the exact reviewed `dev/v3/` payload, verify its file manifest and hashes, then deploy it to `/sites/pocketmic-lan/`. Verify routes, downloads, metadata, and the public page from an external browser; retain the prior payload for rollback. The current candidate has not been deployed.
