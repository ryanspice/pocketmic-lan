#!/usr/bin/env python3
"""Validate shared iOS/macOS String Catalog coverage and locale metadata."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
CATALOG_PATH = ROOT / "localization" / "apple" / "Localizable.xcstrings"
INFO_CATALOG_PATH = ROOT / "localization" / "apple" / "InfoPlist.xcstrings"
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
        info_catalog = json.loads(INFO_CATALOG_PATH.read_text(encoding="utf-8"))
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
    if locales.get("kmr", {}).get("appleCatalogLocale") != "ku-Latn":
        print("Apple's Kurmanji catalog locale must remain ku-Latn to retain the Latin-script variant.", file=sys.stderr)
        return 1
    strings = catalog.get("strings")
    if not isinstance(strings, dict):
        print("Apple String Catalog must contain a strings object.", file=sys.stderr)
        return 1
    info_strings = info_catalog.get("strings")
    required_info_keys = {"NSLocalNetworkUsageDescription", "NSMicrophoneUsageDescription"}
    if info_catalog.get("sourceLanguage") != "en-CA" or not isinstance(info_strings, dict):
        print("Apple InfoPlist String Catalog must use en-CA and contain a strings object.", file=sys.stderr)
        return 1
    if set(info_strings) != required_info_keys:
        print("Apple InfoPlist catalog must contain the local-network and microphone purpose strings.", file=sys.stderr)
        return 1

    errors: list[str] = []
    for key, entry in strings.items():
        localizations = entry.get("localizations", {})
        unit = localizations.get("en-US", {}).get("stringUnit", {})
        if unit.get("state") != "translated" or not isinstance(unit.get("value"), str):
            errors.append(f"Missing en-US catalog value for {key!r}.")
        for tag, apple_tag in (("ckb", "ckb"), ("kmr", "ku-Latn")):
            draft = localizations.get(apple_tag, {}).get("stringUnit", {})
            if draft.get("state") != "needs_review" or not isinstance(draft.get("value"), str):
                errors.append(f"Missing {tag} ({apple_tag}) review-needed draft for {key!r}.")

    for project in PROJECTS:
        content = project.read_text(encoding="utf-8")
        if "../localization/apple/Localizable.xcstrings" not in content:
            errors.append(f"{project.relative_to(ROOT)} does not include the shared catalog.")
        if "../localization/apple/InfoPlist.xcstrings" not in content:
            errors.append(f"{project.relative_to(ROOT)} does not include the localized purpose-string catalog.")
        if "developmentLanguage: en-CA" not in content:
            errors.append(f"{project.relative_to(ROOT)} must set XcodeGen developmentLanguage to en-CA.")
        if "INFOPLIST_KEY_CFBundleDevelopmentRegion: en-CA" not in content:
            errors.append(f"{project.relative_to(ROOT)} must use en-CA as its development region.")
        required_purpose_keys = (
            ("NSLocalNetworkUsageDescription", "NSMicrophoneUsageDescription")
            if project.parts[-2] == "ios"
            else ("INFOPLIST_KEY_NSLocalNetworkUsageDescription",)
        )
        for key in required_purpose_keys:
            if key not in content:
                errors.append(f"{project.relative_to(ROOT)} is missing its {key} purpose string.")

    source_keys: set[str] = set()
    for source_root in SWIFT_ROOTS:
        for source_path in source_root.rglob("*.swift"):
            source_keys.update(swift_keys(source_path.read_text(encoding="utf-8")))
    for key in sorted(source_keys - strings.keys()):
        errors.append(f"Swift UI string is absent from Localizable.xcstrings: {key!r}.")

    for key, entry in info_strings.items():
        localizations = entry.get("localizations", {})
        for locale in ("en-US", "ckb", "ku-Latn"):
            unit = localizations.get(locale, {}).get("stringUnit", {})
            expected_state = "translated" if locale == "en-US" else "needs_review"
            if unit.get("state") != expected_state or not isinstance(unit.get("value"), str) or not unit["value"].strip():
                errors.append(f"Missing {expected_state} {locale} purpose string for {key!r}.")

    if errors:
        print("Apple localization validation failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print(f"Apple localization validation passed: {len(strings)} UI strings, InfoPlist purpose strings, en-CA development/source locale, en-US target coverage, ckb drafts, and kmr drafts mapped to ku-Latn.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
