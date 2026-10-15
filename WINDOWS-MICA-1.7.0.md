# Windows Mica in 1.7.0

## Implemented behavior

The WinForms receiver requests the documented Windows 11 22H2+ system backdrop (`DWMWA_SYSTEMBACKDROP_TYPE`, value `DWMSBT_MAINWINDOW`). Mica is limited to the native title bar and a 12-pixel gutter at 96 DPI, scaled with the window DPI. The content center and native controls retain opaque backgrounds. There is no transparency key, layered-window opacity, full-client glass, or undocumented Windows 11 21H2 attribute.

Mica is an opaque, wallpaper-influenced material, not a view through the window. Windows can render a solid fallback when transparency is disabled, the window is inactive, battery saver is active, or high contrast is enabled. A subtle or solid background under those conditions is expected. See Microsoft's [Mica guidance](https://learn.microsoft.com/en-us/windows/apps/design/style/mica), [system backdrop API](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/ne-dwmapi-dwm_systembackdrop_type), and [frame extension API](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmextendframeintoclientarea).

## Review findings and correction

The first live installed-app check exposed GDI text disappearing under full-client glass. Positive, bounded frame margins corrected that. CodeCraft Opus 5.5's second review then identified a separate failure path: after a successful frame extension, a later failed DWM call dropped managed padding without clearing the old native margins. Sol 6.1 high reproduced both attribute-call and frame-extension failures before the fix (8 passing cases, 2 failing cases).

`MicaForm` now tracks the last successfully applied native gutter separately from whether it paints Mica. Every unsuccessful application on a supported platform attempts both zero frame margins and no backdrop. If Windows also rejects cleanup, the app preserves the known gutter/padding and paints its ordinary background, keeping controls outside any retained native glass. Destroying the HWND clears that tracked state. Theme, settings, composition and DPI notifications reapply the policy.

The owner then reported that the installed `29e7ae9` window was readable but showed no Mica. That invalidated visual acceptance despite successful attribute readback. A native top-down 32-bit DIB experiment reproduced the missing rendering condition: GDI+ `Brushes.Black` wrote `FF000000` (opaque), while native `FillRect` with `BLACK_BRUSH` wrote `00000000` (zero alpha). The opaque gutter covered the compositor material. The original managed-bitmap assertions checked black RGB but required the wrong alpha and therefore missed this defect. Microsoft's [custom-frame guidance](https://learn.microsoft.com/en-us/windows/win32/dwm/customframe) explicitly requires zero-alpha pixels under the extended frame. No installed-window screenshot or private desktop content was used in this experiment.

The correction uses native `FillRect(BLACK_BRUSH)` only in the gutter and explicitly paints the client background opaque. Native DIB tests now execute the real paint method and require gutter `00000000` / center `FFF0F0F0`, instead of relying on managed-bitmap copying that hides alpha behavior. The frame also reapplies on activation and opts the native caption into the documented dark-mode policy (disabled in high contrast). The caption request cannot prevent Mica from applying; it does not recolor the application controls or alter Windows settings.

## Verification and its limits

- The full local Windows Release suite passes **194 tests**, including **16 Mica cases**. The actual native smoke reports Windows `10.0.26200.0`, high contrast off, and Mica enabled; it reads back backdrop value 2, reapplies after activation/notifications, recreates the HWND, and checks opaque content/child layout.
- Injected platform/native boundaries execute the real application and painting logic: older-Windows fallback, high-contrast entry/exit, 96/120/144/192 DPI calculations, transient and repeated HRESULT failures, successful recovery and HWND replacement. Pixel checks cover the content center, first interior pixel and adjacent gutter.
- The installed window is checked separately from source and test evidence. Screenshots and package identity receipts remain in the local ignored release audit directory; the published release notes record final installation and hosted build receipts.
- Injected policy tests do not establish visual acceptance on an older Windows machine, real high contrast/transparency-disabled settings, or movement across monitors with different DPI. No global accessibility/display settings were changed for these tests. A successful DWM request alone does not prove the compositor visibly displays the material.

## Follow-up scope

Keep real multi-monitor DPI, accessibility-theme and transparency-disabled checks in the Windows manual acceptance pass. A fuller Fluent/WinUI redesign and Windows localization remain separate work; this correction preserves the current native controls and layout.
