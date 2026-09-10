# Changelog

## v0.1.5 — 2026-09-10

### Android
- **PM-LAN virtual audio cable**: Built-in virtual audio cable replaces VB-CABLE dependency for app mic routing. Install PM-LAN on both phone and PC — no separate driver download needed
- **Adaptive quality**: LinkQualityPolicy auto-tunes bit rate and packet parameters based on live connection quality (RSSI, loss, jitter). Degrades gracefully on weak Wi-Fi, recovers automatically
- **QR pairing improvements**: Faster camera init, better edge-case handling for low-contrast QR codes
- Version bump: 0.1.4 → 0.1.5 (versionCode 5)

### Windows Receiver
- **PM-LAN integration**: Receiver discovers and pairs with the PM-LAN virtual audio cable. Shows PM-LAN alongside VB-CABLE and other output devices
- **Adaptive quality display**: Real-time connection quality indicator in the diagnostics panel showing current quality tier
- Version bumped to 0.1.5
- Build verified: 0 warnings, 0 errors, 74/74 tests passing

### PM-LAN (Virtual Audio Cable)
- New component: lightweight Windows virtual audio driver for routing PocketMic audio into any app
- Replaces VB-CABLE as the recommended audio routing path
- Works alongside VB-CABLE and VoiceMeeter — not a replacement for the user's existing setup, just a simpler default

### Landing page
- Updated all version strings from v0.1.4 to v0.1.5
- Added PM-LAN to the feature list and setup flow
- Added adaptive quality to the features section
- Updated VB-CABLE section to mention PM-LAN as the preferred option
- Updated screenshots section version reference
- Updated download links to v0.1.5 artifacts

### SEO
- Updated sitemap.xml lastmod dates to 2026-09-10
- Updated JSON-LD softwareVersion to 0.1.5
- Updated all download URL version references

## v0.1.4 — 2026-09-08

### Android
- **QR code scanning**: CameraX + ML Kit barcode scanning. Scan `pmic://host:port/key` from PC receiver to auto-fill connection fields
- **Encrypted pairing key**: key stored in Android Keystore-backed EncryptedSharedPreferences. Auto-migrates from plaintext on upgrade
- **Sequence overflow fix**: overflow check now happens before encrypt, not after (prevents one packet with invalid sequence)
- Version bump: 0.1.2 → 0.1.4 (versionCode 4)
- New dependencies: security-crypto, barcode-scanning, camera-camera2/lifecycle/view

### Windows Receiver
- **Show QR button**: generates `pmic://ip:port/key` QR code via QRCoder for phone scanning
- Version bumped to 0.1.4
- Build verified: 0 warnings, 0 errors, 74/74 tests passing

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
