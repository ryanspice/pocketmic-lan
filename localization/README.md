# PocketMic localization status

`target-locales.json` is the product-wide target list. A target is not a completed translation or a supported locale.

The Android app currently uses Canadian English (`en-CA`) as its source and fallback. It exposes separate `en-CA` and `en-US` app-language choices and includes preliminary Central Kurdish/Sorani (`ckb`, RTL Arabic script) and Northern Kurdish/Kurmanji (`kmr`, LTR Latin script) catalogs. The Kurdish text is a draft and has not had fluent review. Do not describe Kurdish as reviewed or fully supported until a fluent reviewer accepts the terminology, protocol messages, and layout on a device.

The marketing site keeps `en-CA` as its source and fallback. Core pages offer a labelled `en-US` spelling preview for browser preference or manual selection. It only adapts the Canadian `licence`/`behaviour` spellings; it is not a full US editorial review or translation. The site has no Kurdish translations yet. `site/catalog-status.json` tracks those states separately from the Android and Apple catalogs.

Android currently validates `en-CA`, `en-US`, `ckb`, and `kmr`; Kurdish remains unreviewed. The iOS and macOS apps share an Apple String Catalog with `en-CA` as source and project development language, an `en-US` target, and 38 Kurdish draft strings for Sorani (`ckb`, RTL) and Kurmanji (`kmr` in the product registry, emitted as Apple locale `ku-Latn`, LTR). The explicit script tag preserves Kurmanji's distinction in Apple's built resources. These drafts need fluent review and device layout checks before support can be claimed. Check `apple/catalog-status.json` for Apple-specific status. Windows and the marketing site still need their own localization work and must not inherit another platform's status.

Run `python localization/validate_android_locales.py` to check Android resource XML, locale registration, fallback coverage, and format placeholders. Android CI runs this check before building.

Run `python localization/validate_apple_catalog.py` to check shared Apple UI and InfoPlist catalogs, en-US slots, review-needed ckb/kmr drafts, and en-CA Xcode project development language. iOS and macOS CI run this check before generating their Xcode projects.

Run `python localization/verify_apple_bundle.py --platform ios <path-to-built-app.app>` or use `--platform macos` for a Mac bundle. It checks the packaged `en-CA` development region, complete English/Kurdish UI catalogs, and translated local-network/microphone purpose strings. iOS and macOS CI run this against the built app bundles.
