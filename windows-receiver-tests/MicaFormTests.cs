using System.Drawing;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Xunit;
using Xunit.Abstractions;

namespace PocketMicReceiver.Tests;

public sealed class MicaFormTests(ITestOutputHelper output)
{
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
        if (hasMica)
        {
            Assert.True(DwmGetWindowAttribute(form.Handle, 38, out var backdrop, sizeof(int)) >= 0);
            Assert.Equal(2, backdrop);
        }
    }

    private sealed class FrameFixture : MicaForm
    {
        public void RecreateFrameHandle() => RecreateHandle();

        public void Notify(int message)
        {
            var notification = Message.Create(Handle, message, IntPtr.Zero, IntPtr.Zero);
            WndProc(ref notification);
        }
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
