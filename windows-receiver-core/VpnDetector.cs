using System.Net.NetworkInformation;

namespace PocketMicReceiver;

/// <summary>
/// Detects active VPN adapters. Many VPN clients (and their kill switches) drop inbound LAN
/// traffic, so the phone cannot reach the receiver even though pairing looks correct.
/// </summary>
public static class VpnDetector
{
    private static readonly string[] Markers =
    {
        "vpn", "wireguard", "wintun", "openvpn", "tap-windows", "tap adapter", "nordlynx",
        "mullvad", "proton", "expressvpn", "surfshark", "cyberghost", "windscribe", "pia ",
        "private internet access", "fortinet", "forticlient", "globalprotect", "pangp",
        "anyconnect", "cisco", "zscaler", "juniper", "pulse secure", "sonicwall", "zerotier",
        "tailscale", "hamachi", "softether", "cloudflare warp", "warp",
    };

    /// <summary>Whether an adapter with this identity looks like a VPN tunnel.</summary>
    public static bool IsVpnAdapter(string name, string description, NetworkInterfaceType type)
    {
        if (type == NetworkInterfaceType.Ppp) return true;
        var text = $"{name} {description}".ToLowerInvariant();
        return Markers.Any(marker => text.Contains(marker, StringComparison.Ordinal));
    }

    public static bool IsVpnAdapter(NetworkInterface adapter) =>
        IsVpnAdapter(adapter.Name, adapter.Description, adapter.NetworkInterfaceType);

    /// <summary>Names of VPN adapters that are currently up; empty when none are active.</summary>
    public static IReadOnlyList<string> ActiveVpnAdapters()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up && IsVpnAdapter(adapter))
                .Select(adapter => adapter.Name).Distinct().ToArray();
        }
        catch (NetworkInformationException) { return Array.Empty<string>(); }
    }
}
