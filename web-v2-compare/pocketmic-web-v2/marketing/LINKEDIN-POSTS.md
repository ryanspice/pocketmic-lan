# PocketMic LAN social drafts

Publish only after the destination routes are deployed. These drafts are not posted or scheduled.
Product baseline: v0.1.4, checked 9 September 2026. Confirm release currency before publication.
Replace the production base URL only when the deployment path changes. Keep the use-case destination matched to the post.

## li01_launch — Launch / useful product story

Your phone. Your next PC microphone.

I’m building PocketMic LAN: an Android app and a Windows receiver that give your phone’s microphone a local route into your PC audio setup.

The use cases are practical: Discord with the headphones you already like, another audio input for an OBS scene, or an alternative microphone to evaluate before a call.

The connection between the phone and PC runs over your trusted private network, with authenticated packet encryption and no PocketMic account or cloud relay. To expose the receiver’s audio as a microphone in another app, you install VB-CABLE separately.

That last step matters. A useful landing page should explain the whole setup, not leave the most confusing dependency until after the download.

v0.1.4 is early software: an installable debug-signed Android APK and a self-contained Windows x64 ZIP. It is not a production store release, and real-device testing is still important.

The source, downloads, requirements, and setup guides are available here. I’d especially value feedback from people willing to test their actual Android + Windows + destination-app combination.

#OpenSource #Windows

https://canopydigital.ca/sites/pocketmic-lan/?utm_source=linkedin&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=li01_launch

**Graphic:** `../site/assets/social/launch-1200x630.png`

**Alt text:** Your phone. Your next PC mic. PocketMic LAN artwork with real Android and Windows app captures, for a local phone-to-PC audio connection.

---

## li02_latency — Engineering / honest performance claims

A 10 ms audio packet is not a 10 ms latency benchmark.

That distinction is worth keeping visible when explaining a project like PocketMic LAN.

The Android sender captures 48 kHz mono PCM16 and groups 480 samples into each packet. That is 10 ms of audio. The path to another Windows app still includes capture buffering, the network, the receiver prebuffer, Windows audio routing, and the destination app.

The documented default receiver prebuffer alone is 100 ms. Packet duration cannot honestly stand in for the complete experience.

There is a similar distinction in the bandwidth numbers: 768 kbit/s describes the raw audio payload. The documented 1,000-byte datagrams at 100 packets per second are 800 kbit/s before UDP/IP and link-layer overhead.

Those are protocol calculations, not measurements of every user’s network.

I want the product’s technical page to make those boundaries clear: explain what is implemented, identify what has actually been tested, and leave room for real-device evidence rather than filling it with “zero latency” language.

The implementation and current limits are documented here.

#SoftwareEngineering #OpenSource

https://canopydigital.ca/sites/pocketmic-lan/technical/?utm_source=linkedin&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=li02_latency

**Graphic:** `../site/assets/social/engineering-1200x630.png`

**Alt text:** 10 ms packets. Not 10 ms latency. PocketMic app captures with a note on 48 kHz PCM16, encrypted UDP and a trusted LAN.

---

## li03_obs — Creator workflow / practical setup

A small audio-routing tip for anyone trying PocketMic LAN with OBS:

The receiver sends audio to CABLE Input. OBS captures CABLE Output.

The names describe the two ends of VB-CABLE, not the direction you might expect from the application you are configuring.

The complete path is:
Android microphone → PocketMic Receiver → VB-CABLE → OBS Audio Input Capture.

Once that is connected, make a short test recording. Speak normally, leave a few seconds of silence, and clap once on camera. Review the result for level, noise, and audio/video alignment before using it live.

Also check that OBS is not capturing the same device globally and again as a scene source. A duplicate input can be the source of an apparent echo.

PocketMic is not trying to replace OBS. It is another way to get a microphone signal into the Windows workflow you already use.

The Android and Windows apps are open source. They are also early software, so the guide includes the dependency and timing limitations rather than promising that every phone will behave the same way.

The OBS-specific setup is here.

#OBSStudio #OpenSource

https://canopydigital.ca/sites/pocketmic-lan/use-cases/obs/?utm_source=linkedin&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=li03_obs

**Graphic:** `../site/assets/social/obs-1200x630.png`

**Alt text:** A phone mic. In your OBS scene. PocketMic Android and Windows app captures; the setup uses Android, Windows and VB-CABLE.

---

## li04_privacy — Architecture / privacy boundaries

“No cloud” is too broad unless you explain where the boundary ends.

For PocketMic LAN, the intended boundary is specific: microphone audio travels from an Android phone to a Windows receiver over a trusted private network. That hop uses authenticated encryption and does not need a PocketMic cloud relay.

But choosing that input in Teams does not turn Teams into an offline call. Sending it into OBS does not prevent OBS from streaming. The destination application still controls what happens next.

The website deserves the same precision. This site build uses self-hosted assets and no third-party tracking scripts. Its setup checklist can remember checked steps for the browser tab’s session, but it does not access a microphone, inspect native apps, or confirm that installation succeeded.

Those are different data paths, with different responsibilities. Combining them into one sweeping “nothing ever leaves your device” claim would be easier copy, but worse communication.

The project’s technical and privacy pages separate the local transport, the destination app, and the website behaviour.

That is the kind of boundary I want users to understand before they install.

#PrivacyByDesign #SoftwareArchitecture

https://canopydigital.ca/sites/pocketmic-lan/privacy/?utm_source=linkedin&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=li04_privacy

**Graphic:** `../site/assets/social/engineering-1200x630.png`

**Alt text:** 10 ms packets. Not 10 ms latency. PocketMic app captures with a note on 48 kHz PCM16, encrypted UDP and a trusted LAN.

---

## li05_activation — Product engineering / activation

A download click is not a working microphone.

That is the product-design problem behind the PocketMic LAN setup flow.

There are two apps to install. The devices need to reach each other over a trusted private network. The phone needs to capture audio. The receiver needs to receive it. VB-CABLE needs to expose the correct recording endpoint. Finally, the destination app needs to select and hear that input.

Each step can succeed while the next one is still wrong.

So the setup checklist does not pretend to detect a native installation or display a fake “connected” state. It asks the user to confirm the meaningful steps, then points them to the actual mic test or recording in Discord, OBS, or their calling app.

The funnel is not an email gate. It is a route from a specific use case to the matched downloads, the audio setup, and a sound check.

That also changes the success metric: a click is a click; “I can hear it in my app” is a user-reported outcome; neither is an independently measured hardware test.

The current setup flow is here. It is part of the open-source PocketMic project.

#ProductEngineering #DeveloperExperience

https://canopydigital.ca/sites/pocketmic-lan/setup/?utm_source=linkedin&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=li05_activation

**Graphic:** `../site/assets/social/routing-1080x1080.png`

**Alt text:** Input goes in. Output comes out. A routing diagram shows PocketMic Receiver playback into CABLE Input, through VB-CABLE, and CABLE Output selected as the Windows app microphone.

---

## li06_testers — Feedback / invitation to test

I’m looking for useful device feedback on PocketMic LAN — an open-source Android-to-Windows microphone project.

The best feedback is specific rather than just “works” or “doesn’t work.”

Which phone and Android version? Which Windows version? Which destination app? Did the phone input meter move? Did receiver packet counts increase? Did the final application hear the microphone?

That separates a capture problem from a network problem, and a network problem from a Windows audio-routing problem.

The v0.1.4 release includes an installable debug-signed Android APK, a self-contained Windows x64 receiver ZIP, and checksums. It needs the same trusted private network, and app microphone routing uses a separate VB-CABLE installation.

There are real limits: this is early software, not a production store release or a zero-latency monitoring system. Test it before relying on it for a call or stream.

If you try it, please remove pairing keys, QR codes, and sensitive connection details before sharing screenshots or diagnostics. The troubleshooting page links to the project’s issue tracker.

A reproducible report is much more useful than a vague claim — especially for a two-device audio path.

#OpenSource #AndroidDevelopment

https://canopydigital.ca/sites/pocketmic-lan/troubleshooting/?utm_source=linkedin&utm_medium=social&utm_campaign=pocketmic_v014&utm_content=li06_testers

**Graphic:** `../site/assets/social/launch-1200x630.png`

**Alt text:** Your phone. Your next PC mic. PocketMic LAN artwork with real Android and Windows app captures, for a local phone-to-PC audio connection.

---

## Source and claim boundaries

Product claims: supplied landing-page HTML and app screenshots, plus the version-pinned release and README.
- https://github.com/ryanspice/pocketmic-lan/releases/tag/v0.1.4
- https://github.com/ryanspice/pocketmic-lan/blob/v0.1.4/pocketmic-lan-v0.1.4/README.md
- https://vb-audio.com/Cable/
- https://obsproject.com/kb/audio-sources
- https://support.discord.com/hc/en-us/articles/360020641332-Mic-Testing

The bandwidth arithmetic is derived from the documented format. Do not imply independently measured end-to-end latency, superior hardware quality, native virtual-mic installation, store approval, customer counts, or adoption metrics. First-person posts are drafts for the project owner, not independently reported quotes.
