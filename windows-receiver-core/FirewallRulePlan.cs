using System.Security.Cryptography;
using System.Text;

namespace PocketMicReceiver;

/// <summary>Builds a user-invoked repair plan; construction never changes machine settings.</summary>
public static class FirewallRulePlan
{
    public static string Build(string executable, int audioPort)
    {
        if (!Path.IsPathFullyQualified(executable) || !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("An absolute receiver executable path is required.", nameof(executable));
        if (audioPort is < 1 or > 65534) throw new ArgumentOutOfRangeException(nameof(audioPort));
        var quotedPath = executable.Replace("'", "''");
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(executable.ToUpperInvariant())))[..16];
        var ports = string.Join(",", new[] { audioPort, audioPort + 1, ControlProtocol.DiscoveryPort }.Distinct());
        return $$"""
            $ErrorActionPreference = 'Stop'
            Add-Type -AssemblyName System.Windows.Forms
            try {
                $program = '{{quotedPath}}'
                $name = 'PocketMicLAN-{{identity}}-UDP'
                $blocked = @(Get-NetFirewallApplicationFilter -Program $program -ErrorAction SilentlyContinue |
                    Get-NetFirewallRule | Where-Object { $_.Enabled -eq 'True' -and $_.Direction -eq 'Inbound' -and $_.Action -eq 'Block' } |
                    Where-Object { ($_ | Get-NetFirewallPortFilter).Protocol -in @('UDP','Any') })
                if ($blocked.Count) {
                    throw 'An explicit Windows firewall block exists for this receiver. An Allow rule cannot override it. Review the blocked application in Windows Defender Firewall; no rules were changed.'
                }
                $existing = Get-NetFirewallRule -Name $name -ErrorAction SilentlyContinue
                if ($existing) {
                    if ($existing.Group -ne 'PocketMic LAN' -or ($existing | Get-NetFirewallApplicationFilter).Program -ne $program) {
                        throw 'The owned rule identifier is in use by another rule. No rules were changed.'
                    }
                    $existing | Set-NetFirewallRule -Direction Inbound -Action Allow -Enabled True -Profile Private -Protocol UDP -LocalPort {{ports}} -RemoteAddress LocalSubnet -Program $program
                } else {
                    New-NetFirewallRule -Name $name -DisplayName 'PocketMic LAN receiver (local UDP)' -Group 'PocketMic LAN' -Direction Inbound -Action Allow -Enabled True -Program $program -Protocol UDP -LocalPort {{ports}} -RemoteAddress LocalSubnet -Profile Private | Out-Null
                }
                [System.Windows.Forms.MessageBox]::Show('PocketMic UDP access is configured for Private networks and local-subnet devices only. If this trusted LAN is classified Public, review its network profile yourself. No network profile was changed. Restart pairing on the phone.', 'PocketMic firewall') | Out-Null
            } catch {
                [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, 'PocketMic firewall repair did not complete') | Out-Null
                exit 1
            }
            """;
    }
}
