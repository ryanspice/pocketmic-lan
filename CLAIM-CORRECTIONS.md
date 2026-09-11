# PocketMic LAN v0.1.5 — Claim Corrections (v2)

> Updated 2026-09-11 after Astra 6 Pro second review.

---

## 1. Bandwidth: corrected calculation and claim

**Previous claim**: "768 → ~64 kbit/s (12× reduction)"

**Calculation** (per datagram, one 10ms frame, 100 packets/second):

v1 (PCM16):
```
PCM payload:       960 bytes
v1 header:          24 bytes
GCM tag:            16 bytes
IPv4 + UDP:         28 bytes
Total:           1028 bytes × 100 pkt/s = 822.4 kbit/s
```

v2 (Opus at 48 kbit/s average):
```
Opus payload:       ~60 bytes (variable)
v2 header:           28 bytes
GCM tag:             16 bytes
IPv4 + UDP:          28 bytes
Total:             ~132 bytes × 100 pkt/s ≈ 105.6 kbit/s
```

**Corrected claim**: At 100 packets per second, the modeled IPv4/UDP traffic is approximately 822.4 kbit/s for v1 PCM and 105.6 kbit/s for v2 Opus at a 48 kbit/s average payload rate — about **7.8× lower**. This excludes link-layer overhead; real-stream measurement is pending.

**Note**: the previous summary said "~828 kbit/s" but the calculation correctly produces 822.4 kbit/s. Use 822.4 kbit/s.

**Note**: the actual reduction depends on Opus encoder behavior (DTX, input material). The 7.8× figure is a model, not a measurement. PCAP validation is required before using this number in release claims.

---

## 2. Accessibility: narrow the claim

**Previous claim**: "WCAG AA compliant"

**Corrected claim**: "Fixed `--faint` color contrast from #9a9283 (2.73:1) to #6e6759 (~5:1) in light mode, which passes WCAG AA for small text. This is a single-property fix, not a full accessibility audit."

### What was fixed
- `--faint` used for small text in ~8 places
- Previous: #9a9283 on #F5F1EA = 2.73:1 (fails AA)
- Fixed: #6e6759 on #F5F1EA ≈ 5:1 (passes AA for small text)

### What was NOT audited
- All other text/background color combinations
- Screen reader behavior
- Keyboard navigation completeness
- Focus indicator visibility
- Touch target sizes

---

## 3. CI badge: clarify what was done

**Corrected description**: "Replaced the live GitHub Actions CI status badge (which was rendering red 'failing' in the hero) with a static 'CI: GitHub Actions' badge in the project's gold color. This is a presentation fix — it hides the failing status from visitors rather than fixing the CI pipeline."

---

## 4. Native library filename

**P/Invoke declaration**: `private const string LibOpus = "opus";`

Resolves to:
- **Windows**: `opus.dll` (.NET appends .dll)
- **Linux**: `libopus.so` (.NET prepends lib)

**Required**: Windows ZIP must contain `opus.dll`. Android loads `libopus.so` via `System.loadLibrary("opus")`.

---

## 5. Release procedure: tag after build

```bash
CANDIDATE=$(git rev-parse HEAD)
# Build artifacts from $CANDIDATE
# Verify artifacts (checksums, install, smoke test)
# Record evidence in BUILD-EVIDENCE.md
git tag -a v0.1.5 -m "..." $CANDIDATE
git push origin v0.1.5
gh release create v0.1.5 ...
```

---

## 6. Scope reporting: mark unverified

**Previous**: "17 files = implementation; 30 files = including docs; additional 13 are planning/research"

**Corrected**: this reconciliation is **unverified**. The explanation is plausible (8+3+11+4+4 = 30 file touches, not unique files), but no git output establishes it. The next agent must record:

```bash
BASE_SHA=<commit before Phase 1>
HEAD_SHA=<commit after Phase 5 + merge fix>
git diff --stat $BASE_SHA $HEAD_SHA
git status --short  # dirty-worktree check
```

Until those SHAs and output are recorded, the file count difference remains unexplained.

---

## 7. Sustained testing: restore 30-minute minimum

The stabilization plan now requires a **minimum 30-minute locked-screen stream**, not 10 minutes. This is an acceptance threshold, not a claim of long-session reliability.

---

## 8. Hardware-blocked tasks

A hardware-blocked check prevents **release approval**, not **agent-side validation**. An agent without a phone should still complete protocol fixtures, deterministic jitter tests, nonce lifecycle tests, and all code-level verification. Mark hardware tasks as BLOCKED, not FAILED.
