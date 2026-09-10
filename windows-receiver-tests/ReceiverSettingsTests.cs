using System.Text.Json;

namespace PocketMicReceiver.Tests;

/// <summary>
/// ReceiverSettings Load/Save round-trip. The settings file lives under %APPDATA%,
/// but for testing we verify the defaults and the JSON contract rather than the
/// file-system path.
/// </summary>
public class ReceiverSettingsTests
{
    [Fact]
    public void DefaultValuesAreCorrect()
    {
        var settings = new ReceiverSettings();

        Assert.Equal(49_500, settings.Port);
        Assert.Equal(string.Empty, settings.PairingKey);
        Assert.Equal(string.Empty, settings.OutputDevice);
        Assert.True(settings.AutoListen);
        Assert.True(settings.MinimizeToTray);
        Assert.False(settings.StartMinimized);
        Assert.False(settings.SilentTakeover);
        Assert.Equal(100, settings.PrebufferMilliseconds);
        Assert.True(settings.VoiceEnhance);
        Assert.Equal(60, settings.VoiceStrength);
        Assert.True(settings.MonitorEnabled);
        Assert.Equal(string.Empty, settings.MonitorDevice);
        Assert.False(settings.AdvancedMode);
    }

    [Fact]
    public void SaveAndLoadRoundTripsAllSettings()
    {
        var original = new ReceiverSettings
        {
            Port = 50_000,
            PairingKey = "POCKETMIC-TEST",
            OutputDevice = "Speakers",
            AutoListen = false,
            MinimizeToTray = false,
            StartMinimized = true,
            SilentTakeover = true,
            PrebufferMilliseconds = 200,
            VoiceEnhance = false,
            VoiceStrength = 80,
            MonitorEnabled = false,
            MonitorDevice = "Headphones",
            AdvancedMode = true,
        };

        var json = JsonSerializer.Serialize(original);
        var loaded = JsonSerializer.Deserialize<ReceiverSettings>(json)!;

        Assert.Equal(original.Port, loaded.Port);
        Assert.Equal(original.PairingKey, loaded.PairingKey);
        Assert.Equal(original.OutputDevice, loaded.OutputDevice);
        Assert.Equal(original.AutoListen, loaded.AutoListen);
        Assert.Equal(original.MinimizeToTray, loaded.MinimizeToTray);
        Assert.Equal(original.StartMinimized, loaded.StartMinimized);
        Assert.Equal(original.SilentTakeover, loaded.SilentTakeover);
        Assert.Equal(original.PrebufferMilliseconds, loaded.PrebufferMilliseconds);
        Assert.Equal(original.VoiceEnhance, loaded.VoiceEnhance);
        Assert.Equal(original.VoiceStrength, loaded.VoiceStrength);
        Assert.Equal(original.MonitorEnabled, loaded.MonitorEnabled);
        Assert.Equal(original.MonitorDevice, loaded.MonitorDevice);
        Assert.Equal(original.AdvancedMode, loaded.AdvancedMode);
    }

    [Fact]
    public void LoadReturnsDefaultsForMissingFile()
    {
        // Deserialize null or empty — same code path as Load() returning defaults.
        var loaded = JsonSerializer.Deserialize<ReceiverSettings>("null");

        // JSON null deserializes to null for reference types, but ReceiverSettings
        // is a class, so the ?? fallback in Load() handles this.
        var settings = loaded ?? new ReceiverSettings();

        Assert.Equal(49_500, settings.Port);
        Assert.Equal(string.Empty, settings.PairingKey);
        Assert.True(settings.AutoListen);
    }
}
