#!/usr/bin/env python3
"""Strict tag balance + text-content parity check (lead verification)."""
import re
import sys
from html.parser import HTMLParser

orig = open(sys.argv[1], encoding="utf-8").read()   # worker's 01:33 version
fixed = open(sys.argv[2], encoding="utf-8").read()  # repaired version

# 1. Strict tag balance on the repaired file
def count_tags(html, tag):
    opens = re.findall(rf"<{tag}(?=[\s>])", html)
    closes = re.findall(rf"</{tag}>", html)
    return len(opens), len(closes)

tags = ["html","head","body","header","nav","main","footer","section","div","span","ul","ol","li","p","a","h1","h2","h3","code","pre","strong","dl","dt","dd","svg","button","aside","script","title","meta","link"]
problems = []
for t in tags:
    o, c = count_tags(fixed, t)
    if o != c and t not in ("meta", "link"):  # void elements are self-closed
        problems.append(f"{t}: open={o} close={c}")
print("tag balance problems:", problems if problems else "NONE")

# 2. Void-element sanity (meta/link self-closed or no close)
if re.search(r"<meta(?![^>]*/?>)", fixed):
    print("WARN: a meta tag lacks self-close or trailing >")
if re.search(r"<link(?![^>]*/?>)", fixed):
    print("WARN: a link tag lacks self-close or trailing >")

# 3. Text-content parity between original and repaired
def text_of(html):
    # remove comments, script, style blocks first
    html = re.sub(r"<!--.*?-->", "", html, flags=re.S)
    html = re.sub(r"<script.*?</script>", "", html, flags=re.S)
    # remove all tags
    text = re.sub(r"<[^>]*>", "", html)
    # normalize entities & whitespace
    text = text.replace("&nbsp;", " ").replace("&amp;", "&").replace("&copy;", "(c)")
    return re.sub(r"\s+", " ", text).strip()

t_orig = text_of(orig)
t_fixed = text_of(fixed)
print("\norig text len:", len(t_orig))
print("fixed text len:", len(t_fixed))

# show diffs
a, b = t_orig.split(), t_fixed.split()
import difflib
diffs = list(difflib.unified_diff(a, b, lineterm="", n=0))
print("text diff lines:", len(diffs))
for d in diffs[:30]:
    print("  ", d)