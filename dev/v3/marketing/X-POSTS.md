# PocketMic LAN social drafts

Publish only after the destination routes are deployed. These drafts are not posted or scheduled.
Product baseline: v0.1.4, checked 9 September 2026. Confirm release currency before publication.
Replace the production base URL only when the deployment path changes. Keep the use-case destination matched to the post.

X character totals below are estimates using 23 characters per URL; confirm in the composer. The drafts are intentionally below the 280-character editorial budget.

## x01_launch — Launch

Your phone. Your next PC mic.

PocketMic LAN turns Android into a Windows microphone over your private network.

Free, open source. Discord, OBS & calls via VB-CABLE.

Early release: APK + receiver.

https://canopydigital.ca/sites/pocketmic-lan/?utm_source=x&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=x01_launch

**Graphic:** `../site/assets/social/launch-1200x630.png`

**Alt text:** Your phone. Your next PC mic. PocketMic LAN artwork with real Android and Windows app captures, for a local phone-to-PC audio connection.

**Estimated weighted characters:** 223

---

## x02_discord — Discord / headphones

Great headphones. No mic?

Try your Android phone as a Discord input on Windows with PocketMic LAN + VB-CABLE.

No PocketMic account. Same private network. Run the mic test before joining the squad.

https://canopydigital.ca/sites/pocketmic-lan/use-cases/discord/?utm_source=x&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=x02_discord

**Graphic:** `../site/assets/social/discord-1200x630.png`

**Alt text:** Keep the headphones. Change the mic. PocketMic Android and Windows app captures, with a note that Discord routing uses VB-CABLE.

**Estimated weighted characters:** 223

---

## x03_obs — OBS / creators

A phone mic can be another input in your OBS scene.

PocketMic LAN → Windows → VB-CABLE → Audio Input Capture.

Record a short test for level and sync before you go live. Setup guide:

https://canopydigital.ca/sites/pocketmic-lan/use-cases/obs/?utm_source=x&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=x03_obs

**Graphic:** `../site/assets/social/obs-1200x630.png`

**Alt text:** A phone mic. In your OBS scene. PocketMic Android and Windows app captures; the setup uses Android, Windows and VB-CABLE.

**Estimated weighted characters:** 208

---

## x04_route — Useful routing tip

PocketMic setup tip:

Receiver output: CABLE Input
Your app’s microphone: CABLE Output

The names feel backwards because they describe the cable, not your app. Audio goes in one side and comes out the other.

https://canopydigital.ca/sites/pocketmic-lan/setup/?utm_source=x&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=x04_route

**Graphic:** `../site/assets/social/routing-1080x1080.png`

**Alt text:** Routing tip: choose CABLE Input in PocketMic Receiver, then CABLE Output as the microphone in your app.

**Estimated weighted characters:** 232

---

## x05_boundary — Privacy boundary

“Local” needs a boundary.

PocketMic’s Android-to-Windows audio hop stays on your private LAN. Discord or Teams can still send that audio online.

Open-source transport. No misleading promise that your whole call is offline.

https://canopydigital.ca/sites/pocketmic-lan/technical/?utm_source=x&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=x05_boundary

**Graphic:** `../site/assets/social/engineering-1200x630.png`

**Alt text:** 10 ms packets. Not 10 ms latency. PocketMic app captures with a note on 48 kHz PCM16, encrypted UDP and a trusted LAN.

**Estimated weighted characters:** 249

---

## x06_latency — Engineering / latency

10 ms packets ≠ 10 ms latency.

PocketMic sends 10 ms audio frames. Capture, Wi-Fi, buffering and Windows routing still add delay.

I’d rather document that boundary than call it “zero latency.”

https://canopydigital.ca/sites/pocketmic-lan/technical/?utm_source=x&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=x06_latency

**Graphic:** `../site/assets/social/engineering-1200x630.png`

**Alt text:** 10 ms packets. Not 10 ms latency. PocketMic app captures with a note on 48 kHz PCM16, encrypted UDP and a trusted LAN.

**Estimated weighted characters:** 219

---

## x07_install — Installation transparency

What’s in the PocketMic v0.1.4 download?

• Installable debug-signed Android APK
• Self-contained Windows x64 ZIP
• Published SHA-256 checksums

Not a store release. Not an installer. Exact files and setup notes:

https://canopydigital.ca/sites/pocketmic-lan/download/?utm_source=x&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=x07_install

**Graphic:** `../site/assets/social/launch-1200x630.png`

**Alt text:** Your phone. Your next PC mic. PocketMic LAN artwork with real Android and Windows app captures, for a local phone-to-PC audio connection.

**Estimated weighted characters:** 237

---

## x08_calls — Meeting setup

Try a new mic setup before the important call, not during it.

PocketMic LAN can route your Android mic into a Windows calling app via VB-CABLE.

It’s early software: test your devices and keep a known-working fallback.

https://canopydigital.ca/sites/pocketmic-lan/use-cases/meetings/?utm_source=x&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=x08_calls

**Graphic:** `../site/assets/social/meetings-1200x630.png`

**Alt text:** Another mic option. Before the call. PocketMic app captures and a reminder that this is early software and the setup should be tested first.

**Estimated weighted characters:** 244

---

## x09_debug — Support / signal path

No sound? Follow the path:

1. Phone input level
2. Receiver packet count
3. CABLE Input as playback
4. CABLE Output as your app’s mic

Each check answers a different question. PocketMic troubleshooting:

https://canopydigital.ca/sites/pocketmic-lan/troubleshooting/?utm_source=x&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=x09_debug

**Graphic:** `../site/assets/social/routing-1080x1080.png`

**Alt text:** Input goes in. Output comes out. A routing diagram shows PocketMic Receiver playback into CABLE Input, through VB-CABLE, and CABLE Output selected as the Windows app microphone.

**Estimated weighted characters:** 228

---

## x10_open — Open source

PocketMic LAN is open source: Kotlin + Jetpack Compose on Android, C#/.NET on Windows, and a documented encrypted UDP audio path.

For people who want to use it, inspect it, or help improve it.

https://canopydigital.ca/sites/pocketmic-lan/technical/?utm_source=x&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=x10_open

**Graphic:** `../site/assets/social/engineering-1200x630.png`

**Alt text:** 10 ms packets. Not 10 ms latency. PocketMic app captures with a note on 48 kHz PCM16, encrypted UDP and a trusted LAN.

**Estimated weighted characters:** 218

---

## x11_feedback — Device feedback

Trying PocketMic LAN on your Android + Windows setup?

Useful feedback: phone model, OS versions, destination app, capture mode, and what worked or failed.

Please remove pairing keys and QR codes before posting diagnostics.

https://canopydigital.ca/sites/pocketmic-lan/troubleshooting/?utm_source=x&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=x11_feedback

**Graphic:** `../site/assets/social/launch-1200x630.png`

**Alt text:** Your phone. Your next PC mic. PocketMic LAN artwork with real Android and Windows app captures, for a local phone-to-PC audio connection.

**Estimated weighted characters:** 249

---

## x12_recap — Recap / practical fit

An Android phone. A Windows PC. The same private network.

That’s the starting point for PocketMic LAN. Add VB-CABLE to route into Discord, OBS, or a calling app.

Free, open source, and honest about the setup.

https://canopydigital.ca/sites/pocketmic-lan/get-started/?utm_source=x&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=x12_recap

**Graphic:** `../site/assets/social/launch-1200x630.png`

**Alt text:** Your phone. Your next PC mic. PocketMic LAN artwork with real Android and Windows app captures, for a local phone-to-PC audio connection.

**Estimated weighted characters:** 235

---

## x_thread_1 — The idea

1/6 Your phone already has a microphone. PocketMic LAN gives it a route into your Windows setup.

Android app → encrypted local connection → Windows receiver.

Here’s the practical version, including the extra step.

https://canopydigital.ca/sites/pocketmic-lan/?utm_source=x&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=x_thread_1

**Graphic:** `../site/assets/social/launch-1200x630.png`

**Alt text:** Your phone. Your next PC mic. PocketMic LAN artwork with real Android and Windows app captures, for a local phone-to-PC audio connection.

**Estimated weighted characters:** 240

---

## x_thread_2 — The dependency

2/6 The Windows receiver sends audio to a playback device. A calling app expects a recording device.

VB-CABLE bridges that gap. It’s a separate third-party driver, not something silently bundled into PocketMic.

https://canopydigital.ca/sites/pocketmic-lan/setup/?utm_source=x&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=x_thread_2

**Graphic:** `../site/assets/social/launch-1200x630.png`

**Alt text:** Your phone. Your next PC mic. PocketMic LAN artwork with real Android and Windows app captures, for a local phone-to-PC audio connection.

**Estimated weighted characters:** 236

---

## x_thread_3 — The route

3/6 The names are the confusing bit:

PocketMic output → CABLE Input
App microphone → CABLE Output

Input is where audio enters the virtual cable. Output is where your app reads it.

https://canopydigital.ca/sites/pocketmic-lan/setup/?utm_source=x&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=x_thread_3

**Graphic:** `../site/assets/social/launch-1200x630.png`

**Alt text:** Your phone. Your next PC mic. PocketMic LAN artwork with real Android and Windows app captures, for a local phone-to-PC audio connection.

**Estimated weighted characters:** 206

---

## x_thread_4 — The boundary

4/6 “No cloud relay” applies to the PocketMic phone-to-PC hop.

It does not mean Discord, Teams, or your streaming service stops using the internet. The destination app has its own privacy and recording behaviour.

https://canopydigital.ca/sites/pocketmic-lan/privacy/?utm_source=x&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=x_thread_4

**Graphic:** `../site/assets/social/launch-1200x630.png`

**Alt text:** Your phone. Your next PC mic. PocketMic LAN artwork with real Android and Windows app captures, for a local phone-to-PC audio connection.

**Estimated weighted characters:** 238

---

## x_thread_5 — The timing

5/6 A 10 ms packet is not a 10 ms latency benchmark.

Capture, network conditions, buffering, and Windows audio routing matter. Test the real setup before a call or stream. This is not for real-time instrument monitoring.

https://canopydigital.ca/sites/pocketmic-lan/technical/?utm_source=x&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=x_thread_5

**Graphic:** `../site/assets/social/launch-1200x630.png`

**Alt text:** Your phone. Your next PC mic. PocketMic LAN artwork with real Android and Windows app captures, for a local phone-to-PC audio connection.

**Estimated weighted characters:** 246

---

## x_thread_6 — The invitation

6/6 PocketMic v0.1.4 is available as a debug-signed Android APK and a self-contained Windows x64 ZIP.

Free and open source. Start with your use case, get both apps, then run a sound check. Feedback welcome.

https://canopydigital.ca/sites/pocketmic-lan/get-started/?utm_source=x&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=x_thread_6

**Graphic:** `../site/assets/social/launch-1200x630.png`

**Alt text:** Your phone. Your next PC mic. PocketMic LAN artwork with real Android and Windows app captures, for a local phone-to-PC audio connection.

**Estimated weighted characters:** 232

---

## Source and claim boundaries

Product claims: supplied landing-page HTML and app screenshots, plus the version-pinned release and README.
- https://github.com/ryanspice/pocketmic-lan/releases/tag/v0.1.4
- https://github.com/ryanspice/pocketmic-lan/blob/v0.1.4/pocketmic-lan-v0.1.4/README.md
- https://vb-audio.com/Cable/
- https://obsproject.com/kb/audio-sources
- https://support.discord.com/hc/en-us/articles/360020641332-Mic-Testing

The bandwidth arithmetic is derived from the documented format. Do not imply independently measured end-to-end latency, superior hardware quality, native virtual-mic installation, store approval, customer counts, or adoption metrics. First-person posts are drafts for the project owner, not independently reported quotes.
