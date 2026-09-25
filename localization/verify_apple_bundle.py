#!/usr/bin/env python3
"""Verify the packaged Apple app carries its declared locale resources."""

from __future__ import annotations

import argparse
import plistlib
import sys
from pathlib import Path

EXPECTED_LOCALIZATIONS = {"en-US", "ckb", "ku-Latn"}
EXPECTED_STRING_COUNT = 38


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("bundle", type=Path, help="Path to a built .app bundle")
    args = parser.parse_args()

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
    for locale in sorted(EXPECTED_LOCALIZATIONS):
        strings_path = args.bundle / f"{locale}.lproj" / "Localizable.strings"
        try:
            strings = plistlib.loads(strings_path.read_bytes())
        except (OSError, plistlib.InvalidFileException) as error:
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
