using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace PocketMicReceiver;

/// <summary>Explicit support action shared by both desktop hosts; never runs on stream startup.</summary>
public static class FirewallSupport
{
    public static void Show(int audioPort)
    {
        var result = MessageBox.Show(
            "No phone reply does not identify a firewall problem. Check that the receiver is listening, both devices share a trusted LAN, and the PC address, port and pairing key match.\n\n" +
            "Yes: request administrator permission to configure only this receiver's UDP ports for Private/local-subnet access. Existing explicit blocks are reported, not removed.\n" +
            "No: open Windows firewall settings for manual review. Cancel: make no changes.",
            "PocketMic connection help", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
        if (result == DialogResult.Cancel) return;
        try
        {
            if (result == DialogResult.No)
            {
                Process.Start(new ProcessStartInfo("control.exe", "/name Microsoft.WindowsFirewall") { UseShellExecute = true });
                return;
            }
            var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Receiver path unavailable.");
            var script = FirewallRulePlan.Build(executable, audioPort);
            // EncodedCommand avoids a mutable elevated script file and shell argument injection.
            var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
            Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                "WindowsPowerShell", "v1.0", "powershell.exe"), "-NoProfile -STA -EncodedCommand " + encoded)
                { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden });
        }
        catch (Win32Exception error) when (error.NativeErrorCode == 1223)
        { MessageBox.Show("Administrator permission was declined. No repair was applied. Use connection help again and choose No for manual firewall settings.", "PocketMic"); }
        catch (Exception error)
        { MessageBox.Show("Could not start firewall support: " + error.Message, "PocketMic"); }
    }
}
