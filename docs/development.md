# Development and packaging

Run commands from the repository root in PowerShell unless stated otherwise. Inspect scripts before running them: setup downloads sources, builds generate files, publishing writes packages, and installers change the selected machine. The [CI workflow](../.github/workflows/ci.yml) is the executable reference for the complete Android/Windows release checks.

## Android

Use Android SDK Platform 36, NDK `27.2.12479018`, CMake `3.22.1`, and Android Studio's JBR or a compatible JDK 17+. `scripts/build-android.ps1` locates the SDK/JDK and uses the committed Gradle wrapper; do not install a separate Gradle version.

```powershell
.\scripts\setup-libopus.ps1
.\scripts\build-android.ps1
```

Setup downloads and verifies pinned libopus 1.5.2 source. The build helper normally runs JVM tests and lint as well as the APK build. Its versioned output is `release/PocketMic-v<versionName>-debug.apk`; the published 0.1.7 asset is named `app-debug.apk`.

```powershell
.\scripts\build-android.ps1 -SkipClean
.\scripts\build-android.ps1 -SkipClean -SkipChecks
.\scripts\build-android.ps1 -Configuration Release
```

`-SkipChecks` omits test/lint evidence; Release output is unsigned. Debug builds from different machines may have different signing certificates. Android `com.canopydigital.pocketmic` installs separately from the old `com.ryanspice.pocketmic`; do not promise settings migration or use uninstall as a routine update workaround.

## Windows

Use .NET 8 SDK on Windows. The modern project restores its Windows App SDK/build tooling through NuGet. `windows-receiver/` contains the launcher/classic interface, `windows-receiver-winui/` the modern UI and `windows-receiver-core/` the shared engine.

```powershell
dotnet build windows-receiver/PocketMicReceiver.csproj -c Release
dotnet build windows-receiver-tests/PocketMicReceiver.Tests.csproj -c Release
```

The full test suite includes native Opus smoke tests. Build the pinned x64 Opus DLL using the checksum and CMake configuration in the Windows CI job, then stage that DLL into the test output before running the suite. For example, after setting `$opusDllPath` to that verified local file:

```powershell
Copy-Item -LiteralPath $opusDllPath -Destination windows-receiver-tests/bin/Release/net8.0-windows/opus.dll
dotnet test windows-receiver-tests/PocketMicReceiver.Tests.csproj -c Release --no-build
.\scripts\publish-windows.ps1 -OpusDllPath $opusDllPath
.\scripts\test-windows-installer.ps1
```

The publisher builds the modern and compatibility interfaces and produces `release/PocketMicReceiver-win-x64.zip`. It does not run the test suite. Without `-OpusDllPath`, a locally published package does not acquire a decoder automatically; do not claim release-equivalent Opus packaging. The 0.1.7 download includes the pinned decoder and notices.

`-ClassicOnly` is for explicit compatibility builds, not the default release. The modern payload must include app XBF/PRI resources, runtime dependencies and notices. Package hashing and nested-path validation must cover the complete payload.

Keep [PACKAGE-README.md](../windows-receiver/PACKAGE-README.md), [THIRD-PARTY-NOTICES.md](../windows-receiver/THIRD-PARTY-NOTICES.md), [OPUS-COPYING.txt](../windows-receiver/OPUS-COPYING.txt) and [LICENSE](../LICENSE) in their current source locations: the publisher consumes them. Installation instructions belong to that package guide; do not move its inputs merely to simplify `docs/`.

## Apple previews and localization

Windows contributors can edit the Apple source and use [iOS CI](../.github/workflows/ios.yml) and [macOS CI](../.github/workflows/macos.yml). XcodeGen generates projects on the hosted Mac runner. Ordinary iOS source builds exercise the simulator; tag/release builds produce an unsigned archive. Mac CI builds the universal app and virtual microphone driver, packages tester files and verifies the archives.

See [Mac setup](../macos/README.md), [localization status](../localization/README.md) and the [0.1.8 plan](plans/0.1.8-apple.md). Signing and store upload are not configured by those unsigned workflows; do not invent a supported secret-name contract or put credentials in documentation.

## Website ownership

`dev/v3/` is the canonical static-site candidate. Its validation is:

```powershell
node --check dev/v3/assets/marketing-home.js
node --check dev/v3/assets/subsite.js
node --check dev/v3/assets/site-locale.js
node --test scripts/site-release-contract.test.mjs
node scripts/validate-site-release.mjs
```

`dev/v3/release.json` records separate stable and development-prerelease channels with version-pinned assets, hashes and sizes. Stable remains the default download; prerelease links stay in explicitly labelled opt-in sections. The release-contract tests cover channel mix-ups, stale versions and metadata changes, and run in website CI. Update the manifest and affected static pages together after verifying published release metadata.

[Lighthouse](../.github/workflows/lighthouse.yml) also targets `dev/v3/`. `tools/verify_web.py` validates the retained legacy `web/` demo, not the deployment candidate. README screenshot assets still live under `web/`; `web-v2-compare/` contains comparison material. Keep these trees until a separate dependency/asset inventory justifies restructuring. A source build does not deploy the website.

## Generated files and release identity

Use existing ignored output directories. `release/` can contain backups and audit receipts as well as generated packages; never clean it wholesale. Temporary workspaces belong under ignored `workspaces/` when needed. The old version copies and registered worktrees are preservation boundaries until inspected.

Run `python scripts/verify_release_versions.py` when preparing a candidate. Keep app versions unchanged for documentation-only work, and never move an existing release tag. [Testing](testing.md) defines evidence boundaries; the [0.1.7 record](releases/0.1.7/release.md) preserves that release's results.
