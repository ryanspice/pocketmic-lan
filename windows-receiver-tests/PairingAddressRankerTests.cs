using PocketMicReceiver;
using Xunit;

namespace PocketMicReceiver.Tests;

public sealed class PairingAddressRankerTests
{
    [Fact]
    public void RecommendsPhysicalDefaultGatewayButRetainsOtherChoices()
    {
        var vpn = new PairingAddress("VPN", "Tunnel", "10.8.0.2", true, false);
        var virtualLan = new PairingAddress("Virtual", "Ethernet", "172.20.0.2", false, false);
        var wifi = new PairingAddress("Wi-Fi", "Wireless80211", "192.168.1.20", true, true);
        var ethernet = new PairingAddress("Ethernet", "Ethernet", "192.168.1.30", false, true);

        var ranked = PairingAddressRanker.Rank(new[] { vpn, virtualLan, ethernet, wifi });

        Assert.Equal(wifi, ranked[0]);
        Assert.Equal(4, ranked.Count);
        Assert.Contains(vpn, ranked);
        Assert.Contains(virtualLan, ranked);
    }

    [Fact]
    public void EmptyInventoryStaysEmpty() => Assert.Empty(PairingAddressRanker.Rank(Array.Empty<PairingAddress>()));
}
