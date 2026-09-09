#!/usr/bin/env python3
"""Report parse defects in the built index.html (lead verification)."""
import sys
from html.parser import HTMLParser

path = sys.argv[1]
raw = open(path, encoding="utf-8").read()
print("bytes:", len(raw.encode("utf-8")))

class Checker(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.events = []

checker = Checker()
try:
    checker.feed(raw)
    checker.close()
    print("html.parser: completed without exception")
except Exception as e:
    print("html.parser EXCEPTION:", type(e).__name__, e)

import re
patterns = {
    "bare-closing-tag (no >)": r"</[a-zA-Z]+(?![a-zA-Z>\s])",
    "attr-value-glued-to-closing": r'"[^"]{0,80}</[a-z]+>',
    "bare-attr-then-closing": r'[a-zA-Z-]{2,}</[a-z]+>',
}
for name, pat in patterns.items():
    hits = re.findall(pat, raw)
    print(f"{name}: {len(hits)}")
    for h in hits[:8]:
        print("   ", repr(h))

# structural counts
for tag in ["section", "div", "span", "li", "ul", "ol", "p", "a", "code", "pre", "strong", "dl", "dd", "dt", "header", "nav", "main", "footer", "svg", "button", "script", "aside", "h1", "h2", "h3"]:
    opens = len(re.findall(rf"<{tag}[\s>]", raw))
    closes = len(re.findall(rf"</{tag}[\s>]", raw))
    if opens != closes:
        print(f"IMBALANCE {tag}: open={opens} close={closes}")

# h1 / ids / anchors
print("h1 count:", len(re.findall(r"<h1[\s>]", raw)))
ids = re.findall(r'id="([^"]+)"', raw)
print("dup ids:", sorted({i for i in ids if ids.count(i) > 1}))
hrefs = re.findall(r'href="#([^"]+)"', raw)
missing = [h for h in hrefs if h not in ids]
print("broken anchors:", missing)
print("external http(s):", sorted(set(re.findall(r"https?://[^\s\"']+", raw))))