namespace PocketMicReceiver;

/// <summary>Ranks usable local IPv4 choices without removing VPN or virtual adapters.</summary>
public sealed record PairingAddress(string InterfaceName, string AdapterType, string Address,
    bool HasDefaultGateway, bool IsPhysical)
{
    public string Label => $"{Address} — {InterfaceName} ({AdapterType})";
}

public static class PairingAddressRanker
{
    public static IReadOnlyList<PairingAddress> Rank(IEnumerable<PairingAddress> addresses) => addresses
        .OrderByDescending(address => address.IsPhysical && address.HasDefaultGateway)
        .ThenByDescending(address => address.HasDefaultGateway)
        .ThenByDescending(address => address.IsPhysical)
        .ThenBy(address => address.InterfaceName, StringComparer.OrdinalIgnoreCase)
        .ThenBy(address => address.Address, StringComparer.Ordinal)
        .ToArray();
}
