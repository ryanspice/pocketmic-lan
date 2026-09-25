# PocketMic localization status

`target-locales.json` is the product-wide target list. A target is not a completed translation or a supported locale.

The Android app currently uses Canadian English (`en-CA`) as its source and fallback. It exposes separate `en-CA` and `en-US` app-language choices and includes preliminary Central Kurdish/Sorani (`ckb`, RTL Arabic script) and Northern Kurdish/Kurmanji (`kmr`, LTR Latin script) catalogs. The Kurdish text is a draft and has not had fluent review. Do not describe Kurdish as reviewed or fully supported until a fluent reviewer accepts the terminology, protocol messages, and layout on a device.

Android currently validates `en-CA`, `en-US`, `ckb`, and `kmr`; Kurdish is still an unreviewed draft. The iOS and macOS apps now share an Apple String Catalog with `en-CA` as the source locale and a separate `en-US` locale. Their English catalog entries still need regional review, and neither Apple app has Kurdish translations yet. Check `apple/catalog-status.json` for Apple-specific status. Windows and the marketing site still need their own localization work and must not inherit another platform's status.

Run `python localization/validate_android_locales.py` to check Android resource XML, locale registration, fallback coverage, and format placeholders. Android CI runs this check before building.

Run `python localization/validate_apple_catalog.py` to check shared Apple catalog coverage, en-US entries, and en-CA development-region configuration. iOS and macOS CI run this check before generating their Xcode projects.
