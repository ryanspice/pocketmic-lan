#!/usr/bin/env python3
"""Final structural + content verification for the redesigned landing page."""
import re

path = r"B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.3\web\index.html"
html = open(path, encoding="utf-8").read()
print("bytes:", len(html.encode("utf-8")))

# 1. strict tag balance (skip void + self-closing elements)
voids = {"meta", "link", "input", "img", "br", "hr", "path", "rect", "circle", "use"}
tags = re.findall(r"<(/)?([a-zA-Z][a-zA-Z0-9]*)(?=[\s/>])([^>]*?)(/?)>", html)
stack = []
problems = []
for closing, name, attrs, self_close in tags:
    if name in voids or self_close:
        continue
    if closing:
        if not stack or stack[-1] != name:
            problems.append(f"mismatched close </{name}> (stack top: {stack[-1] if stack else 'empty'})")
            continue
        stack.pop()
    else:
        stack.append(name)
problems += [f"unclosed: {t}" for t in stack]
print("tag balance:", problems if problems else "OK")

# 2. headings
h = re.findall(r"<(h[1-6])[\s>]", html)
order = [int(x[1]) for x in h]
skips = [f"{order[i]} after {order[i-1]}" for i in range(1, len(order)) if order[i] > order[i-1] + 1]
print("h1 count:", order.count(1), "| heading skips:", skips if skips else "none")

# 3. ids vs anchors
ids = set(re.findall(r'id="([^"]+)"', html))
hrefs = set(re.findall(r'href="#([^"]+)"', html))
print("anchors without ids:", sorted(hrefs - ids) or "none")

# 4. claim-critical strings present (no regression vs the reviewed version)
claims = [
    "encrypted microphone for a Windows PC on the same LAN",
    "48 kHz mono PCM16 in 10 ms UDP packets",
    "100 packets per second",
    "768 kbit/s",
    "480 samples / 960 plaintext bytes",
    "exactly 1000 bytes per datagram",
    "SHA-256(UTF-8(pairing key))",
    "AES-256-GCM",
    "Random 64-bit stream session ID",
    "24-byte protocol header",
    "Android backup",
    "Trusted private LAN only",
    "does not make this a hardened internet voice service",
    "Do not port-forward the receiver",
    "PocketMic-v0.1.2-debug.apk",
    "PocketMicReceiver-win-x64.zip",
    "adb install -r",
    "49500",
    "Private networks",
    "CABLE Input",
    "CABLE Output",
    "VB-CABLE",
    "No signed Android release",
    "physical Android, Windows, Wi-Fi, and VB-CABLE testing",
    "MIT License",
    "Ryan Spice-Finnie",
    "40",
    "140",
    "300 ms safety ceiling",
    "10 Hz",
    "little-endian",
    "no discovery or QR pairing",
]
missing = [c for c in claims if c not in html]
print("missing claims:", missing if missing else "none")

# 5. forbidden strings (W5 fixes must survive)
forbidden = ["low-latency", "safe tool to expose", "Audio now streams", "aria-expanded"]
present = [f for f in forbidden if f in html]
print("forbidden strings present:", present if present else "none")

# 6. a11y atoms
print("skip link to #main:", 'class="skip" href="#main"' in html)
print("nav-toggle checkbox:", '<input type="checkbox" id="nav-toggle"' in html)
print(":has checked selector:", ":has(.nav-toggle-input:checked)" in open(r"B:\Dev\android\pocketmic-lan\pocketmic-lan-v0.1.3\web\styles.css", encoding="utf-8").read())
print("lang attr:", 'lang="en"' in html)
print("color-scheme:", 'content="light dark"' in html)