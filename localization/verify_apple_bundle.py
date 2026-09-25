#!/usr/bin/env python3
"""Verify the packaged Apple app carries its declared locale resources."""

from __future__ import annotations

import argparse
import plistlib
import re
import sys
from xml.parsers.expat import ExpatError
from pathlib import Path

EXPECTED_LOCALIZATIONS = {"en-US", "ckb", "ku-Latn"}
EXPECTED_STRING_COUNT = 38


def read_strings(path: Path) -> dict[str, object]:
    data = path.read_bytes()
    try:
        strings = plistlib.loads(data)
        if isinstance(strings, dict):
            return strings
    except (plistlib.InvalidFileException, ExpatError, ValueError):
        pass

    try:
        text = data.decode("utf-8-sig")
    except UnicodeDecodeError:
        text = data.decode("utf-16")

    if text.lstrip().startswith("<?xml"):
        normalized = re.sub(
            r'(?i)(<\?xml[^>]*encoding\s*=\s*["\'])[^"\']+(["\'])',
            r"\1UTF-8\2",
            text,
            count=1,
        )
        try:
            strings = plistlib.loads(normalized.encode("utf-8"))
            if isinstance(strings, dict):
                return strings
        except (plistlib.InvalidFileException, ExpatError, ValueError):
            pass

    entries = re.findall(r'^\s*("(?:\\.|[^"\\])*")\s*=\s*("(?:\\.|[^"\\])*")\s*;\s*$', text, re.MULTILINE)
    if entries:
        return {key: value for key, value in entries}
    raise ValueError("file is not a binary/XML property list or a strings key/value catalog")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("bundle", type=Path, help="Path to a built .app bundle")
    args = parser.parse_args()

    info_path = args.bundle / "Contents" / "Info.plist"
    if not info_path.is_file():
        info_path = args.bundle / "Info.plist"
    try:
        info = plistlib.loads(info_path.read_bytes())
    except (OSError, plistlib.InvalidFileException) as error:
        print(f"Cannot read app Info.plist at {info_path}: {error}", file=sys.stderr)
        return 1

    region = info.get("CFBundleDevelopmentRegion")
    if region != "en-CA":
        print(f"Expected CFBundleDevelopmentRegion en-CA, found {region!r}.", file=sys.stderr)
        return 1

    errors: list[str] = []
    resources = args.bundle / "Contents" / "Resources"
    if not resources.is_dir():
        resources = args.bundle
    for locale in sorted(EXPECTED_LOCALIZATIONS):
        strings_path = resources / f"{locale}.lproj" / "Localizable.strings"
        try:
            strings = read_strings(strings_path)
        except (OSError, UnicodeDecodeError, ValueError, plistlib.InvalidFileException) as error:
            errors.append(f"Cannot read {locale} resources: {error}")
            continue
        if not isinstance(strings, dict) or len(strings) != EXPECTED_STRING_COUNT:
            count = len(strings) if isinstance(strings, dict) else "invalid"
            errors.append(f"{locale} must contain {EXPECTED_STRING_COUNT} strings; found {count}.")

    if errors:
        print("Apple app bundle localization validation failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print(
        f"Apple app bundle localization passed: en-CA default and {EXPECTED_STRING_COUNT} strings each for en-US, ckb, and ku-Latn."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
