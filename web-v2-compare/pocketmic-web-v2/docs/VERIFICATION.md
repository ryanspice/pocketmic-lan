# Website verification — 9 September 2026

## Executed checks

- **15 HTML pages**; **13 sitemap routes**; **412 local references** checked with no missing files or fragments.
- Page title, description, viewport, canonical, one H1, unique ids, declared image dimensions / alt attributes, explicit button types and new-tab link relationships checked structurally.
- All **7 social PNGs** exist; every Open Graph / Twitter image URL maps to a bundled artwork file. Release asset URLs and checksum lengths pass the pinned-manifest checks.
- **60 Chromium page renders**: all 15 pages at 320, 390, 768 and 1440 px. No document-width overflow, missing rendered image or extra H1 in those checks.
- **12 functional checks passed**: mobile navigation, Escape / focus restore, screenshot modal, six-step progress, local checklist events, reset, app-specific guide selection, hash-copy feedback/fallback, preview navigation / picker / back, and visible navigation/content without JavaScript.
- **0 uncaught page errors** recorded in this run.

The marketing studio also passed All / X / LinkedIn / thread filtering, copy feedback and a 390 px overflow check. Every post’s matching graphic exists.

Raw reports: `verification/static-checks.json`, `verification/browser-checks.json` and `verification/marketing-checks.json`. Actual full-page and viewport screenshots are included in the same directory. Social copy has 24 records: twelve X singles, a six-part X thread and six LinkedIn drafts. The maximum estimated X length is 249 weighted characters with each URL estimated at 23 characters; this is an editorial estimate, not a platform API validation.

## Method and limitations

The runtime browser blocks direct local file and localhost navigation with `ERR_BLOCKED_BY_ADMINISTRATOR`. The rendering and interaction checks therefore used Chromium `page.set_content()` with the actual generated HTML/CSS/JS and the bundled images inlined. No administrator policy was changed. The self-contained preview’s client-side route transitions were exercised directly.

Those checks validate rendered layout and the tested controls, not production HTTP behavior. Static references and fragments were checked against the real on-disk site. The optional browser-check script deliberately retains this inline method so the delivered report is reproducible. Hosted directory routing, response headers, CSP, caches, social crawler fetches and redirects still need the post-deployment smoke test.

The session-storage API is guarded, and in-memory checklist progress/reset passed. Persistent behavior across real hosted navigations was not separately established in this restricted browser context. Clipboard feedback was checked; system clipboard contents were not independently read. This is not a full accessibility audit, field-performance study, comprehensive security audit or cross-browser certification.

Most importantly, **the native Android / Windows apps, Wi-Fi audio path and VB-CABLE driver were not installed or tested**. GitHub release metadata was checked; native binary bytes were not downloaded and hash-verified here. No phone-to-app latency, microphone quality, battery, installation success or conversion lift is claimed.

## Rerun

```powershell
py .\scripts\verify_site.py
.\.venv\Scripts\python .\scripts\browser_checks.py
```

Install optional Playwright tooling as described in README. Rebuild `pocketmic-preview.html` before rerunning preview tests after any change.
