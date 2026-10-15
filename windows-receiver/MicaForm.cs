using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PocketMicReceiver;

/// <summary>
/// Gives the native title bar and the gutter around the content a Windows 11 Mica backdrop.
/// The content remains an opaque WinForms surface: extending glass through GDI controls can
/// erase their text and backgrounds. Older Windows versions keep the ordinary solid frame.
/// </summary>
internal class MicaForm : Form
{
    private const int SystemBackdropType = 38; // DWMWA_SYSTEMBACKDROP_TYPE
    private const int NoBackdrop = 1; // DWMSBT_NONE
    private const int MainWindowBackdrop = 2; // DWMSBT_MAINWINDOW (Mica on Windows 11)
    private const int GutterWidth = 12;
    private bool _micaFrameEnabled;
    private int _nativeGutter;
    private bool _applyingBackdrop;

    // Keep native policy/calls overridable so failure and platform branches can be exercised
    // without changing the machine's accessibility or display settings.
    protected virtual bool SupportsSystemBackdrop => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621);
    protected virtual bool IsHighContrast => SystemInformation.HighContrast;
    protected virtual int FrameDpi => DeviceDpi;

    protected virtual int SetSystemBackdrop(int backdrop) =>
        DwmSetWindowAttribute(Handle, SystemBackdropType, ref backdrop, sizeof(int));

    protected virtual int ExtendFrame(int width)
    {
        var margins = new Margins(width);
        return DwmExtendFrameIntoClientArea(Handle, ref margins);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyBackdrop();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _micaFrameEnabled = false;
        _nativeGutter = 0;
        base.OnHandleDestroyed(e);
    }

    protected override void OnDpiChanged(DpiChangedEventArgs e)
    {
        base.OnDpiChanged(e);
        ApplyBackdrop();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        base.OnPaintBackground(e);
        if (_micaFrameEnabled)
        {
            // Expose Mica only inside the bounded frame. Painting the entire client black
            // or extending glass with -1 makes GDI text participate in DWM alpha blending,
            // even when its containing WinForms panel has an opaque BackColor.
            e.Graphics.FillRectangle(Brushes.Black, 0, 0, ClientSize.Width, Padding.Top);
            e.Graphics.FillRectangle(Brushes.Black, 0, ClientSize.Height - Padding.Bottom, ClientSize.Width, Padding.Bottom);
            e.Graphics.FillRectangle(Brushes.Black, 0, 0, Padding.Left, ClientSize.Height);
            e.Graphics.FillRectangle(Brushes.Black, ClientSize.Width - Padding.Right, 0, Padding.Right, ClientSize.Height);
        }
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        // Windows can change composition, contrast and transparency while the app is open.
        if (m.Msg is 0x001A or 0x031A or 0x031E) // WM_SETTINGCHANGE / THEMECHANGED / DWMCOMPOSITIONCHANGED
            ApplyBackdrop();
    }

    private void ApplyBackdrop()
    {
        if (_applyingBackdrop || !IsHandleCreated || Disposing || IsDisposed) return;

        _applyingBackdrop = true;
        try
        {
            var enabled = false;
            var gutter = (int)Math.Round(GutterWidth * FrameDpi / 96.0);
            // The documented system-backdrop attribute starts at Windows 11 22H2. Do not
            // substitute the undocumented, version-dependent Mica attribute used on 21H2.
            if (SupportsSystemBackdrop)
            {
                var backdrop = IsHighContrast ? NoBackdrop : MainWindowBackdrop;
                if (SetSystemBackdrop(backdrop) >= 0)
                {
                    // Keep the client content outside the glass region. A negative margin
                    // extends glass through every GDI/native control in the window.
                    var requestedGutter = backdrop == MainWindowBackdrop ? gutter : 0;
                    if (ExtendFrame(requestedGutter) >= 0)
                    {
                        _nativeGutter = requestedGutter;
                        enabled = backdrop == MainWindowBackdrop;
                    }
                }
                if (!enabled)
                {
                    // Both resets are best effort, including when the first attribute call
                    // failed. If the native frame cannot be cleared, retain its last known
                    // gutter so controls never move into glass left on the current HWND.
                    if (ExtendFrame(0) >= 0) _nativeGutter = 0;
                    SetSystemBackdrop(NoBackdrop);
                }
            }

            _micaFrameEnabled = enabled;
            // Failed material updates paint the ordinary solid background, while a retained
            // native gutter still reserves space independently of the material's state.
            Padding = new Padding(_nativeGutter);
            Invalidate(true);
        }
        finally
        {
            _applyingBackdrop = false;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;

        public Margins(int width) => Left = Right = Top = Bottom = width;
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);
}
