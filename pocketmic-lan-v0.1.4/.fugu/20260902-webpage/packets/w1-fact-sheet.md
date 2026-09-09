ROLE: bounded evidence extractor. Read-only — NEVER edit files. Return ONE evidence-gated fact sheet.

TASK: Produce the complete, citation-backed content spec for a static landing page for the PocketMic LAN project (Android phone becomes an encrypted low-latency microphone for a Windows PC on the same LAN). The page will be built from YOUR spec by another worker — everything you state must be true and traceable.

SOURCE DOCS (read only these, absolute paths):
- B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.2\README.md (primary)
- B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.2\PROTOCOL.md
- B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.2\PERFORMANCE_AUDIT.md
- B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.2\BUILD_STATUS.md
- B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.2\CHANGELOG.md
- B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.2\LICENSE
- B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.2\ROADMAP.md (limits/future sections only)

DELIVERABLE (markdown, sections in this exact order):
1. IDENTITY — one-line product description + 2-3 tagline candidates. All from facts.
2. FEATURES — max 10 bullets, each with file:line.
3. PERFORMANCE FACTS — numbered/sampled facts: sample rate, packet size/frequency, prebuffer, latency trimming, meter rate, buffer reuse. Each with file:line.
4. SECURITY FACTS — key derivation, cipher, nonce/sequence, authenticated data, Android backup setting, network scope, and the explicit boundary warning ("not a hardened internet voice service" etc.). Each with file:line.
5. HOW IT WORKS — the numbered end-to-end flow from the docs (receiver → IP → pairing key → device select → start). Each step with file:line.
6. BUILD & INSTALL — requirements (Android Studio/SDK 36, JDK 17+, .NET 8), build commands, expected outputs (exact filenames), install via adb, firewall port default. Each with file:line.
7. CURRENT LIMITS — the complete documented limits list, verbatim-ish, each with file:line.
8. VERSION HISTORY — release headline per version from CHANGELOG, with file:line.
9. DO-NOT-CLAIM LIST — every statement the docs explicitly say is NOT true or NOT done (e.g. no signed release, no installer/updater, no discovery/QR, manual IP entry, PCM vs Opus bandwidth, fixed prebuffer, receiver-only, physical E2E not yet tested). Each with file:line.

RULES:
- No invention. Every claim must cite a file:line that exists. Never extrapolate beyond the docs.
- If two docs contradict each other, say so explicitly in a CONTRADICTIONS section (add it after section 9).
- Do not propose page copy — facts only.
- Return ONLY the fact sheet markdown. Stop when complete.

VERIFICATION: every section has at least one file:line citation; no citation points at a nonexistent line. State at the end which checks you actually ran.