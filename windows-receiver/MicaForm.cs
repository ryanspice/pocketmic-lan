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
    private bool _applyingBackdrop;

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        ApplyBackdrop();
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        _micaFrameEnabled = false;
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
            var gutter = (int)Math.Round(GutterWidth * DeviceDpi / 96.0);
            // The documented system-backdrop attribute starts at Windows 11 22H2. Do not
            // substitute the undocumented, version-dependent Mica attribute used on 21H2.
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621))
            {
                var backdrop = SystemInformation.HighContrast ? NoBackdrop : MainWindowBackdrop;
                if (DwmSetWindowAttribute(Handle, SystemBackdropType, ref backdrop, sizeof(int)) >= 0)
                {
                    // Keep the client content outside the glass region. A negative margin
                    // extends glass through every GDI/native control in the window.
                    var margins = new Margins(backdrop == MainWindowBackdrop ? gutter : 0);
                    enabled = DwmExtendFrameIntoClientArea(Handle, ref margins) >= 0
                        && backdrop == MainWindowBackdrop;
                    if (!enabled)
                    {
                        backdrop = NoBackdrop;
                        DwmSetWindowAttribute(Handle, SystemBackdropType, ref backdrop, sizeof(int));
                    }
                }
            }

            _micaFrameEnabled = enabled;
            Padding = enabled
                ? new Padding(gutter)
                : Padding.Empty;
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
