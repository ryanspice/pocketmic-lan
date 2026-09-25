#!/usr/bin/env python3
"""Validate shared iOS/macOS String Catalog coverage and locale metadata."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CATALOG_PATH = ROOT / "localization" / "apple" / "Localizable.xcstrings"
PROJECTS = (ROOT / "ios" / "project.yml", ROOT / "macos" / "project.yml")
SWIFT_ROOTS = (ROOT / "ios" / "Sources", ROOT / "macos" / "Sources")
STATUS_PATH = ROOT / "localization" / "apple" / "catalog-status.json"


def swift_keys(source: str) -> set[str]:
    patterns = (
        r"(?:Text|Section|GroupBox|TextField|SecureField|Button)\s*\(\s*\"((?:\\.|[^\"\\])*)\"",
        r"\.(?:navigationTitle|alert)\s*\(\s*\"((?:\\.|[^\"\\])*)\"",
        r"String\s*\(\s*localized:\s*\"((?:\\.|[^\"\\])*)\"",
    )
    found: set[str] = set()
    for pattern in patterns:
        found.update(re.findall(pattern, source))
    return found


def main() -> int:
    try:
        catalog = json.loads(CATALOG_PATH.read_text(encoding="utf-8"))
        status = json.loads(STATUS_PATH.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError) as error:
        print(f"Invalid Apple String Catalog: {error}", file=sys.stderr)
        return 1

    if catalog.get("sourceLanguage") != "en-CA":
        print("Apple String Catalog sourceLanguage must remain en-CA.", file=sys.stderr)
        return 1
    if status.get("defaultLocale") != "en-CA" or status.get("platforms") != ["ios", "macos"]:
        print("Apple localization status must cover iOS/macOS with en-CA as default.", file=sys.stderr)
        return 1
    locales = status.get("locales", {})
    if locales.get("en-US", {}).get("status") != "target" or locales.get("en-US", {}).get("reviewed"):
        print("en-US must remain an unreviewed target until regional copy review is complete.", file=sys.stderr)
        return 1
    for tag in ("ckb", "kmr"):
        if locales.get(tag, {}).get("status") != "draft" or locales.get(tag, {}).get("reviewed"):
            print(f"{tag} must remain an unreviewed draft until Apple translations are reviewed.", file=sys.stderr)
            return 1
    strings = catalog.get("strings")
    if not isinstance(strings, dict):
        print("Apple String Catalog must contain a strings object.", file=sys.stderr)
        return 1

    errors: list[str] = []
    for key, entry in strings.items():
        localizations = entry.get("localizations", {})
        unit = localizations.get("en-US", {}).get("stringUnit", {})
        if unit.get("state") != "translated" or not isinstance(unit.get("value"), str):
            errors.append(f"Missing en-US catalog value for {key!r}.")
        for tag in ("ckb", "kmr"):
            draft = localizations.get(tag, {}).get("stringUnit", {})
            if draft.get("state") != "needs_review" or not isinstance(draft.get("value"), str):
                errors.append(f"Missing {tag} review-needed draft for {key!r}.")

    for project in PROJECTS:
        content = project.read_text(encoding="utf-8")
        if "../localization/apple/Localizable.xcstrings" not in content:
            errors.append(f"{project.relative_to(ROOT)} does not include the shared catalog.")
        if "INFOPLIST_KEY_CFBundleDevelopmentRegion: en-CA" not in content:
            errors.append(f"{project.relative_to(ROOT)} must use en-CA as its development region.")

    source_keys: set[str] = set()
    for source_root in SWIFT_ROOTS:
        for source_path in source_root.rglob("*.swift"):
            source_keys.update(swift_keys(source_path.read_text(encoding="utf-8")))
    for key in sorted(source_keys - strings.keys()):
        errors.append(f"Swift UI string is absent from Localizable.xcstrings: {key!r}.")

    if errors:
        print("Apple localization validation failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print(f"Apple localization validation passed: {len(strings)} strings, en-CA source, en-US target coverage, and ckb/kmr review-needed drafts.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
