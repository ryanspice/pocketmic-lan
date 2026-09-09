using System.Text.Json;

namespace PocketMicReceiver;

/// <summary>
/// Small settings file kept beside the executable. Auto-listen exists so the receiver answers
/// discovery probes the moment it opens — without it the phone cannot find the PC until
/// somebody clicks Start here, which defeats the point of auto-connect.
/// </summary>
public sealed class ReceiverSettings
{
    public int Port { get; set; } = 49_500;

    public string PairingKey { get; set; } = string.Empty;

    public string OutputDevice { get; set; } = string.Empty;

    public bool AutoListen { get; set; } = true;

    public bool MinimizeToTray { get; set; } = true;

    /// <summary>
    /// Opens without taking focus — to the tray when <see cref="MinimizeToTray"/> is on, to the
    /// taskbar otherwise. Off by default: a receiver that hides itself on first run looks like a
    /// receiver that failed to start.
    ///
    /// Lives here rather than in one front end because both read the same file, and a setting
    /// only one of them knows about would be silently dropped the next time the other saved.
    /// </summary>
    public bool StartMinimized { get; set; }

    /// <summary>
    /// Close an already-running receiver without asking first.
    ///
    /// Off by default, and deliberately so: the other instance may be mid-call, and the person
    /// on the other end of that call did not agree to this. The setting exists for the launcher
    /// path, where the user has just explicitly chosen which front end they want and a
    /// confirmation dialog only asks them to repeat themselves.
    ///
    /// Lives here rather than in one front end because both read the same file, and a setting
    /// only one of them knows about would be silently dropped the next time the other saved.
    /// </summary>
    public bool SilentTakeover { get; set; }

    public int PrebufferMilliseconds { get; set; } = 100;

    public bool VoiceEnhance { get; set; } = true;

    public int VoiceStrength { get; set; } = 60;

    public bool MonitorEnabled { get; set; } = true;

    public string MonitorDevice { get; set; } = string.Empty;

    public bool AdvancedMode { get; set; }

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    /// <summary>
    /// Stored under %APPDATA%, deliberately not beside the executable. Publishing wipes the
    /// output directory, an installed copy may live in a read-only Program Files, and settings
    /// should survive an update — all three break if this sits next to the binary.
    /// </summary>
    private static string SettingsPath
    {
        get
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PocketMic");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "receiver.json");
        }
    }

    public static ReceiverSettings Load()
    {
        try
        {
            var path = SettingsPath;
            if (!File.Exists(path)) return new ReceiverSettings();
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ReceiverSettings>(json) ?? new ReceiverSettings();
        }
        catch
        {
            // A corrupt or unreadable settings file must never stop the app from starting.
            return new ReceiverSettings();
        }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, Options));
        }
        catch
        {
            // Settings are a convenience; failing to persist them is not worth interrupting for.
        }
    }
}
