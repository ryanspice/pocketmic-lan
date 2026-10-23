using System.Text.Json;
using Xunit;

namespace PocketMicReceiver.Tests;

public class MacFeedbackInteropTests
{
    [Fact]
    public void MacReceptionFeedbackMatchesExistingWindowsControlProtocol()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "tests", "fixtures", "vectors.json")))
            directory = directory.Parent;
        Assert.NotNull(directory);

        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(directory.FullName, "tests", "fixtures", "vectors.json")));
        var root = document.RootElement;
        var control = root.GetProperty("control");
        var stats = control.GetProperty("stats");
        var key = ControlProtocol.DeriveControlKey(root.GetProperty("pairing_key").GetString()!);
        Assert.Equal(Convert.FromHexString(control.GetProperty("key_hex").GetString()!), key);

        var payload = ControlProtocol.StatsPayload(
            stats.GetProperty("packets").GetInt64(),
            stats.GetProperty("lost").GetInt64(),
            stats.GetProperty("late").GetInt64(),
            stats.GetProperty("rejected").GetInt64(),
            stats.GetProperty("trimmed").GetInt64(),
            stats.GetProperty("buffer_millis").GetInt32(),
            stats.GetProperty("playing").GetBoolean());
        var fixture = Convert.FromHexString(stats.GetProperty("frame_hex").GetString()!);

        Assert.Equal(fixture, ControlProtocol.Build(key, ControlProtocol.TypeStats, payload));
        Assert.True(ControlProtocol.Verify(key, fixture));
        Assert.True(ControlProtocol.TryParse(fixture, out var type, out var parsed));
        Assert.Equal(ControlProtocol.TypeStats, type);
        Assert.Equal(payload, parsed);
        // The Mac confirms authenticated reception, not driver playback or audible routing.
        Assert.Equal(0, stats.GetProperty("buffer_millis").GetInt32());
        Assert.False(stats.GetProperty("playing").GetBoolean());
    }
}
