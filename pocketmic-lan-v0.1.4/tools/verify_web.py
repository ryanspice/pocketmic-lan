#!/usr/bin/env python3
"""Verify PocketMic's static website without third-party dependencies."""

from __future__ import annotations

import re
import subprocess
import sys
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urlsplit

ROOT = Path(__file__).resolve().parents[1]
WEB = ROOT / "web"
FORBIDDEN = (
    "hello@example.com",
    "https://YOUR-ENDPOINT",
    "pocketmic_demo_lead",
    "surprisingly decent",
    "low-latency",
)


class PageParser(HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.ids: list[str] = []
        self.refs: list[tuple[str, str]] = []
        self.h1_count = 0

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        values = dict(attrs)
        if values.get("id"):
            self.ids.append(values["id"] or "")
        if tag == "h1":
            self.h1_count += 1
        for attr in ("href", "src"):
            if values.get(attr):
                self.refs.append((attr, values[attr] or ""))


def fail(errors: list[str], message: str) -> None:
    errors.append(message)


def verify_page(path: Path, errors: list[str]) -> None:
    text = path.read_text(encoding="utf-8")
    parser = PageParser()
    parser.feed(text)
    rel = path.relative_to(ROOT)

    if parser.h1_count != 1:
        fail(errors, f"{rel}: expected one h1, found {parser.h1_count}")
    duplicates = sorted({item for item in parser.ids if parser.ids.count(item) > 1})
    if duplicates:
        fail(errors, f"{rel}: duplicate ids: {', '.join(duplicates)}")

    for forbidden in FORBIDDEN:
        if forbidden.lower() in text.lower():
            fail(errors, f"{rel}: forbidden placeholder/claim: {forbidden}")

    ids = set(parser.ids)
    for attr, ref in parser.refs:
        parsed = urlsplit(ref)
        if parsed.scheme in {"http", "https", "mailto", "tel", "data"} or ref.startswith("//"):
            continue
        if parsed.path:
            target = (path.parent / unquote(parsed.path)).resolve()
            if target.is_dir():
                target = target / "index.html"
            if not target.exists():
                fail(errors, f"{rel}: missing local {attr} target: {ref}")
        elif parsed.fragment and parsed.fragment not in ids:
            fail(errors, f"{rel}: unresolved anchor: #{parsed.fragment}")


def main() -> int:
    errors: list[str] = []
    pages = sorted(WEB.rglob("*.html"))
    scripts = sorted(WEB.rglob("*.js"))
    if not pages:
        fail(errors, "web: no HTML pages found")

    for page in pages:
        verify_page(page, errors)

    for script in scripts:
        result = subprocess.run(
            ["node", "--check", str(script)],
            capture_output=True,
            text=True,
            check=False,
        )
        if result.returncode:
            detail = (result.stderr or result.stdout).strip()
            fail(errors, f"{script.relative_to(ROOT)}: node --check failed: {detail}")

    if errors:
        print("WEB VERIFY: FAIL")
        for error in errors:
            print(f"- {error}")
        return 1

    print(
        "WEB VERIFY: PASS — "
        f"{len(pages)} HTML pages, {len(scripts)} JavaScript files, "
        "anchors/assets/placeholders checked"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
