using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PocketMicReceiver;

/// <summary>
/// Best-effort native scrollbar colouring without replacing scrolling or input behaviour.
/// SetWindowTheme is documented; the DarkMode_Explorer subclass is Windows-version-specific.
/// Unsupported themes retain native rendering. Light/high contrast restore the default theme.
/// </summary>
internal static class NativeScrollTheme
{
    private static readonly ConditionalWeakTable<Control, Registration> Registrations = new();

    // Call on the UI thread after applying the content palette. This does not create HWNDs.
    public static void Apply(Control control, bool dark, bool highContrast)
    {
        if (control.IsDisposed || control.Disposing) return;
        if (control is not ScrollableControl { AutoScroll: true } && control is not TextBox { Multiline: true }) return;
        Registrations.GetValue(control, static target => new Registration(target)).Apply(dark && !highContrast);
    }

    private sealed class Registration
    {
        private readonly Control _control;
        private bool _dark;
        private bool? _appliedDark;
        private IntPtr _appliedHandle;

        public Registration(Control control)
        {
            _control = control;
            control.HandleCreated += (_, _) => Apply(_dark);
            control.HandleDestroyed += (_, _) =>
            {
                _appliedHandle = IntPtr.Zero;
                _appliedDark = null;
            };
        }

        public void Apply(bool dark)
        {
            _dark = dark;
            if (!_control.IsHandleCreated || _control.Disposing || _control.IsDisposed) return;
            var handle = _control.Handle;
            if (_appliedHandle == handle && _appliedDark == dark) return;

            // SetWindowTheme sends WM_THEMECHANGED synchronously. Record the requested
            // state first so a theme refresh cannot recursively reapply the same override.
            _appliedHandle = handle;
            _appliedDark = dark;
            var result = SetWindowTheme(handle, dark ? "DarkMode_Explorer" : null, dark ? "ScrollBar" : null);
            if (result < 0 && dark)
                SetWindowTheme(handle, null, null);
        }
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int SetWindowTheme(IntPtr window, string? application, string? classes);
}
