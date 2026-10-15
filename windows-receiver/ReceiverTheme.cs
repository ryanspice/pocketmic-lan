using System.Drawing;
using System.Windows.Forms;
using Microsoft.Win32;

namespace PocketMicReceiver;

internal enum ThemeRole { Default, Muted, Accent, Success, Warning, Error, Surface, Primary }

/// <summary>Opaque content colours coordinated with the native Mica frame.</summary>
internal sealed class ReceiverTheme(bool dark, bool highContrast)
{
    public bool HighContrast { get; } = highContrast;
    public Color Canvas => HighContrast ? SystemColors.Control : dark ? Color.FromArgb(32, 32, 32) : Color.FromArgb(249, 249, 249);
    public Color Surface => HighContrast ? SystemColors.Control : dark ? Color.FromArgb(43, 43, 43) : Color.White;
    public Color Input => HighContrast ? SystemColors.Window : dark ? Color.FromArgb(51, 51, 51) : Color.White;
    public Color Text => HighContrast ? SystemColors.ControlText : dark ? Color.FromArgb(245, 245, 245) : Color.FromArgb(32, 32, 32);
    public Color Muted => HighContrast ? SystemColors.ControlText : dark ? Color.FromArgb(190, 190, 190) : Color.FromArgb(96, 96, 96);
    public Color Accent => HighContrast ? SystemColors.HotTrack : dark ? Color.FromArgb(224, 185, 104) : Color.FromArgb(125, 86, 22);
    public Color Border => HighContrast ? SystemColors.WindowText : dark ? Color.FromArgb(87, 87, 87) : Color.FromArgb(171, 171, 171);
    public Color Hover => HighContrast ? SystemColors.Highlight : dark ? Color.FromArgb(61, 61, 61) : Color.FromArgb(235, 235, 235);
    public Color Pressed => HighContrast ? SystemColors.Highlight : dark ? Color.FromArgb(74, 74, 74) : Color.FromArgb(220, 220, 220);
    public Color Success => HighContrast ? SystemColors.ControlText : dark ? Color.FromArgb(128, 210, 162) : Color.FromArgb(25, 112, 63);
    public Color Warning => HighContrast ? SystemColors.ControlText : dark ? Color.FromArgb(244, 195, 112) : Color.FromArgb(137, 87, 0);
    public Color Error => HighContrast ? SystemColors.ControlText : dark ? Color.FromArgb(255, 151, 151) : Color.FromArgb(181, 42, 42);
    public Color ChartBlue => HighContrast ? SystemColors.HotTrack : dark ? Color.FromArgb(137, 188, 255) : Color.FromArgb(40, 101, 184);

    public static ReceiverTheme FromSystem()
    {
        var dark = false;
        try
        {
            dark = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1) is int value && value == 0;
        }
        catch (Exception error) when (error is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            // A restricted profile can still use the ordinary light/system palette.
        }
        return new ReceiverTheme(dark, SystemInformation.HighContrast);
    }

    public Color Foreground(ThemeRole role) => role switch
    {
        ThemeRole.Muted => Muted,
        ThemeRole.Accent => Accent,
        ThemeRole.Success => Success,
        ThemeRole.Warning => Warning,
        ThemeRole.Error => Error,
        _ => Text,
    };

    public void Apply(Control control)
    {
        var role = control.Tag is ThemeRole tagged ? tagged : ThemeRole.Default;
        control.ForeColor = Foreground(role);
        control.BackColor = role == ThemeRole.Surface ? Surface : control.Parent?.BackColor ?? Canvas;
        switch (control)
        {
            case Button button:
                button.FlatStyle = HighContrast ? FlatStyle.System : FlatStyle.Flat;
                button.UseVisualStyleBackColor = HighContrast;
                button.BackColor = role == ThemeRole.Primary && !HighContrast ? Accent : Surface;
                button.ForeColor = role == ThemeRole.Primary && !HighContrast
                    ? (dark ? Color.FromArgb(32, 32, 32) : Color.White) : Text;
                button.FlatAppearance.BorderColor = Border;
                button.FlatAppearance.BorderSize = 1;
                // Keep primary text contrast during hover/press as well as at rest.
                button.FlatAppearance.MouseOverBackColor = role == ThemeRole.Primary ? Accent : Hover;
                button.FlatAppearance.MouseDownBackColor = role == ThemeRole.Primary ? Accent : Pressed;
                button.MinimumSize = new Size(button.MinimumSize.Width, 32);
                break;
            case TextBox textBox:
                textBox.BackColor = Input;
                textBox.ForeColor = HighContrast ? SystemColors.WindowText : Text;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                break;
            case ComboBox combo:
                combo.BackColor = Input;
                combo.ForeColor = HighContrast ? SystemColors.WindowText : Text;
                combo.FlatStyle = HighContrast ? FlatStyle.Standard : FlatStyle.Flat;
                break;
            case CheckBox checkBox:
                checkBox.UseVisualStyleBackColor = HighContrast;
                break;
            case AnalyticsPanel analytics:
                analytics.ApplyTheme(this);
                break;
        }
        foreach (Control child in control.Controls) Apply(child);
        NativeScrollTheme.Apply(control, dark, HighContrast);
        control.Invalidate();
    }
}
