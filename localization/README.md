# PocketMic localization status

`target-locales.json` is the product-wide target list. A target is not a completed translation or a supported locale.

The Android app currently uses Canadian English (`en-CA`) as its source and fallback. It exposes separate `en-CA` and `en-US` app-language choices and includes preliminary Central Kurdish/Sorani (`ckb`, RTL Arabic script) and Northern Kurdish/Kurmanji (`kmr`, LTR Latin script) catalogs. The Kurdish text is a draft and has not had fluent review. Do not describe Kurdish as reviewed or fully supported until a fluent reviewer accepts the terminology, protocol messages, and layout on a device.

The Android language configuration is intentionally limited to those four tags. Other product-wide targets remain untranslated in Android. iOS, macOS, Windows, and the marketing site have separate localization work and must not inherit Android's status.

Run `python localization/validate_android_locales.py` to check Android resource XML, locale registration, fallback coverage, and format placeholders. Android CI runs this check before building.
