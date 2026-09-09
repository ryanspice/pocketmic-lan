# Changelog

## v0.1.4 — 2026-09-08

### Landing page
- Updated all version strings from v0.1.2 to v0.1.4
- Corrected performance numbers to measured values: 100 ms prebuffer, 220 ms high-water mark, 200 ms concealment ceiling (was 40/140/100)
- Fixed discovery claim in limits section — LAN discovery exists, QR pairing is the gap
- Updated performance section heading to reflect measured data
- Fixed steps timeline connecting line on desktop (second column now has a visual connector)

### SEO
- Added Open Graph meta tags (og:title, og:description, og:image, og:url, og:type, og:site_name)
- Added Twitter Card meta tags (summary_large_image)
- Added canonical URL
- Added JSON-LD SoftwareApplication structured data
- Added theme-color meta (light + dark variants)
- Added inline SVG favicon (microphone icon)
- Added robots.txt and sitemap.xml
- Improved title tag for keyword targeting

### Accessibility
- Added aria-expanded attribute to mobile nav for screen reader state announcement
- Simplified Escape key handler (removed dead keyCode comparison)

### Infrastructure
- Added GitHub Actions Lighthouse CI workflow (performance, accessibility, SEO, best-practices assertions)
- Added HTML validation CI step
- Added performance budget (≤50 KB total)

### CSS
- Added color-mix() fallback for pre-2023 browsers (header background)
- Added dark mode override for header background
