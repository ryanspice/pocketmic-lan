using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Xunit;
using Xunit.Abstractions;

namespace PocketMicReceiver.Tests;

public sealed class MicaFormTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void UnsupportedWindowsAndHighContrastUseSolidBackground(bool supported, bool highContrast) => RunSta(() =>
    {
        using var form = new SimulatedFrameFixture { Supported = supported, HighContrast = highContrast };
        _ = form.Handle;

        Assert.Equal(Padding.Empty, form.Padding);
        Assert.Equal(0, form.NativeMargin);
        Assert.Equal(supported ? 1 : 0, form.NativeBackdrop);
        if (!supported) Assert.Equal(0, form.NativeCalls);
        AssertSolidCenter(form);
    });

    [Fact]
    public void HighContrastChangeClearsExistingFrameAndCanRestoreIt() => RunSta(() =>
    {
        using var form = new SimulatedFrameFixture();
        _ = form.Handle;
        Assert.Equal(12, form.NativeMargin);

        form.HighContrast = true;
        form.Notify(0x001A);
        Assert.Equal(Padding.Empty, form.Padding);
        Assert.Equal(0, form.NativeMargin);
        Assert.Equal(1, form.NativeBackdrop);
        AssertSolidCenter(form);

        form.HighContrast = false;
        form.Notify(0x001A);
        Assert.Equal(new Padding(12), form.Padding);
        Assert.Equal(12, form.NativeMargin);
        Assert.Equal(2, form.NativeBackdrop);
    });

    [Theory]
    [InlineData(96, 12)]
    [InlineData(120, 15)]
    [InlineData(144, 18)]
    [InlineData(192, 24)]
    public void ReapplicationScalesGlassAndPaddingTogether(int dpi, int expectedGutter) => RunSta(() =>
    {
        using var form = new SimulatedFrameFixture { Dpi = 192 };
        _ = form.Handle;
        Assert.Equal(24, form.NativeMargin);
        form.Dpi = dpi;
        form.Notify(0x001A);

        Assert.Equal(new Padding(expectedGutter), form.Padding);
        Assert.Equal(expectedGutter, form.NativeMargin);
        AssertSolidCenter(form);
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void NativeFailureAfterSuccessClearsPreviouslyAppliedFrame(bool failSet) => RunSta(() =>
    {
        using var form = new SimulatedFrameFixture();
        _ = form.Handle;
        Assert.Equal(12, form.NativeMargin);
        form.FailNextSet = failSet;
        form.FailNextExtend = !failSet;

        form.Notify(0x031E);

        Assert.Equal(Padding.Empty, form.Padding);
        Assert.Equal(0, form.NativeMargin);
        Assert.Equal(1, form.NativeBackdrop);
        AssertSolidCenter(form);
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RepeatedResetFailurePreservesKnownGutterUntilCleanupSucceeds(bool failSet) => RunSta(() =>
    {
        using var form = new SimulatedFrameFixture();
        _ = form.Handle;
        form.FailAllSets = failSet;
        form.FailAllExtensions = true;

        for (var attempt = 0; attempt < 3; attempt++)
        {
            form.Notify(0x031E);
            Assert.Equal(new Padding(12), form.Padding);
            Assert.Equal(12, form.NativeMargin);
            Assert.Equal(failSet ? 2 : 1, form.NativeBackdrop);
            AssertSolidCenter(form, exposesMica: false);
        }

        form.FailAllSets = false;
        form.FailAllExtensions = false;
        form.HighContrast = true;
        form.Notify(0x001A);
        Assert.Equal(Padding.Empty, form.Padding);
        Assert.Equal(0, form.NativeMargin);
        Assert.Equal(1, form.NativeBackdrop);
        AssertSolidCenter(form);

        form.HighContrast = false;
        form.Notify(0x001A);
        Assert.Equal(new Padding(12), form.Padding);
        Assert.Equal(12, form.NativeMargin);
        Assert.Equal(2, form.NativeBackdrop);
        AssertSolidCenter(form);
    });

    [Fact]
    public void HandleRecreationDoesNotCarryRetainedGutterOntoNewWindow() => RunSta(() =>
    {
        using var form = new SimulatedFrameFixture();
        _ = form.Handle;
        form.FailAllSets = true;
        form.FailAllExtensions = true;
        form.Notify(0x031E);
        Assert.Equal(new Padding(12), form.Padding);

        form.RecreateFrameHandle();

        Assert.Equal(0, form.NativeMargin);
        Assert.Equal(Padding.Empty, form.Padding);
        AssertSolidCenter(form);

        form.FailAllSets = false;
        form.FailAllExtensions = false;
        form.Notify(0x031E);
        Assert.Equal(12, form.NativeMargin);
        Assert.Equal(new Padding(12), form.Padding);
        AssertSolidCenter(form);
    });

    private static void AssertSolidCenter(FrameFixture form, bool exposesMica = true)
    {
        using var background = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.PaintFrameBackground(background);
        Assert.Equal(form.BackColor.ToArgb(), background.GetPixel(background.Width / 2, background.Height / 2).ToArgb());
        if (form.Padding == Padding.Empty || !exposesMica)
            Assert.Equal(form.BackColor.ToArgb(), background.GetPixel(0, 0).ToArgb());
        else
        {
            Assert.Equal(form.BackColor.ToArgb(), background.GetPixel(form.Padding.Left, form.Padding.Top).ToArgb());
            Assert.Equal(Color.Black.ToArgb(), background.GetPixel(form.Padding.Left - 1, form.Padding.Top).ToArgb());
            Assert.Equal(Color.Black.ToArgb(), background.GetPixel(form.Padding.Left, form.Padding.Top - 1).ToArgb());
        }
    }

    private static void RunSta(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception ex) { failure = ex; }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Window-frame fixture did not finish in 10 seconds.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    [Fact]
    public void FrameSurvivesThemeChangesAndHandleRecreationWithoutMakingControlsTransparent()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var form = new FrameFixture { ClientSize = new Size(400, 300) };
                using var content = new Panel { Dock = DockStyle.Fill, BackColor = SystemColors.Control };
                using var button = new Button { Text = "Start receiver", Bounds = new Rectangle(20, 20, 140, 40) };
                form.Controls.Add(content);
                content.Controls.Add(button);
                _ = form.Handle;
                form.PerformLayout();

                var hasMica = form.Padding.Left > 0;
                output.WriteLine($"OS: {Environment.OSVersion.Version}; high contrast: {SystemInformation.HighContrast}; Mica frame enabled: {hasMica}");
                if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621) || SystemInformation.HighContrast)
                    Assert.False(hasMica);

                AssertFrameAndControls(form, content, hasMica);
                if (hasMica)
                {
                    // Simulate an external/native reset; each notification must restore the
                    // requested backdrop rather than relying only on the initial HWND.
                    foreach (var message in new[] { 0x001A, 0x031A, 0x031E })
                    {
                        var none = 1;
                        Assert.True(DwmSetWindowAttribute(form.Handle, 38, ref none, sizeof(int)) >= 0);
                        form.Notify(message);
                        AssertFrameAndControls(form, content, hasMica);
                    }
                }

                form.RecreateFrameHandle();
                form.PerformLayout();
                AssertFrameAndControls(form, content, hasMica);

                // No transparency key or layered-window alpha: the content and its native
                // controls remain opaque children, with no chroma-keyed holes in black text.
                Assert.Equal(Color.Empty, form.TransparencyKey);
                Assert.Equal(1.0, form.Opacity);
                Assert.Equal(FormBorderStyle.Sizable, form.FormBorderStyle);
                Assert.Same(button, content.Controls[0]);
                Assert.True(button.Bounds.Contains(new Point(30, 30)));
                Assert.NotEqual(IntPtr.Zero, button.Handle);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.IsBackground = true;
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "Window-frame fixture did not finish in 10 seconds.");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void AssertFrameAndControls(FrameFixture form, Panel content, bool hasMica)
    {
        Assert.Equal(hasMica, form.Padding.Left > 0);
        Assert.Equal(SystemColors.Control, content.BackColor);
        Assert.Equal(form.ClientRectangle.Width - form.Padding.Horizontal, content.Width);
        Assert.Equal(form.ClientRectangle.Height - form.Padding.Vertical, content.Height);
        using var background = new Bitmap(form.ClientSize.Width, form.ClientSize.Height);
        form.PaintFrameBackground(background);
        Assert.Equal(form.BackColor.ToArgb(), background.GetPixel(background.Width / 2, background.Height / 2).ToArgb());
        if (hasMica)
        {
            Assert.Equal(Color.Black.ToArgb(), background.GetPixel(0, 0).ToArgb());
            Assert.Equal(Color.Black.ToArgb(), background.GetPixel(background.Width - 1, background.Height - 1).ToArgb());
            Assert.True(DwmGetWindowAttribute(form.Handle, 38, out var backdrop, sizeof(int)) >= 0);
            Assert.Equal(2, backdrop);
        }
    }

    private class FrameFixture : MicaForm
    {
        public void RecreateFrameHandle() => RecreateHandle();

        public void PaintFrameBackground(Bitmap bitmap)
        {
            using var graphics = Graphics.FromImage(bitmap);
            using var args = new PaintEventArgs(graphics, ClientRectangle);
            OnPaintBackground(args);
        }

        public void Notify(int message)
        {
            var notification = Message.Create(Handle, message, IntPtr.Zero, IntPtr.Zero);
            WndProc(ref notification);
        }
    }

    private sealed class SimulatedFrameFixture : FrameFixture
    {
        public bool Supported { get; set; } = true;
        public bool HighContrast { get; set; }
        public int Dpi { get; set; } = 96;
        public bool FailNextSet { get; set; }
        public bool FailNextExtend { get; set; }
        public bool FailAllSets { get; set; }
        public bool FailAllExtensions { get; set; }
        public int NativeBackdrop { get; private set; }
        public int NativeMargin { get; private set; }
        public int NativeCalls { get; private set; }

        protected override bool SupportsSystemBackdrop => Supported;
        protected override bool IsHighContrast => HighContrast;
        protected override int FrameDpi => Dpi;

        protected override int SetSystemBackdrop(int backdrop)
        {
            NativeCalls++;
            if (FailNextSet || FailAllSets)
            {
                FailNextSet = false;
                return unchecked((int)0x80004005);
            }
            NativeBackdrop = backdrop;
            return 0;
        }

        protected override int ExtendFrame(int width)
        {
            NativeCalls++;
            if (FailNextExtend || FailAllExtensions)
            {
                FailNextExtend = false;
                return unchecked((int)0x80004005);
            }
            NativeMargin = width;
            return 0;
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            // A replacement HWND starts without the old window's native DWM attributes.
            NativeMargin = 0;
            NativeBackdrop = 0;
        }
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
