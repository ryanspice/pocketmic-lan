using System.Globalization;
using System.Text;
using System.Text.Json;

namespace PocketMicReceiver;

/// <summary>
/// Turns a session JSONL file into a readable Markdown report: header, summary, percentile
/// breakdown over time, and notable events. Reads from disk rather than from a live
/// <see cref="AnalyticsCollector"/> so a report can be produced for any past session, not just
/// the one currently running, and so this type carries no dependency on the collector at all.
/// </summary>
public static class SessionReportWriter
{
    public static void WriteMarkdown(string sessionJsonlPath, string outputMarkdownPath) =>
        File.WriteAllText(outputMarkdownPath, BuildMarkdown(sessionJsonlPath));

    public static string BuildMarkdown(string sessionJsonlPath)
    {
        var (header, tenSecond, oneMinute, session, events) = ReadSession(sessionJsonlPath);
        return Render(Path.GetFileName(sessionJsonlPath), header, tenSecond, oneMinute, session, events);
    }

    private static (
        SessionHeader? Header,
        List<WindowStats> TenSecond,
        List<WindowStats> OneMinute,
        WindowStats? Session,
        List<SessionEventRecord> Events) ReadSession(string path)
    {
        SessionHeader? header = null;
        WindowStats? session = null;
        var tenSecond = new List<WindowStats>();
        var oneMinute = new List<WindowStats>();
        var events = new List<SessionEventRecord>();

        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            using var document = JsonDocument.Parse(line);
            var root = document.RootElement;
            if (!root.TryGetProperty("Type", out var typeElement)) continue;

            switch (typeElement.GetString())
            {
                case "header" when root.TryGetProperty("Header", out var headerElement):
                    header = headerElement.Deserialize<SessionHeader>();
                    break;

                case "window" when root.TryGetProperty("Window", out var windowElement):
                    var window = windowElement.Deserialize<WindowStats>();
                    if (window is null) break;
                    switch (window.WindowKind)
                    {
                        case "10s": tenSecond.Add(window); break;
                        case "1m": oneMinute.Add(window); break;
                        case "session": session = window; break;
                    }

                    break;

                case "event" when root.TryGetProperty("Event", out var eventElement):
                    var evt = eventElement.Deserialize<SessionEventRecord>();
                    if (evt is not null) events.Add(evt);
                    break;
            }
        }

        return (header, tenSecond, oneMinute, session, events);
    }

    private static string Render(
        string fileName,
        SessionHeader? header,
        List<WindowStats> tenSecond,
        List<WindowStats> oneMinute,
        WindowStats? session,
        List<SessionEventRecord> events)
    {
        var text = new StringBuilder();
        text.AppendLine($"# PocketMic session report — {fileName}");
        text.AppendLine();

        AppendHeaderSection(text, header);
        AppendSummarySection(text, session, oneMinute, tenSecond);
        AppendBreakdownSection(text, oneMinute.Count > 0 ? oneMinute : tenSecond, oneMinute.Count > 0 ? "1 min" : "10 s");
        AppendEventsSection(text, events);

        return text.ToString();
    }

    private static void AppendHeaderSection(StringBuilder text, SessionHeader? header)
    {
        text.AppendLine("## Session");
        text.AppendLine();
        if (header is null)
        {
            text.AppendLine("No session header was found in this file.");
            text.AppendLine();
            return;
        }

        text.AppendLine("| Field | Value |");
        text.AppendLine("|---|---|");
        text.AppendLine($"| Started | {header.StartedAt.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz} |");
        text.AppendLine($"| Phone address | {header.PhoneAddress} |");
        text.AppendLine($"| PC hostname | {header.PcHostName} |");
        text.AppendLine($"| Network subnet | {header.NetworkSubnet} |");
        text.AppendLine($"| Output device | {header.OutputDevice} |");
        text.AppendLine($"| Monitor device | {header.MonitorDevice} |");
        text.AppendLine($"| Prebuffer | {header.PrebufferMilliseconds} ms |");
        text.AppendLine($"| High-water trim | {header.HighWaterMilliseconds} ms |");
        text.AppendLine($"| Voice enhancement | {(header.VoiceEnhanceEnabled ? $"on, strength {header.VoiceStrength}%" : "off")} |");
        text.AppendLine($"| Protocol version | {header.ProtocolVersion} |");

        // Blank for sessions recorded before the receiver had more than one front end. Printing
        // "Unknown" for those would suggest the information was lost rather than never captured.
        if (header.FrontEnd != ReceiverFrontEnd.Unknown || !string.IsNullOrEmpty(header.BuildVersion))
        {
            text.AppendLine($"| Front end | {header.FrontEnd} {header.BuildVersion} |");
        }

        text.AppendLine();
    }

    private static void AppendSummarySection(
        StringBuilder text, WindowStats? session, List<WindowStats> oneMinute, List<WindowStats> tenSecond)
    {
        text.AppendLine("## Summary");
        text.AppendLine();

        // The session window is a live cumulative snapshot emitted once a minute, so a session
        // shorter than a minute never produces one. Fall back to the coarsest window that does
        // exist rather than showing nothing, and say plainly which one was used.
        var summary = session ?? oneMinute.LastOrDefault() ?? tenSecond.LastOrDefault();
        if (summary is null)
        {
            text.AppendLine("No window data was recorded for this session.");
            text.AppendLine();
            return;
        }

        if (session is null)
        {
            text.AppendLine(
                $"*No whole-session snapshot yet (sessions under 1 minute do not produce one); " +
                $"showing the most recent \"{summary.WindowKind}\" window instead.*");
            text.AppendLine();
        }

        text.AppendLine("| Metric | Value |");
        text.AppendLine("|---|---|");
        text.AppendLine($"| Packets received | {summary.PacketsReceived:N0} |");
        text.AppendLine($"| Packets lost | {summary.PacketsLost:N0} ({Fixed(summary.LossPercent)}%) |");
        text.AppendLine($"| Duplicates | {summary.Duplicates:N0} |");
        text.AppendLine($"| Reordered | {summary.Reordered:N0} |");
        text.AppendLine($"| Rejected | {summary.Rejected:N0} |");
        text.AppendLine($"| Trimmed (latency control) | {summary.Trimmed:N0} |");
        text.AppendLine($"| Interarrival p50 / p95 / p99 / max | {Fixed(summary.InterarrivalP50Ms)} / {Fixed(summary.InterarrivalP95Ms)} / {Fixed(summary.InterarrivalP99Ms)} / {Fixed(summary.InterarrivalMaxMs)} ms |");
        text.AppendLine($"| Interarrival stdev | {Fixed(summary.InterarrivalStdevMs)} ms |");
        text.AppendLine($"| Buffer depth avg / max | {Fixed(summary.BufferDepthAvgMs)} / {Fixed(summary.BufferDepthMaxMs)} ms |");
        // Digital silence has no level, so a muted or unplugged phone produces no level samples
        // at all. Printing the placeholder average as though it were a measurement is exactly
        // the kind of confident wrong number this whole layer exists to avoid.
        text.AppendLine(summary.LevelSamples > 0
            ? $"| Input level avg (min–max) | {Fixed(summary.LevelDbfsAvg)} dBFS ({Fixed(summary.LevelDbfsMin)} to {Fixed(summary.LevelDbfsMax)}) |"
            : "| Input level avg (min–max) | not measured — every delivered packet was digital silence |");
        text.AppendLine($"| Noise gate active | {Fixed(summary.NoiseGateActivePercent)}% of the time |");
        if (summary.DroppedSamples > 0)
        {
            text.AppendLine($"| Analytics samples dropped | {summary.DroppedSamples:N0} — the analytics thread fell behind |");
        }

        text.AppendLine();
    }

    private static void AppendBreakdownSection(StringBuilder text, List<WindowStats> windows, string cadence)
    {
        text.AppendLine($"## Percentile breakdown ({cadence} windows)");
        text.AppendLine();

        if (windows.Count == 0)
        {
            text.AppendLine("No windows were recorded.");
            text.AppendLine();
            return;
        }

        text.AppendLine("| Window start | Packets | Loss % | p50 ms | p95 ms | p99 ms | max ms | Buffer avg ms | Level avg dBFS | Gate active % |");
        text.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var window in windows)
        {
            text.AppendLine(
                $"| {window.WindowStart.ToLocalTime():HH:mm:ss} | {window.PacketsReceived:N0} | {Fixed(window.LossPercent)} | " +
                $"{Fixed(window.InterarrivalP50Ms)} | {Fixed(window.InterarrivalP95Ms)} | {Fixed(window.InterarrivalP99Ms)} | {Fixed(window.InterarrivalMaxMs)} | " +
                $"{Fixed(window.BufferDepthAvgMs)} | {(window.LevelSamples > 0 ? Fixed(window.LevelDbfsAvg) : "—")} | {Fixed(window.NoiseGateActivePercent)} |");
        }

        text.AppendLine();
    }

    private static void AppendEventsSection(StringBuilder text, List<SessionEventRecord> events)
    {
        text.AppendLine("## Notable events");
        text.AppendLine();

        if (events.Count == 0)
        {
            text.AppendLine("No discovery events or faults were recorded.");
            text.AppendLine();
            return;
        }

        text.AppendLine("| Time | Kind | Detail |");
        text.AppendLine("|---|---|---|");
        foreach (var evt in events)
        {
            text.AppendLine($"| {evt.At.ToLocalTime():HH:mm:ss} | {evt.Kind} | {evt.Detail} |");
        }

        text.AppendLine();
    }

    private static string Fixed(double value) => value.ToString("F2", CultureInfo.InvariantCulture);
}
