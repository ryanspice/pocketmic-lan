using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PocketMicReceiver;

/// <summary>The portable entry point selects the modern UI while retaining a standalone fallback.</summary>
internal static class ModernReceiverLauncher
{
    public static bool TryLaunch(string[] args)
    {
        if (args.Contains("--classic", StringComparer.OrdinalIgnoreCase) ||
            !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22621)) return false;

        var directory = Path.Combine(AppContext.BaseDirectory, "modern");
        var executable = Path.Combine(directory, "PocketMicReceiver.WinUI.exe");
        if (!File.Exists(executable)) return false;
        try
        {
            var start = new ProcessStartInfo(executable) { WorkingDirectory = directory, UseShellExecute = false };
            foreach (var arg in args) start.ArgumentList.Add(arg);
            using var process = Process.Start(start);
            if (process is null) return false;
            // Transfer the shell launch's foreground entitlement to this exact child; its
            // activation client can then pass it to the already-running receiver.
            AllowSetForegroundWindow((uint)process.Id);
            // A second launch acknowledges activation and exits with zero. A genuine startup
            // failure gets the compatibility UI; a later runtime fault must not start a second engine.
            if (!process.WaitForExit(8000) || process.ExitCode == 0) return true;
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        {
            // The classic application remains independently runnable if modern deployment fails.
        }

        MessageBox.Show("The modern Windows interface could not start. PocketMic will open its compatibility interface. " +
            "Reinstall the complete Windows package to repair the modern interface.", "PocketMic", MessageBoxButtons.OK, MessageBoxIcon.Information);
        return false;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
