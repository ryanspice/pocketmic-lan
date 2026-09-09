# PocketMic LAN — launch content and funnel plan

## Editorial position

“Your phone. Your next PC mic.” Lead with the job, show real apps, and disclose the route before installation. Primary audience: Android/Windows users comfortable with an APK and a driver, especially Discord users and creators. Secondary: people evaluating an alternative meeting microphone. Technical readers get source-first material, not a consumer page full of packet diagrams.

These audiences are positioning hypotheses inferred from the supplied use cases, not verified conversion research. There are no fabricated customers, testimonials, conversion lifts, performance measurements, or user counts.

## The funnel, without a gate

| Intent | Entry | Next action | Activation | Follow-through |
|---|---|---|---|---|
| Discord / headphones | `use-cases/discord/` | Matched app downloads | Receiver → VB-CABLE → Discord mic test | Troubleshooting or feedback |
| OBS / creators | `use-cases/obs/` | Matched app downloads | OBS input + a real test recording | Report device and timing issues |
| Meetings | `use-cases/meetings/` | Requirements / permission check, then downloads | Destination-app test before a call | Keep a known-working fallback |
| Developers | `technical/` | Inspect source or download | Build / inspect / device-test | GitHub issue or contribution |
| Ready to install | `download/` | Android and Windows detail pages | Full six-step checklist | User-confirmed final sound check |

The global “Get the apps” CTA goes directly to downloads. The use-case chooser is optional. No account, email form, pricing fiction, scarcity message, fake installation detection, or native-device permissions are added to the website.

## Suggested two-week sequence

These are relative publishing slots, not scheduled posts or a promise about optimal posting time. Prefer useful responses and actual device feedback over publishing every draft.

| Relative day | X | LinkedIn | Intent |
|---|---|---|---|
| 1 | x01_launch | li01_launch | Introduce the product and exact release state |
| 2 | x02_discord | — | One concrete user problem |
| 3 | x04_route | li05_activation | Explain the dependency and setup completion |
| 4 | x03_obs | — | Show the creator workflow |
| 5 | x06_latency | li02_latency | Demonstrate engineering judgement |
| 6–7 | Reply to real questions; use x09_debug only when useful | — | Resolve friction, do not invent engagement |
| 8 | Six-part thread, once | li03_obs | Full route / practical tutorial |
| 9 | x05_boundary | — | State the privacy boundary |
| 10 | x08_calls | li04_privacy | Explain fit and responsibility |
| 11 | x07_install | — | Clarify distribution and signatures |
| 12 | x11_feedback | li06_testers | Ask for actionable device reports |
| 13–14 | x10_open or x12_recap; skip if redundant | — | Source visibility / recap |

## Social graphics

Seven ready-to-use PNGs are generated from editable HTML layouts: five 1200×630 campaign cards, one 1080×1080 launch card, and one 1080×1080 cable-routing card. Real supplied app captures are used in an illustrative layout; no fake connected status or performance graph is created. The wide cards also back the site’s Open Graph metadata.

`social-studio.html` groups all 24 drafts, offers copy buttons with a keyboard fallback, links the matching images, and provides alt text. It is a local review tool, not a posting integration. Keep `marketing/` out of the deployed public site unless you deliberately publish it.

## Measurement contract

UTM tags identify platform, campaign, and creative. Only constrained tag values are propagated on same-site links. The site emits local `pocketmic:conversion` events; it sends no analytics requests.

- `cta_click`: website navigation intent.
- `download_click`: click on a release asset, not a completed download or install.
- `setup_step_check`: user checked or unchecked a step.
- `setup_self_reported`: user clicked “I can hear it in my app.” Not independent device verification.

No production metrics exist until a consent-reviewed collection adapter is deliberately implemented and the privacy notice matches it. Do not use a click count as an activation rate. First qualitative goal: find the largest real setup failure with a reproducible report.

## Before publication

Deploy routes and graphics first. Open every campaign link from the live host. Check the current release against the version-pinned download manifest. Confirm the project owner approves first-person wording and the hosting/privacy notice. Validate the post in the platform composer. Add image alt text. Publish manually; no account has been accessed and no post has been sent.
