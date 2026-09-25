#!/usr/bin/env python3
"""Validate Android locale resources against the Canadian English source catalog."""

from __future__ import annotations

import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
RES = ROOT / "android/app/src/main/res"
SOURCE_LOCALE = "en-CA"
PLACEHOLDER = re.compile(r"%(?:[1-9][0-9]*\$)?[-+#0-9.]*[a-zA-Z]")


def parse_catalog(path: Path) -> dict[str, str]:
    root = ET.parse(path).getroot()
    values: dict[str, str] = {}
    for entry in root.findall("string"):
        name = entry.attrib.get("name")
        if not name:
            raise ValueError(f"{path}: string without a name")
        if name in values:
            raise ValueError(f"{path}: duplicate string resource {name}")
        values[name] = "".join(entry.itertext())
    return values


def folder_locale(folder: str) -> str | None:
    if folder == "values":
        return SOURCE_LOCALE
    if not folder.startswith("values-"):
        return None
    qualifier = folder.removeprefix("values-")
    if qualifier.startswith("b+"):
        return qualifier[2:].replace("+", "-")
    match = re.fullmatch(r"([a-z]{2,3})-r([A-Z]{2})", qualifier)
    return f"{match.group(1)}-{match.group(2)}" if match else None


def main() -> int:
    errors: list[str] = []
    source_path = RES / "values/strings.xml"
    try:
        source = parse_catalog(source_path)
        locales_root = ET.parse(RES / "xml/locales_config.xml").getroot()
        locales = [entry.attrib["{http://schemas.android.com/apk/res/android}name"] for entry in locales_root]
        target_data = json.loads((ROOT / "localization/target-locales.json").read_text(encoding="utf-8"))
        status_data = json.loads((ROOT / "localization/catalog-status.json").read_text(encoding="utf-8"))
    except (OSError, ET.ParseError, KeyError, ValueError, json.JSONDecodeError) as exc:
        print(f"Localization validation failed: {exc}", file=sys.stderr)
        return 1

    if target_data.get("defaultLocale") != SOURCE_LOCALE:
        errors.append(f"target registry defaultLocale must remain {SOURCE_LOCALE}")
    properties = (RES / "resources.properties").read_text(encoding="utf-8") if (RES / "resources.properties").exists() else ""
    if f"unqualifiedResLocale={SOURCE_LOCALE}" not in properties.splitlines():
        errors.append(f"Android resources.properties must declare unqualifiedResLocale={SOURCE_LOCALE}")
    if SOURCE_LOCALE not in locales:
        errors.append(f"Android locale config must include fallback locale {SOURCE_LOCALE}")
    if len(locales) != len(set(locales)):
        errors.append("Android locale config contains duplicate locale tags")

    app_catalogs: dict[str, dict[str, str]] = {SOURCE_LOCALE: source}
    for folder in RES.iterdir():
        locale = folder_locale(folder.name)
        catalog_path = folder / "strings.xml"
        if locale is None or not catalog_path.is_file():
            continue
        try:
            app_catalogs[locale] = parse_catalog(catalog_path)
        except (OSError, ET.ParseError, ValueError) as exc:
            errors.append(str(exc))

    target_tags = {entry.get("tag") for entry in target_data.get("locales", [])}
    status_locales = status_data.get("locales", {})
    for locale in locales:
        if locale not in target_tags:
            errors.append(f"Android locale {locale} is missing from localization/target-locales.json")
        if locale not in status_locales:
            errors.append(f"Android locale {locale} is missing from localization/catalog-status.json")
        if locale not in app_catalogs and locale != SOURCE_LOCALE:
            errors.append(f"Android locale {locale} has no resource catalog")
        if status_locales.get(locale, {}).get("status") == "supported" and not status_locales[locale].get("reviewed"):
            errors.append(f"Android locale {locale} cannot be supported before review")

    for locale in locales:
        catalog = app_catalogs.get(locale)
        if catalog is None:
            continue
        for key, translated in catalog.items():
            if key not in source:
                errors.append(f"{locale}: unknown resource {key}")
                continue
            expected = sorted(PLACEHOLDER.findall(source[key]))
            actual = sorted(PLACEHOLDER.findall(translated))
            if expected != actual:
                errors.append(f"{locale}:{key}: placeholders {actual} do not match source {expected}")
        if locale in {"en-US", "ckb", "kmr"}:
            missing = sorted(source.keys() - catalog.keys())
            if missing:
                errors.append(f"{locale}: incomplete catalog, missing {', '.join(missing)}")

    if errors:
        print("Android localization validation failed:", file=sys.stderr)
        for error in errors:
            print(f"- {error}", file=sys.stderr)
        return 1

    print(
        f"Android locale resources valid: fallback {SOURCE_LOCALE}; "
        f"app languages {', '.join(locales)}; complete non-fallback catalogs and placeholders verified."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
