using System.Text.Json;

namespace PocketMicReceiver;

/// <summary>
/// Append-only JSONL session log under <c>%APPDATA%\PocketMic\sessions</c> — the same base
/// directory <c>ReceiverSettings</c> already uses, so everything PocketMic writes lives under
/// one folder. The file opens with a <see cref="SessionHeader"/> line describing the network
/// and settings context, then one line per closed <see cref="WindowStats"/> window or logged
/// <see cref="SessionEventRecord"/>, in the order they happened.
///
/// JSON Lines rather than a single JSON array on purpose: a crash or forced close mid-session
/// still leaves every line written so far individually valid and readable, where a single
/// top-level array would be truncated into invalid JSON by the exact same failure.
///
/// Every write is flushed immediately. Session data arrives at most a few times a second (10 s
/// windows, occasional events) — this is nowhere near the audio path, so trading a little I/O
/// throughput for "never lose the last few seconds on a crash" is the right side of that trade.
/// </summary>
public sealed class SessionRecorder : IDisposable
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    private readonly object _lock = new();
    private readonly StreamWriter _writer;
    private bool _disposed;

    public string FilePath { get; }

    public static string SessionsDirectory
    {
        get
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "PocketMic",
                "sessions");
            Directory.CreateDirectory(directory);
            return directory;
        }
    }

    public SessionRecorder(SessionHeader header)
    {
        FilePath = CreateSessionFile(header.StartedAt);
        _writer = new StreamWriter(
            new FileStream(FilePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true,
        };
        WriteLine(new HeaderLine("header", header));
    }

    public void AppendWindow(WindowStats window)
    {
        if (_disposed) return;
        WriteLine(new WindowLine("window", window));
    }

    public void AppendEvent(SessionEventRecord evt)
    {
        if (_disposed) return;
        WriteLine(new EventLine("event", evt));
    }

    private void WriteLine<T>(T line)
    {
        var json = JsonSerializer.Serialize(line, Options);
        lock (_lock)
        {
            if (_disposed) return;
            try { _writer.WriteLine(json); }
            catch (IOException)
            {
                // A session log is a diagnostic convenience; a full disk or a removed drive
                // must never take the receiver down with it.
            }
        }
    }

    /// <summary>
    /// Timestamp-named so sessions sort chronologically in the folder without opening any of
    /// them. Collisions (two sessions starting the same second) are vanishingly rare but not
    /// impossible on a fast restart loop, so a numeric suffix is appended rather than silently
    /// overwriting an existing file.
    /// </summary>
    private static string CreateSessionFile(DateTimeOffset startedAt)
    {
        var directory = SessionsDirectory;
        var stamp = startedAt.LocalDateTime.ToString("yyyyMMdd_HHmmss");
        var path = Path.Combine(directory, $"session_{stamp}.jsonl");
        var suffix = 1;
        while (File.Exists(path))
        {
            path = Path.Combine(directory, $"session_{stamp}-{suffix}.jsonl");
            suffix++;
        }

        return path;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;
            try { _writer.Flush(); }
            catch (IOException) { }
            _writer.Dispose();
        }
    }

    private sealed record HeaderLine(string Type, SessionHeader Header);

    private sealed record WindowLine(string Type, WindowStats Window);

    private sealed record EventLine(string Type, SessionEventRecord Event);
}
