#!/usr/bin/env python3
"""Deterministic structural repair for worker-minimax's index.html output.

The minimax lane systematically drops the '>' on closing tags and glues
attribute values to closing tags. This script repairs ONLY those two classes
of defects; it does not touch content, claims, or styling.

Run: python repair_html.py <path-to-index.html>
"""
import re
import sys


def repair(text: str) -> str:
    original = text
    # Class B first (needs proper </tag>): quoted attribute value glued to a
    # closing tag:  aria-hidden="true</span>  ->  aria-hidden="true"></span>
    text = re.sub(r'"([^"]{1,120})</([a-z]+)>', r'"\1"></\2>', text)
    # Class A: bare closing tag missing '>' (not followed by '>' or a letter):
    #   </span</li>  ->  </span></li>
    text = re.sub(r'</([a-zA-Z]+)(?![a-zA-Z>])', r'</\1>', text)
    # Class C special case: bare attribute glued to a closing tag.
    text = text.replace('defer</script>', 'defer></script>')
    return text


def main() -> None:
    path = sys.argv[1]
    with open(path, encoding="utf-8") as f:
        text = f.read()
    fixed = repair(text)
    if fixed == text:
        print("no changes needed")
        return
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(fixed)
    before = text.count("</") - text.count("</")
    bare_before = len(re.findall(r"</([a-zA-Z]+)(?![a-zA-Z>])", text))
    bare_after = len(re.findall(r"</([a-zA-Z]+)(?![a-zA-Z>])", fixed))
    print(f"repaired: bare closing tags {bare_before} -> {bare_after}")
    print(f"bytes: {len(text.encode())} -> {len(fixed.encode())}")


if __name__ == "__main__":
    main()