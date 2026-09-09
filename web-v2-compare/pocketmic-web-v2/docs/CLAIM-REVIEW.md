# Claim review and source boundary

Checked 9 September 2026. The supplied HTML and archive were the starting point. Public release and official vendor documentation were used only to verify release-sensitive details and add concrete setup instructions. The native codebase was not comprehensively audited or run.

## Changes made

| Original statement or treatment | Delivered treatment | Reason / evidence |
| --- | --- | --- |
| “Your phone is now a wireless microphone”; any Android and any Windows app | Explicit Android + Windows pair, documented platform baseline and a separate virtual-driver step | The Android UI/metadata identifies API 26+; the receiver release is Windows x64; app routing depends on the destination supporting the endpoint |
| Packet layout occupies the hero | Real native screenshots and the user’s job occupy the hero | Design choice based on supplied use cases, not a measured conversion claim |
| “Next release” will include a PocketMic virtual cable | No shipping-date or bundled-driver promise | The checked v0.1.4 release does not establish that commitment |
| `/latest/download/` combined with a fixed APK filename | Pin both assets and checksum file to `/releases/download/v0.1.4/` | Prevent version/filename drift; current release metadata was checked |
| “No signed Android release” without distinction | Installable **debug-signed APK**; no production-signed consumer artifact claimed | Exact release notes distinguish debug-signed, unsigned release, and self-contained Windows ZIP |
| Windows “download” could suggest an installer | ZIP extraction and `PocketMicReceiver.exe` are explicit | GitHub release describes a self-contained x64 WinForms ZIP, not an installer |
| “Measured 100–250 ms,” “CD quality,” “professional audio quality,” blanket superiority to Bluetooth / headsets | No universal end-to-end timing, quality, range or battery guarantee | The checked release leaves real-device acceptance open; sample rate is not a quality benchmark |
| “768 kbit/s” presented as full 1000-byte datagram traffic | 768 kbit/s raw PCM; 800 kbit/s documented datagrams before UDP/IP and link-layer overhead | 48,000 × 16 = 768,000; 1,000 × 100 × 8 = 800,000 bits/s. Derived arithmetic, not measured throughput |
| “10 ms” could be read as latency | Label as audio packet duration; explain the separate prebuffer | README describes a 100 ms default prebuffer. It is not an end-to-end benchmark |
| “No data ever leaves your LAN” | No PocketMic cloud relay between phone and receiver; destination apps have their own behavior | Online meetings / streaming should not be represented as LAN-only because their input came from PocketMic |
| Detailed Custom DSP promises | Qualified technical note, not a consumer headline | Supplied page and screenshot show Custom mode, but the pinned release README does not establish every effect / live-control behavior described by the page |
| “No telemetry” privacy language alongside Google Analytics, remote badges and donation JS | Remove those web requests; explain local events, functional storage and the unresolved host-logging policy | Original page contains third-party scripts/requests; native-app policy is not automatically the website policy |
| Audio-port-only firewall guidance | Use documented Private-profile project helper; mention the next-port control channel | Pinned README explicitly identifies a control channel on the next port |
| Source files linked at the repository root | Link to the tagged nested project directory | Tag contents show `pocketmic-lan-v0.1.4/` as the project root |

## Primary sources actually consulted

- Supplied `Pasted text(20260909-212823).txt`: original HTML, copy, requirements, feature scope, screenshots and downloads.
- Supplied `sitemap.tar`: original index, stylesheet, app script, privacy page, sitemap and product screenshot assets.
- GitHub latest-release metadata: `https://api.github.com/repos/ryanspice/pocketmic-lan/releases/latest` (returned v0.1.4 on the check date).
- Pinned release: `https://github.com/ryanspice/pocketmic-lan/releases/tag/v0.1.4`.
- Pinned README: `https://github.com/ryanspice/pocketmic-lan/blob/v0.1.4/pocketmic-lan-v0.1.4/README.md`.
- Tag root contents: `https://api.github.com/repos/ryanspice/pocketmic-lan/contents?ref=v0.1.4`.
- VB-Audio driver and input/output behavior: `https://vb-audio.com/Cable/`.
- OBS source selection and duplicate-device warning: `https://obsproject.com/kb/audio-sources`.
- Discord mic test: `https://support.discord.com/hc/en-us/articles/360020641332-Mic-Testing`.
- Teams audio settings: `https://support.microsoft.com/en-au/teams/meetings/manage-audio-settings-in-microsoft-teams-meetings`.
- Android API 26 / Android 8 mapping: `https://developer.android.com/about/versions/oreo/android-8.0-changes`.

Release URLs, hashes and asset sizes were read from GitHub metadata. They are not proof that locally downloaded bytes match those hashes. No APK, ZIP or virtual driver was installed or tested here. Screenshot appearance is not a substitute for acceptance testing.

## Keep unresolved until checked

The site operator must confirm current release support, Windows version / architecture coverage beyond the supplied Windows 11 baseline, native signing and installation prompts, actual Custom-mode behavior, measured phone-to-app delay, hardware quality, battery drain, managed-device policy, host log retention and any injected web analytics.

Do not convert those unknowns into positive claims just to simplify a campaign. Audience priority and the editorial schedule are proposed hypotheses; there is no fabricated market research or performance uplift.
