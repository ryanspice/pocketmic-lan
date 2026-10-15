#!/usr/bin/env python3
"""Check local inline Markdown links in tracked and non-ignored Markdown files.

Supports inline links/images and ATX headings used by this repository. Skips
fenced code, external URLs and non-Markdown fragments; does not fetch websites
or validate reference-style links. Uses only Python's standard library and Git.
"""

from pathlib import Path
import re
import subprocess
import sys
from urllib.parse import unquote, urlsplit


ROOT = Path(__file__).resolve().parents[1]
LINK = re.compile(r'\]\(\s*(?:<([^>]+)>|([^\s)]+))(?:\s+"[^"]*")?\s*\)')


def prose(text):
    fence = None
    for line in text.splitlines():
        marker = re.match(r"^\s{0,3}(`{3,}|~{3,})", line)
        if marker:
            token = marker[1]
            if fence is None:
                fence = token
            elif token[0] == fence[0] and len(token) >= len(fence):
                fence = None
            yield ""
        else:
            yield line if fence is None else ""


def anchors(text):
    found = set(re.findall(r'\b(?:id|name)=["\']([^"\']+)["\']', text))
    counts = {}
    for line in prose(text):
        heading = re.match(r"^ {0,3}#{1,6}\s+(.+?)\s*#*\s*$", line)
        if not heading:
            continue
        label = re.sub(r"!?\[([^]]*)\]\([^)]*\)", r"\1", heading[1])
        label = re.sub(r"<[^>]+>", "", label).replace("`", "").replace("*", "")
        slug = re.sub(r"[^\w\- ]", "", label.lower()).replace(" ", "-")
        count = counts.get(slug, 0)
        counts[slug] = count + 1
        found.add(f"{slug}-{count}" if count else slug)
    return found


def main():
    names = subprocess.check_output(
        ["git", "ls-files", "--cached", "--others", "--exclude-standard", "-z", "--", "*.md"],
        cwd=ROOT,
    ).decode("utf-8").split("\0")
    files = sorted({ROOT / name for name in names if name and (ROOT / name).is_file()})
    errors = []
    checked = 0
    cache = {}
    for file in files:
        content = file.read_text(encoding="utf-8-sig")
        for number, line in enumerate(prose(content), 1):
            for match in LINK.finditer(line):
                url = match[1] or match[2]
                parsed = urlsplit(url)
                if parsed.scheme or parsed.netloc:
                    continue
                target = (file.parent / unquote(parsed.path)).resolve() if parsed.path else file
                checked += 1
                problem = None
                if not target.is_relative_to(ROOT):
                    problem = "destination is outside the repository"
                elif not target.exists():
                    problem = "missing destination"
                elif parsed.fragment and target.is_file() and target.suffix.lower() == ".md":
                    if target not in cache:
                        cache[target] = anchors(target.read_text(encoding="utf-8-sig"))
                    if unquote(parsed.fragment) not in cache[target]:
                        problem = "missing Markdown heading/anchor"
                if problem:
                    errors.append(f"{file.relative_to(ROOT).as_posix()}:{number}: {problem}: {url}")
    for error in errors:
        print(error, file=sys.stderr)
    print(f"Checked {checked} local inline links in {len(files)} Markdown files; {len(errors)} errors.")
    return 1 if errors else 0


if __name__ == "__main__":
    raise SystemExit(main())
