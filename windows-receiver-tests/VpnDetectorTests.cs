using System.Net.NetworkInformation;
using PocketMicReceiver;
using Xunit;

namespace PocketMicReceiver.Tests;

public class VpnDetectorTests
{
    [Theory]
    [InlineData("NordLynx", "NordLynx Tunnel", NetworkInterfaceType.Unknown)]
    [InlineData("Mullvad", "WireGuard Tunnel", NetworkInterfaceType.Unknown)]
    [InlineData("Ethernet 3", "TAP-Windows Adapter V9", NetworkInterfaceType.Ethernet)]
    [InlineData("ProtonVPN", "Wintun Userspace Tunnel", NetworkInterfaceType.Unknown)]
    [InlineData("Dial-up", "WAN Miniport", NetworkInterfaceType.Ppp)]
    public void FlagsVpnAdapters(string name, string description, NetworkInterfaceType type) =>
        Assert.True(VpnDetector.IsVpnAdapter(name, description, type));

    [Theory]
    [InlineData("Ethernet", "Realtek Gaming GbE Family Controller", NetworkInterfaceType.Ethernet)]
    [InlineData("Wi-Fi", "Intel(R) Wi-Fi 6E AX211 160MHz", NetworkInterfaceType.Wireless80211)]
    [InlineData("vEthernet (WSL (Hyper-V firewall))", "Hyper-V Virtual Ethernet Adapter #2", NetworkInterfaceType.Ethernet)]
    public void IgnoresLanAdapters(string name, string description, NetworkInterfaceType type) =>
        Assert.False(VpnDetector.IsVpnAdapter(name, description, type));
}
