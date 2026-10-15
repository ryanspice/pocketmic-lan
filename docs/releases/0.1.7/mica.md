# Windows Mica in 0.1.7

Status: release-specific investigation and acceptance record, 2026-10-15, source `3ef192e`. Use [the current Windows guide](../../windows-ui.md) for ongoing implementation work. Paths below are repository-relative; historical failures described here were corrected in the release unless explicitly listed as open.

## Modern interface and fallback

Windows 11 22H2 and newer use `windows-receiver-winui`, an unpackaged WinUI 3 application with Windows App SDK 2.5.1. `MainWindow.SystemBackdrop` is a `MicaBackdrop`; the root grid, scrolling surface and page stack have transparent backgrounds. Native Fluent controls and theme-resource card brushes sit above that canvas. Text, controls, scrollbars, light/dark appearance and high-contrast behavior follow WinUI resources.

The root `PocketMicReceiver.exe` selects the modern executable in `modern/`. Older Windows uses the independently runnable classic WinForms interface. `--classic --show` selects that fallback explicitly. An early modern startup failure produces a notice and opens the classic interface. Both use the same audio engine, settings and current-user/session activation endpoint, so changing front ends does not create a second receiver.

Mica is an opaque, wallpaper-influenced material, not a transparent view of other windows. Windows controls its active/inactive, transparency, battery-saver and accessibility fallbacks. A flat material under those conditions can be expected. See Microsoft's [system backdrop guidance](https://learn.microsoft.com/en-us/windows/apps/develop/ui/system-backdrops) and [Mica guidance](https://learn.microsoft.com/en-us/windows/apps/design/style/mica).

## Defects reproduced and corrected

The earlier WinForms full-client glass experiment erased GDI text. Restricting its native backdrop to the title bar and a DPI-scaled gutter preserved the controls. Independent review then found that a later DWM failure could leave stale native frame margins after managed padding was cleared. `MicaForm` now tracks the last successful margins, attempts both zero margins and no backdrop on failure, and preserves safe padding if native cleanup also fails.

The owner subsequently reported readable controls but no visible Mica. A native 32-bit DIB reproduction showed that GDI+ black wrote opaque `FF000000`, covering the material, while native `FillRect(BLACK_BRUSH)` wrote the required `00000000`. The classic gutter now uses native zero-alpha painting and an explicitly opaque content center. The owner confirmed the borders then showed Mica, but requested the entire rendering canvas and native Fluent controls. That requirement led to the WinUI interface above; the classic body palette is only compatibility presentation.

The first installed WinUI package crashed in `Microsoft.UI.Xaml.dll`. Build output contained `App.xbf` and `PocketMicReceiver.WinUI.pri`, but .NET publish omitted both. The project now explicitly publishes these generated resources and refuses to publish if they are absent. Packaging also checks the extracted payload and final ZIP entries. The corrected published application initialized its XAML resources, constructed its window and displayed successfully; the owner responded, "finally that looks like its working!" The corrected full package was then installed and launched through its Start Menu shortcut.

Bounded startup diagnostics are stored locally in `%LOCALAPPDATA%\PocketMic\receiver-winui-startup.log`. They omit settings and pairing keys; after UI initialization they retain exception types/stacks without messages. Secondary activation preserves the primary process's startup log.

## Desktop integration

- The Windows executable, window, tray and shortcuts use an icon generated from the Android launcher vector. The checked-in ICO contains sizes from 16 through 256 pixels; `scripts/build-windows-icon.ps1` regenerates it using ImageMagick.
- The tray icon remains created while the window is open. X hides the window; tray Exit stops audio, restores only microphone assignments still owned by PocketMic, and exits.
- Opening the app again sends a fixed SHOW command over a current-user-only named pipe, grants foreground permission to the exact receiver process and restores its existing window. A named mutex prevents competing owners. The installed second-launch check returned exit code 0 and retained the original modern process ID.
- The installer sends a distinct graceful EXIT command and waits for teardown before its bounded legacy shutdown fallback. The EXIT request validates the installed executable path so it cannot close a separate development copy.

## Verification and limits

- The full local Windows Release suite passes **211 tests**, including native Opus, activation lifecycle and **16 classic Mica** fallback/painting/native-boundary cases. These are not WinUI visual tests.
- Modern Debug/Release builds and a complete self-contained publish passed. A deliberate missing-resource publish fails with the expected error. The final package includes app XBF/PRI, pinned Opus, runtime files and notices.
- Installer fault injection covers nested runtime staging, inaccessible unrelated processes, bounded shutdown, graceful shutdown acknowledgement, rollback, corrupt files and unsafe/duplicate manifest paths.
- Live activation returned the exact OK acknowledgement. The development preview exited gracefully; the corrected installed receiver launched from the Start Menu shortcut, listened on its configured UDP ports and retained its process across a second launch. Shortcut target, `--show` argument and icon location were checked separately.
- The owner confirmed the corrected WinUI appearance, then confirmed that X leaves the tray icon and Start Menu activation restores the same window. Computer Use was stopped with Escape, so automated final screenshot, theme-switch and scrolling acceptance is not claimed.
- Real high contrast, transparency-disabled settings, older Windows, mixed-DPI monitors, prolonged audio sessions and accessibility navigation remain manual acceptance gates. No global display/accessibility or audio-default settings were changed for these checks.
