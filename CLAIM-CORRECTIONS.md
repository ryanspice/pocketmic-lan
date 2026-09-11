# PocketMic LAN v0.1.5 — Claim Corrections

> Updated 2026-09-11 after Astra 6 Pro review. These corrections supersede
> any conflicting statements in CHANGELOG.md, MASTER-PLAN.md, and RESEARCH-SYNTHESIS.md.

---

## 1. Bandwidth: correct the accounting model

**Previous claim**: "768 → ~64 kbit/s (12× reduction)"

**Corrected claim**: "Opus payload 48 kbit/s; including v2 header + GCM tag + IPv4/UDP headers ≈ 106 kbit/s total on wire. Compared to v1's ~828 kbit/s on wire, this is approximately **8× reduction in total bandwidth**."

### Calculation (per datagram, one 10ms frame)

**v1 (PCM16)**:
```
PCM payload:       960 bytes
v1 header:          24 bytes
GCM tag:            16 bytes
IPv4 + UDP:         28 bytes
Total:           1028 bytes × 100 pkt/s = 822.4 kbit/s
```

**v2 (Opus at 48 kbit/s, ~60 bytes/frame average)**:
```
Opus payload:       ~60 bytes (variable, 48kbps average at 10ms)
v2 header:           28 bytes
GCM tag:             16 bytes
IPv4 + UDP:          28 bytes
Total:             ~132 bytes × 100 pkt/s ≈ 105.6 kbit/s
```

**Ratio**: 822.4 / 105.6 ≈ **7.8× reduction**

### What's uncertain
- Opus at 48kbps with DTX enabled may average lower than 60 bytes/frame
- Opus at 32kbps would be even smaller (~40 bytes/frame average)
- The actual reduction depends on input material and encoder settings

### Measurement needed
- PCAP capture of real v1 and v2 streams
- Average datagram size over 60 seconds
- Payload-only measurement (excluding IP/UDP) for app-level comparison

---

## 2. Accessibility: narrow the claim

**Previous claim**: "WCAG AA compliant"

**Corrected claim**: "Fixed `--faint` color contrast from #9a9283 (2.73:1) to #6e6759 (~5:1) in light mode, which passes WCAG AA for small text. This is a single-property fix, not a full accessibility audit."

### What was fixed
- `--faint` used for small text in ~8 places (stage-label, breadcrumbs, compare-table, etc.)
- Previous: #9a9283 on #F5F1EA background = 2.73:1 (fails AA)
- Fixed: #6e6759 on #F5F1EA background ≈ 5:1 (passes AA)

### What was NOT audited
- Color contrast of all other text/background combinations
- Screen reader behavior
- Keyboard navigation completeness
- Focus indicator visibility
- Touch target sizes

---

## 3. CI badge: clarify what was done

**Previous implication**: "CI badge fixed" suggests build health improvement.

**Corrected description**: "Replaced the live GitHub Actions CI status badge (which was rendering red 'failing' in the hero) with a static 'CI: GitHub Actions' badge in the project's gold color. This is a presentation fix — it hides the failing status from visitors rather than fixing the CI pipeline."

### What this means
- The CI workflow may still be failing
- Visitors no see "CI: GitHub Actions" in gold instead of "CI: failing" in red
- The actual CI state should be verified and fixed separately

---

## 4. Native library filename: document the convention

**P/Invoke declaration**: `private const string LibOpus = "opus";`

This resolves to:
- **Windows**: `opus.dll` (or `opus` — .NET appends .dll automatically)
- **Linux**: `libopus.so` (or `opus` — .NET prepends lib automatically)
- **macOS**: `libopus.dylib`

**Required for distribution**: the Windows ZIP must contain `opus.dll` (not `libopus.dll`).

### Where the filename matters
- `OpusDecoder.cs` line 19: `private const string LibOpus = "opus";`
- Build scripts must produce `opus.dll` (MSVC) or rename `libopus-0.dll` (MinGW)
- Android: `System.loadLibrary("opus")` in OpusEncoder.kt loads `libopus.so`

---

## 5. Release procedure: tag after build, not before

**Previous procedure**:
```bash
git tag -a v0.1.5 -m "..."
git push origin master --tags
# then build artifacts
```

**Corrected procedure**:
```bash
# 1. Freeze candidate commit
CANDIDATE=$(git rev-parse HEAD)
echo "Release candidate: $CANDIDATE"

# 2. Build artifacts from that exact commit
dotnet publish ... -c Release
./gradlew assembleDebug assembleRelease

# 3. Verify artifacts (checksums, install test, smoke test)
sha256sum PocketMicReceiver-win-x64.zip
sha256sum PocketMic-v0.1.5-debug.apk

# 4. Tag the verified commit
git tag -a v0.1.5 -m "PocketMic LAN v0.1.5" $CANDIDATE

# 5. Push tag (not branch)
git push origin v0.1.5

# 6. Create GitHub release with verified artifacts
gh release create v0.1.5 --verify-tag ...
```

---

## 6. Scope reporting: reconcile file counts

**MASTER-PLAN.md**: "30 files changed, 1,450+ insertions"
**REVIEW-HANDOFF-ASTRA.md**: "17 files changed, 1,588 insertions"

**Reconciliation**: These measure different things.
- **17 files** = files changed in the 5 implementation commits (Phases 1-5 + merge fix)
- **30 files** = total files changed including research docs, ROADMAP, MASTER-PLAN, handoff

**Corrected**: Use `git diff --stat HEAD~6 HEAD` for the implementation scope:
```
17 files changed, 1588 insertions(+), 48 deletions(-)
```

The additional 13 files are planning/research documents, not implementation code.
