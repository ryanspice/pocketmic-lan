using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PocketMicReceiver;

/// <summary>Ranks usable local IPv4 choices without removing VPN or virtual adapters.</summary>
public sealed record PairingAddress(string InterfaceName, string AdapterType, string Address,
    bool HasDefaultGateway, bool IsPhysical, bool Recommended = false)
{
    public string Label => $"{Address} — {InterfaceName} ({AdapterType}){(Recommended ? " · recommended" : "")}";
}

public static class PairingAddressRanker
{
    public static IReadOnlyList<PairingAddress> Rank(IEnumerable<PairingAddress> addresses) => addresses
        .OrderByDescending(address => address.IsPhysical && address.HasDefaultGateway)
        .ThenByDescending(address => address.HasDefaultGateway)
        .ThenByDescending(address => address.IsPhysical)
        .ThenBy(address => address.InterfaceName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(address => address.Address, StringComparer.Ordinal)
        .Select((address, index) => address with { Recommended = index == 0 && address.IsPhysical && address.HasDefaultGateway })
        .ToArray();

    public static bool LooksPhysical(NetworkInterfaceType type, string name, string description) =>
        type is NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.Wireless80211 &&
        !new[] { "virtual", "vpn", "tunnel", "hyper-v", "vmware", "virtualbox", "wsl", "tap-", "tun-" }
            .Any(hint => (name + " " + description).Contains(hint, StringComparison.OrdinalIgnoreCase));

    /// <summary>Read-only OS inventory shared by both hosts; recommendation remains advisory.</summary>
    public static IReadOnlyList<PairingAddress> LocalChoices()
    {
        try
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up &&
                    adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(adapter =>
                {
                    var properties = adapter.GetIPProperties();
                    var gateway = properties.GatewayAddresses.Any(item => item.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !item.Address.Equals(IPAddress.Any) && !IPAddress.IsLoopback(item.Address));
                    var physical = LooksPhysical(adapter.NetworkInterfaceType, adapter.Name, adapter.Description);
                    return properties.UnicastAddresses
                        .Where(item => item.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !item.Address.Equals(IPAddress.Any) && !IPAddress.IsLoopback(item.Address))
                        .Select(item => new PairingAddress(adapter.Name, adapter.NetworkInterfaceType.ToString(),
                            item.Address.ToString(), gateway, physical));
                });
            return Rank(candidates);
        }
        catch (NetworkInformationException) { return Array.Empty<PairingAddress>(); }
    }
}
