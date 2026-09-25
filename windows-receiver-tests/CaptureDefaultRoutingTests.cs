using PocketMicReceiver;
using Xunit;

namespace PocketMicReceiver.Tests;

public sealed class CaptureDefaultRoutingTests
{
    [Theory]
    [InlineData("CABLE Input (VB-Audio Virtual Cable)", "CABLE Output (VB-Audio Virtual Cable)")]
    [InlineData("VoiceMeeter Input (VB-Audio VoiceMeeter VAIO)", "VoiceMeeter Output (VB-Audio VoiceMeeter VAIO)")]
    [InlineData("VoiceMeeter Aux Input (VB-Audio VoiceMeeter AUX VAIO)", "VoiceMeeter Aux Output (VB-Audio VoiceMeeter AUX VAIO)")]
    public void MatcherPairsKnownRenderAndCaptureEndpointNames(string renderName, string captureName)
    {
        Assert.Equal(captureName, CaptureEndpointMatcher.ExpectedCaptureName(renderName));
    }

    [Fact]
    public void MatcherDoesNotGuessFromGenericVirtualCableNames()
    {
        Assert.Null(CaptureEndpointMatcher.ExpectedCaptureName("Some Virtual Cable"));
        Assert.Null(CaptureEndpointMatcher.ExpectedCaptureName("Speakers"));
    }

    [Fact]
    public void MatcherRequiresOneExactActiveCaptureEndpoint()
    {
        var renderName = "CABLE Input (VB-Audio Virtual Cable)";
        var single = CaptureEndpointMatcher.Resolve(renderName, new[]
        {
            new CaptureEndpointInfo("capture-1", "CABLE Output (VB-Audio Virtual Cable)"),
            new CaptureEndpointInfo("unrelated", "VoiceMeeter Output"),
        });
        var ambiguous = CaptureEndpointMatcher.Resolve(renderName, new[]
        {
            new CaptureEndpointInfo("capture-1", "CABLE Output (VB-Audio Virtual Cable)"),
            new CaptureEndpointInfo("capture-2", "CABLE Output (VB-Audio Virtual Cable)"),
        });

        Assert.True(single.IsUnique);
        Assert.Equal("capture-1", single.Endpoint!.Id);
        Assert.False(ambiguous.IsUnique);
        Assert.Null(ambiguous.Endpoint);
    }

    [Fact]
    public void RouteSnapshotsAndRestoresDifferentRolesIndependently()
    {
        var access = new FakeAccess("console-old", "multimedia-old", "communications-old");
        var routing = new CaptureDefaultRouting(access);

        Assert.True(routing.TryRouteTo("cable-capture", out var route, out var error), error);
        Assert.Equal(new[] { "cable-capture", "cable-capture", "cable-capture" }, access.ValuesInRoleOrder);

        Assert.True(routing.TryRestore(route!, onlyIfStillTarget: false, out error), error);
        Assert.Equal(new[] { "console-old", "multimedia-old", "communications-old" }, access.ValuesInRoleOrder);
    }

    [Fact]
    public void ExitRestorePreservesRolesChangedByAnotherApplication()
    {
        var access = new FakeAccess("console-old", "multimedia-old", "communications-old");
        var routing = new CaptureDefaultRouting(access);
        Assert.True(routing.TryRouteTo("cable-capture", out var route, out var error), error);

        access.Values[CaptureDeviceRole.Multimedia] = "user-selected-mic";
        Assert.True(routing.TryRestore(route!, onlyIfStillTarget: true, out error), error);

        Assert.Equal("console-old", access.Values[CaptureDeviceRole.Console]);
        Assert.Equal("user-selected-mic", access.Values[CaptureDeviceRole.Multimedia]);
        Assert.Equal("communications-old", access.Values[CaptureDeviceRole.Communications]);
    }

    [Fact]
    public void ExplicitRestoreWorksAfterTargetEndpointDisappears()
    {
        var access = new FakeAccess("console-old", "multimedia-old", "communications-old");
        var routing = new CaptureDefaultRouting(access);
        Assert.True(routing.TryRouteTo("cable-capture", out var route, out var error), error);

        // Windows may clear a default when the virtual cable is removed. Restoring the
        // saved role IDs must not require enumerating the missing target endpoint.
        foreach (var role in Enum.GetValues<CaptureDeviceRole>()) access.Values[role] = null;

        Assert.True(routing.TryRestore(route!, onlyIfStillTarget: false, out error), error);
        Assert.Equal(new[] { "console-old", "multimedia-old", "communications-old" }, access.ValuesInRoleOrder);
    }

    [Fact]
    public void PartialRouteFailureRollsBackRolesAlreadyChanged()
    {
        var access = new FakeAccess("console-old", "multimedia-old", "communications-old")
        {
            FailRole = CaptureDeviceRole.Communications,
        };
        var routing = new CaptureDefaultRouting(access);

        Assert.False(routing.TryRouteTo("cable-capture", out var route, out var error));

        Assert.Null(route);
        Assert.Contains("Communications", error);
        Assert.Equal(new[] { "console-old", "multimedia-old", "communications-old" }, access.ValuesInRoleOrder);
    }

    [Fact]
    public void CannotRedirectUnlessEveryPriorRoleCanBeRestored()
    {
        var access = new FakeAccess("console-old", null, "communications-old");
        var routing = new CaptureDefaultRouting(access);

        Assert.False(routing.TryRouteTo("cable-capture", out var route, out var error));

        Assert.Null(route);
        Assert.Contains("cannot safely save and restore", error);
        Assert.Empty(access.SetCalls);
    }

    private sealed class FakeAccess(string? console, string? multimedia, string? communications)
        : IDefaultCaptureEndpointAccess
    {
        public Dictionary<CaptureDeviceRole, string?> Values { get; } = new()
        {
            [CaptureDeviceRole.Console] = console,
            [CaptureDeviceRole.Multimedia] = multimedia,
            [CaptureDeviceRole.Communications] = communications,
        };

        public CaptureDeviceRole? FailRole { get; init; }
        public List<(CaptureDeviceRole Role, string DeviceId)> SetCalls { get; } = new();
        public string?[] ValuesInRoleOrder => new[]
        {
            Values[CaptureDeviceRole.Console],
            Values[CaptureDeviceRole.Multimedia],
            Values[CaptureDeviceRole.Communications],
        };

        public string? GetDefaultEndpointId(CaptureDeviceRole role) => Values[role];

        public bool TrySetDefaultEndpointId(string deviceId, CaptureDeviceRole role, out string error)
        {
            SetCalls.Add((role, deviceId));
            if (role == FailRole)
            {
                error = "injected failure";
                return false;
            }

            Values[role] = deviceId;
            error = string.Empty;
            return true;
        }
    }
}
