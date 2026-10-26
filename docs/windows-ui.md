# Windows interface and lifecycle

Status: current implementation guide based on 0.1.7. Detailed reproduction history and dated acceptance remain in the [Mica release record](releases/0.1.7/mica.md).

## Modern canvas and compatibility

The root launcher in `windows-receiver/` selects `windows-receiver-winui/` on Windows 11 22H2 and newer. The modern window uses `MicaBackdrop`, transparent root/scrolling/page containers, native Fluent controls and system theme-resource card brushes. Preserve that full-canvas design rather than replacing the content with a flat dark background. Mica itself is an opaque wallpaper-influenced material; Windows controls its active/inactive, accessibility, transparency and power fallbacks.

Older Windows uses the classic WinForms interface. `PocketMicReceiver.exe --classic --show` selects it explicitly. An early modern startup failure opens the compatibility interface with a notice. Both front ends use the shared engine, settings and activation endpoint. The classic interface intentionally has an opaque readable control area and a bounded native Mica gutter; do not restore the full-client glass experiment that erased GDI text.

## Packaging contract

Publish the complete recursive `modern/` payload, including `App.xbf`, the app PRI file, Windows App SDK/runtime files and applicable notices. A project build that contains those files is not proof that publish copied them. The project/publisher must fail clearly when required resources are absent; verify the final ZIP as well as the directory.

The Windows package includes the Android-derived multi-size icon for executable, window, tray and shortcuts. [Package instructions](../windows-receiver/PACKAGE-README.md) remain a publisher input. [Development](development.md) explains the separate Opus DLL input for local publishing.

## Activation, tray and settings

- Keep the tray icon present while the window is visible or hidden. X hides; tray Exit stops audio and restores only microphone assignments still owned by PocketMic.
- A subsequent launch sends SHOW over the current-user/session activation channel and restores the existing receiver. Preserve the named mutex, current-user-only pipe, exact-process foreground grant and resilient listener after a disconnected client.
- Installer EXIT is distinct from SHOW and must validate the installed executable path. Preserve bounded teardown and the previous installation/settings; do not close unrelated development copies.
- Settings remain in `%APPDATA%\PocketMic\receiver.json`. The installer preserves them. Explicit `--show` must not be defeated by a minimized-start preference.
- The modern pairing card remains visible outside Advanced. Refresh labels every usable IPv4 address by adapter, ranks a physical interface with an IPv4 gateway as the recommendation, and keeps the explicit address selection for both copy and QR. The classic fallback also exposes pairing outside Advanced and ranks a physical gateway interface first; its QR dialog still offers address-only alternatives, while its copy button copies the recommendation.
- Bounded modern startup diagnostics are local and must not expose pairing keys. Secondary activation must preserve the primary process's startup log.

## Verification boundary

The release owner confirmed the corrected Mica appearance, close-to-tray and Start-menu reactivation for 0.1.7. Native paint/activation tests and the full suite provide separate deterministic evidence. High contrast, older Windows, mixed DPI, accessibility, scrolling/theme transitions and prolonged sessions require the relevant [manual checks](testing.md); do not expand a source or unit-test pass into visual acceptance.

Both desktop interfaces use the shared adapter inventory/ranker, retain an available explicit selection across refresh, label the recommended physical/default-gateway candidate and keep VPN/virtual/no-gateway alternatives. Copy and QR use that same selection. Recommendation is advisory and does not establish the phone subnet. Classic QR captions show address/port rather than printing the key-bearing URI.
