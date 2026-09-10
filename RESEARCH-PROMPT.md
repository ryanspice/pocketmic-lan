# PocketMic LAN — Research References & Deep Research Prompt

## Key Papers and Documentation

### Encryption & Protocol
1. **RFC 7714** — AES-GCM Authenticated Encryption in SRTP — https://www.rfc-editor.org/info/rfc7714/
2. **AES-GCM-SIV** — Nonce Misuse-Resistant Authenticated Encryption — https://datatracker.ietf.org/doc/draft-mattsson-cfrg-aes-gcm-sst/
3. **SFrame Security Analysis** — E2EE for real-time audio/video — https://www.sciencedirect.com/science/article/pii/S2214212624002606
4. **Crypto Model of Real-Time Audio Streaming** — https://www.semanticscholar.org/paper/Crypto-Model-of-Real-Time-Audio-Streaming-Across-Ibam-Boyinbode/b3db4aba8522945df2c41f873ecfe970f3a3c1d5
5. **AES-GCM for ESP32/MQTT** — nonce management under long-term operation — https://www.mdpi.com/2078-2489/17/1/33

### Audio Codec & Processing
6. **RFC 6716** — Opus Audio Codec (IETF standard) — https://www.rfc-editor.org/info/rfc6716/
7. **Opus FEC Experiments** (Mozilla) — https://blog.mozilla.org/webrtc/audio-fec-experiments/
8. **WebRTC NetEQ Jitter Buffer** — https://webrtchacks.com/how-webrtcs-neteq-jitter-buffer-provides-smooth-audio/
9. **VoIP Adaptive Jitter Buffer** (ACM) — https://sbhunia.me/publications/manuscripts/acity11.pdf
10. **GARCH-based Adaptive Playout Delay** — https://www.sciencedirect.com/science/article/abs/pii/S1389128610001726

### Android Low-Latency Audio
11. **Android Oboe/AAudio Documentation** — https://developer.android.com/games/sdk/oboe/low-latency-audio
12. **Oboe: C++ Library for Low Latency Audio** (Google blog) — https://android-developers.googleblog.com/2018/10/introducing-oboe-c-library-for-low.html
13. **Low Latency Audio with improved CPU Performance** (ADC 2025, Phil Burk) — https://www.youtube.com/watch?v=DtBrKEu0R0g
14. **Android Wi-Fi Low-Latency Mode** — https://source.android.com/docs/core/connect/wifi-low-latency
15. **Wi-Fi 6 Performance: Throughput, Latency, Energy** (ACM SIGMETRICS 2023) — https://liux4189.github.io/files/sigmetric23_wifi6_cameraready.pdf

### Windows Audio Drivers
16. **Microsoft ACX Audio Codec Sample** — https://github.com/microsoft/Windows-driver-samples/tree/main/audio/simpleaudiosample
17. **Kernel-Mode WDM Audio Components** — https://learn.microsoft.com/en-us/windows-hardware/drivers/audio/kernel-mode-wdm-audio-components
18. **Virtual Audio Cable WDM Driver Guide** — https://levelup.gitconnected.com/building-a-virtual-audio-cable-with-a-custom-wdm-driver-a-guide-to-kernel-mode-audio-device-25931fac999d
19. **VB-CABLE Technical Reference** — https://vac.muzychenko.net/en/manual/glossary.htm
20. **SysVAD Virtual Audio Driver** (Microsoft sample) — https://github.com/microsoft/Windows-driver-samples/tree/main/audio/sysvad

### Real-Time Transport
21. **SRTP (RFC 3711)** — Secure Real-time Transport Protocol
22. **WebTransport** — Browser API for QUIC-based low-latency — https://www.rfc-editor.org/info/rfc9000
23. **RTP Jitter Mechanisms** — https://www.vobiz.ai/blog/rtp-jitter-mechanisms/
24. **Asterisk Jitter Buffer Operation** — https://www.asterisk.org/jitter-buffer-asterisk/

### Competitor Analysis
25. **WO Mic** — Google Play 3.3★, 17K reviews — https://play.google.com/store/apps/details?id=com.wo.voice2
26. **AndroidMic** — Open source, cross-platform, F-Droid — https://github.com/teamclouday/AndroidMic
27. **QuicMic** — WebTransport/QUIC browser-based — https://github.com/Fix3dll/QuicMic
28. **AudioRelay** — Bidirectional, cross-platform — https://audiorelay.net/
29. **Micstream** — USB-focused, <10ms latency — https://micstream.io/

---

## Deep Research Prompt for Astra 6 Pro

You are helping design the next version of PocketMic LAN — an open-source Android-to-Windows encrypted microphone app over LAN. The current architecture uses:

**Current stack:**
- Android: Kotlin + Jetpack Compose, `AudioRecord` at 48kHz mono PCM16, AES-256-GCM per packet, UDP transport, CameraX QR pairing
- Windows: C# .NET 8 WinForms, NAudio 2.3 for playback, VB-CABLE for virtual mic routing
- Protocol: 1000-byte UDP datagrams (24-byte header + 960-byte PCM16 + 16-byte GCM tag), 100 packets/second
- Measured: 100ms prebuffer, 220ms high-water, 200ms concealment, 768 kbit/s bandwidth, ~250ms worst-case Wi-Fi jitter tail

**Key constraints:**
- Must work on trusted private LAN only (no internet relay)
- Must remain open source (MIT)
- Must not add third-party cloud dependencies
- Target: voice chat, streaming, meetings — NOT real-time instrument monitoring

**Research the following topics in depth, with specific technical recommendations and implementation guidance:**

### 1. Android Low-Latency Capture
- How does `AudioRecord` compare to AAudio/Oboe for 48kHz mono capture latency?
- What are the real-world latency numbers for MMAP exclusive mode on modern Android devices?
- What's the minimum achievable capture latency on a OnePlus 9 Pro (Snapdragon 888)?
- How should the capture callback be structured for minimum latency (lock-free ring buffer, no allocation)?
- What are the pitfalls of `VOICE_COMMUNICATION` source vs `UNPROCESSED` on different vendors?

### 2. Wi-Fi Audio Transport Optimization
- What causes the ~250ms jitter tail on Wi-Fi? Is it power save, frame aggregation, or scanning?
- How effective is `WIFI_MODE_FULL_LOW_LATENCY` vs `WIFI_MODE_FULL_HIGH_PERF` on Wi-Fi 5 vs Wi-Fi 6?
- Should we implement packet pacing (spread 100 packets evenly over 1 second) instead of burst-sending?
- What's the optimal UDP buffer size for real-time audio on Android and Windows?
- How does Wi-Fi 6 OFDMA affect small-packet real-time audio?

### 3. AES-GCM Nonce Management at Scale
- Our nonce is 8-byte session ID + 4-byte sequence. At 100 pps, the 32-bit sequence space lasts ~497 days. Is this safe?
- What are the risks of AES-GCM nonce reuse in a reconnect scenario? (Our ROADMAP identified a critical pathId nonce reuse bug)
- Should we move to AES-GCM-SIV (nonce misuse-resistant) or is the current scheme sufficient for LAN-only use?
- What's the performance difference between AES-GCM and ChaCha20-Poly1305 on ARM (Android) and x86 (Windows)?

### 4. Opus Codec Integration
- What's the real-world latency of Opus at 10ms frame size in VOIP mode?
- How does Opus FEC (in-band forward error correction) work, and what's the quality tradeoff?
- For Android, should we use libopus via NDK or MediaCodec Opus encoder?
- For Windows, should we use Concentus (pure C#) or libopus P/Invoke?
- What's the optimal Opus configuration for voice chat over LAN (bitrate, complexity, FEC, DTX)?

### 5. Adaptive Jitter Buffer Design
- How does WebRTC's NetEQ jitter buffer work? What can we learn from it?
- What's the optimal algorithm for adaptive buffer depth (GARCH, Kalman filter, percentile-based)?
- How should we handle clock drift between Android capture and Windows playback?
- What's the best packet loss concealment algorithm for PCM16 voice? (vs Opus built-in PLC)

### 6. Windows Virtual Audio Driver
- What's the feasibility of the "Pattern A" approach (render→capture loopback in kernel) using the VirtualDrivers/Virtual-Audio-Driver fork?
- What's the minimum code change to turn the reference SysVAD sample into a working loopback device?
- How does VB-CABLE achieve its render→capture loopback? What can we learn?
- What are the signing requirements for a test-signed driver on Windows 11?

### 7. USB Transport via adb reverse
- Can `adb reverse tcp:<port> tcp:<port>` reliably forward real-time audio?
- What's the measured latency of TCP over USB bulk transfer on modern Android devices?
- Should we use TCP (simpler) or try to tunnel UDP over adb?
- What happens when the USB cable is mid-stream? How fast is the failover to Wi-Fi?

### 8. Noise Suppression & DSP
- How does Android's `VOICE_COMMUNICATION` source processing compare to WebRTC APM?
- When should we add RNNoise or WebRTC APM — only when the vendor processing is insufficient?
- What's the CPU cost of a real-time noise gate + compressor + limiter on a Snapdragon 888?
- How should we handle the "double DSP" problem (vendor processing + our processing)?

### 9. Security Hardening
- Our pairing key is 12 characters from a 32-symbol alphabet (~60 bits). Is this sufficient for LAN-only use?
- Should we move to PBKDF2/Argon2id for key derivation?
- How should we handle the "encrypted at rest" requirement on Android (EncryptedSharedPreferences vs Android Keystore direct)?
- What's the threat model for a LAN-only encrypted audio stream?

### 10. Battery & Power Optimization
- What's the expected battery drain of continuous 48kHz capture + AES-GCM + UDP send?
- How should we handle Doze mode and battery saver restrictions?
- What's the optimal Wi-Fi lock strategy for different network conditions?
- How does the OnePlus 9 Pro's power management affect audio capture consistency?

**For each topic, provide:**
1. Current best practice (with citations)
2. Specific implementation recommendations for PocketMic
3. Tradeoffs and risks
4. Estimated effort (hours/days)
5. Priority (P0/P1/P2)

**Output format:** Structured markdown with clear sections, code snippets where relevant, and a prioritized action plan at the end.
