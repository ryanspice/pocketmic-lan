namespace PocketMicReceiver;

public enum CaptureDeviceRole
{
    Console,
    Multimedia,
    Communications,
}

public sealed record CaptureRoleDefaults(string? Console, string? Multimedia, string? Communications)
{
    public string? For(CaptureDeviceRole role) => role switch
    {
        CaptureDeviceRole.Console => Console,
        CaptureDeviceRole.Multimedia => Multimedia,
        CaptureDeviceRole.Communications => Communications,
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

}

public sealed record CaptureDefaultRoute(CaptureRoleDefaults PreviousDefaults, string TargetDeviceId);

public sealed record CaptureEndpointInfo(string Id, string FriendlyName);

public sealed record CaptureEndpointResolution(string ExpectedName, IReadOnlyList<CaptureEndpointInfo> Matches)
{
    public bool IsUnique => Matches.Count == 1;
    public CaptureEndpointInfo? Endpoint => IsUnique ? Matches[0] : null;
}

/// <summary>Boundary around Windows' per-role default capture device API.</summary>
public interface IDefaultCaptureEndpointAccess
{
    string? GetDefaultEndpointId(CaptureDeviceRole role);
    bool TrySetDefaultEndpointId(string deviceId, CaptureDeviceRole role, out string error);
}

/// <summary>
/// Owns a reversible default-capture routing change. Writes are applied role by role and rolled
/// back on partial failure. Exit restoration only touches roles that still point at our target.
/// </summary>
public sealed class CaptureDefaultRouting
{
    private static readonly CaptureDeviceRole[] Roles =
    {
        CaptureDeviceRole.Console,
        CaptureDeviceRole.Multimedia,
        CaptureDeviceRole.Communications,
    };

    private readonly IDefaultCaptureEndpointAccess _access;

    public CaptureDefaultRouting(IDefaultCaptureEndpointAccess access) =>
        _access = access ?? throw new ArgumentNullException(nameof(access));

    public bool TryRouteTo(string targetDeviceId, out CaptureDefaultRoute? route, out string error)
    {
        route = null;
        if (string.IsNullOrWhiteSpace(targetDeviceId))
        {
            error = "The selected microphone endpoint is unavailable.";
            return false;
        }

        if (!TryReadDefaults(out var previous, out error)) return false;
        if (Roles.Any(role => string.IsNullOrWhiteSpace(previous.For(role))))
        {
            error = "Windows does not have a microphone assigned for every role, so PocketMic cannot safely save and restore them.";
            return false;
        }

        var desired = Roles.ToDictionary(role => role, _ => targetDeviceId);
        if (!TryApply(previous, desired, out error)) return false;

        route = new CaptureDefaultRoute(previous, targetDeviceId);
        return true;
    }

    /// <summary>
    /// Explicit restore honors the user's button press and restores every role in the snapshot.
    /// Exit restore is conservative and restores only roles still assigned to this route.
    /// </summary>
    public bool TryRestore(CaptureDefaultRoute route, bool onlyIfStillTarget, out string error)
    {
        ArgumentNullException.ThrowIfNull(route);
        if (!TryReadDefaults(out var current, out error)) return false;

        var desired = new Dictionary<CaptureDeviceRole, string>();
        foreach (var role in Roles)
        {
            var originalId = route.PreviousDefaults.For(role);
            if (string.IsNullOrWhiteSpace(originalId))
            {
                error = $"The previous {role} microphone was not available to restore.";
                return false;
            }

            if (!onlyIfStillTarget || string.Equals(current.For(role), route.TargetDeviceId, StringComparison.Ordinal))
                desired[role] = originalId;
        }

        return TryApply(current, desired, out error);
    }

    private bool TryReadDefaults(out CaptureRoleDefaults defaults, out string error)
    {
        try
        {
            defaults = new CaptureRoleDefaults(
                _access.GetDefaultEndpointId(CaptureDeviceRole.Console),
                _access.GetDefaultEndpointId(CaptureDeviceRole.Multimedia),
                _access.GetDefaultEndpointId(CaptureDeviceRole.Communications));
        }
        catch (Exception exception)
        {
            defaults = new CaptureRoleDefaults(null, null, null);
            error = $"Could not read the current Windows microphone assignments: {exception.Message}";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool TryApply(
        CaptureRoleDefaults before,
        IReadOnlyDictionary<CaptureDeviceRole, string> desired,
        out string error)
    {
        var changed = new List<CaptureDeviceRole>();
        foreach (var role in Roles)
        {
            if (!desired.TryGetValue(role, out var targetId) ||
                string.Equals(before.For(role), targetId, StringComparison.Ordinal))
                continue;

            if (_access.TrySetDefaultEndpointId(targetId, role, out var setError))
            {
                changed.Add(role);
                continue;
            }

            var rollbackErrors = new List<string>();
            for (var index = changed.Count - 1; index >= 0; index--)
            {
                var changedRole = changed[index];
                var originalId = before.For(changedRole);
                if (string.IsNullOrWhiteSpace(originalId))
                {
                    rollbackErrors.Add($"{changedRole}: previous endpoint was not available to restore");
                    continue;
                }

                if (!_access.TrySetDefaultEndpointId(originalId, changedRole, out var rollbackError))
                    rollbackErrors.Add($"{changedRole}: {rollbackError}");
            }

            error = $"Windows rejected the {role} microphone change: {setError}";
            if (rollbackErrors.Count > 0)
                error += " Rollback was incomplete (" + string.Join("; ", rollbackErrors) + "). Check Windows Sound settings.";
            return false;
        }

        error = string.Empty;
        return true;
    }
}

/// <summary>Strict friendly-name pairing for the cable render/capture endpoints we recognize.</summary>
public static class CaptureEndpointMatcher
{
    private static readonly (string Render, string Capture)[] KnownPairs =
    {
        ("VoiceMeeter Aux Input", "VoiceMeeter Aux Output"),
        ("VoiceMeeter Input", "VoiceMeeter Output"),
        ("CABLE Input", "CABLE Output"),
    };

    public static string? ExpectedCaptureName(string renderFriendlyName)
    {
        foreach (var pair in KnownPairs)
        {
            var index = renderFriendlyName.IndexOf(pair.Render, StringComparison.OrdinalIgnoreCase);
            if (index < 0) continue;

            return renderFriendlyName[..index] + pair.Capture + renderFriendlyName[(index + pair.Render.Length)..];
        }

        return null;
    }

    public static CaptureEndpointResolution Resolve(
        string renderFriendlyName,
        IEnumerable<CaptureEndpointInfo> activeCaptureEndpoints)
    {
        var expected = ExpectedCaptureName(renderFriendlyName);
        if (expected is null) return new CaptureEndpointResolution(string.Empty, Array.Empty<CaptureEndpointInfo>());

        var matches = activeCaptureEndpoints
            .Where(endpoint => string.Equals(endpoint.FriendlyName, expected, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return new CaptureEndpointResolution(expected, matches);
    }
}
