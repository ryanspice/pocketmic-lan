using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using NAudio;
using NAudio.Wave;

namespace PocketMicReceiver;

/// <summary>
/// What the receiver is currently doing. Front ends map this to their own wording and colour;
/// the engine carries the enum so a host never has to parse the message string to decide how to
/// present it.
/// </summary>
public enum EngineStatus
{
    Stopped,
    Listening,

    /// <summary>Audio is running but discovery and delivery confirmation are unavailable.</summary>
    ControlChannelUnavailable,

    DatagramRejected,
    Buffering,
    Receiving,

    /// <summary>
    /// Audio arrived recently but has stopped. The phone is most likely paused, muted by the
    /// user, or riding out a brief Wi-Fi stall — the link is not yet presumed dead.
    /// </summary>
    PhonePaused,

    /// <summary>
    /// Nothing has arrived for long enough that the phone should be presumed gone. Distinct from
    /// <see cref="PhonePaused"/> because they call for different actions: one is waited out, the
    /// other means going and looking at the phone.
    /// </summary>
    PhoneLost,
}

/// <summary>Status value plus the ready-to-display sentence, so both hosts show the same words.</summary>
public readonly record struct EngineStatusUpdate(EngineStatus Status, string Message);

/// <summary>
/// Counter snapshot. A struct rather than an object because it is published several times a
/// second from the receive loop, and the receive loop is not allowed to allocate.
/// </summary>
public readonly record struct EngineStats(
    long Packets,
    long Lost,
    long Late,
    long Rejected,
    long Trimmed,
    double BufferedMilliseconds,
    bool Playing);

/// <summary>
/// Everything the engine needs to open a run. Device numbers are NAudio WaveOut indices, where
/// -1 means the Windows default output.
/// </summary>
public sealed record EngineOptions(
    int Port,
    string PairingKey,
    int OutputDeviceNumber,
    int MonitorDeviceNumber,
    bool MonitorEnabled,

    // FrontEnd/BuildVersion identify the host to the phone (in the discovery announce) and to
    // the session log. Defaulted so neither host is forced to supply them, but both do: with
    // three parts shipping independently, a connection fault whose log cannot say which
    // receiver was running is a fault that has to be reproduced before it can be read.
    ReceiverFrontEnd FrontEnd = ReceiverFrontEnd.Unknown,
    string BuildVersion = "");

/// <summary>
/// One receiver run: the audio and control sockets, the output devices, the jitter buffer and
/// the counters. Lifted out of the WinForms form so the WinUI 3 front end can host exactly the
/// same engine rather than a second copy of the same logic.
///
/// Nothing here may reference a UI framework. State is published as plain events raised on
/// whichever worker thread produced it, and each host is responsible for marshalling them onto
/// its own dispatcher.
///
/// Async loops are guarded by a run generation (<c>runId</c>) rather than only by the
/// cancellation token: a socket read that was already in flight when a run was torn down can
/// still complete afterwards, and its result must not be attributed to the run that replaced it.
/// </summary>
public sealed class PocketMicEngine
{
    private readonly VoiceProcessor _voice = new();

    private CancellationTokenSource? _cts;
    private UdpClient? _udp;
    private UdpClient? _control;
    private byte[]? _controlKey;
    private AesGcm? _aes;
    private WaveOutEvent? _waveOut;
    private BufferedWaveProvider? _audioBuffer;
    private WaveOutEvent? _monitorOut;
    private EngineOptions? _currentOptions;
    private bool _monitorEnabled;
    private BufferedWaveProvider? _monitorBuffer;
    private Task? _receiveTask;
    private Task? _controlReceiveTask;
    private Task? _controlStatsTask;
    private Task? _stoppingTask;
    private AnalyticsCollector? _analytics;
    private SessionRecorder? _sessionRecorder;

    private long _packetCount;
    private long _lostPackets;
    private long _latePackets;
    private long _rejectedPackets;
    private long _trimmedPackets;
    private ulong? _sessionId;
    private uint? _lastSequence;
    private long _lastUpdateTick;
    private bool _playbackStarted;
    private long _runId;
    private float _recentRms;
    private volatile IPAddress? _phoneAddress;

    /// <summary>
    /// When the last authenticated packet landed, as a monotonic tick. Written by the receive
    /// loop and read by the control tick, which is what lets the receiver notice that audio has
    /// stopped: the receive loop cannot report an absence, because an absence is precisely the
    /// thing that stops it running.
    /// </summary>
    private long _lastPacketTick;

    private int _prebufferMilliseconds = AudioPipeline.DefaultPrebufferMilliseconds;
    private int _highWaterMilliseconds = AudioPipeline.DefaultHighWaterMilliseconds;

    // Last values published, so a status or address that has not actually changed is not
    // re-broadcast on every update tick.
    private EngineStatus _lastStatus = EngineStatus.Stopped;
    private string _lastStatusMessage = "Stopped";
    private IPEndPoint? _lastReportedEndpoint;

    /// <summary>Raised when the status value or its message changes.</summary>
    public event EventHandler<EngineStatusUpdate>? StatusChanged;

    /// <summary>Raised at the update throttle while audio is arriving.</summary>
    public event EventHandler<EngineStats>? StatsUpdated;

    /// <summary>
    /// The receiver hit something it cannot continue through. The engine does not stop itself:
    /// the host decides how to report the failure and then calls <see cref="StopAsync"/>, so the
    /// host's own controls never fall out of step with the engine.
    /// </summary>
    public event EventHandler<string>? Fault;

    /// <summary>
    /// The endpoint of the last datagram the receiver reacted to, authenticated or not — a
    /// rejected sender is exactly what the operator needs to see when the pairing key is wrong.
    /// </summary>
    public event EventHandler<IPEndPoint>? PhoneAddressChanged;

    /// <summary>
    /// The phone pushed voice-processing settings. Already applied to the processor by the time
    /// this is raised; hosts subscribe only to reflect it in their controls.
    /// </summary>
    public event EventHandler<DspConfig>? DspConfigReceived;

    /// <summary>
    /// The local monitor output opened successfully. Separate from <see cref="Start"/> returning
    /// because the monitor is allowed to fail without failing the run, and a host that persists
    /// the chosen monitor device should only do so once it is known to work.
    /// </summary>
    public event EventHandler? MonitorStarted;

    /// <summary>
    /// A diagnostics window closed: every 10 s, and every minute for the 1 min and cumulative
    /// session windows. Raised on the analytics background thread, like every other engine
    /// event, so each host marshals it onto its own dispatcher.
    ///
    /// Surfaced as an engine event rather than leaving hosts to subscribe to
    /// <see cref="Analytics"/> directly, because the collector is created per run: a host that
    /// attached to the collector would have to re-attach on every start, and would miss
    /// everything raised before it noticed.
    /// </summary>
    public event EventHandler<WindowStats>? AnalyticsWindowClosed;

    public bool IsRunning => _cts is not null;

    /// <summary>
    /// The live diagnostics collector, or null while stopped. Exposed for hosts that want the
    /// raw hot-path API or the dropped-sample count; the ordinary way to consume analytics is
    /// <see cref="AnalyticsWindowClosed"/>.
    /// </summary>
    public AnalyticsCollector? Analytics => _analytics;

    /// <summary>
    /// The JSONL session log this run is writing, or null if none could be opened. Like
    /// <see cref="Stats"/>, it survives a stop so a host can still offer "export the session
    /// that just finished" — which is exactly when a report is worth having.
    /// </summary>
    public string? AnalyticsSessionPath { get; private set; }

    /// <summary>Where the last authenticated audio came from, and where statistics are sent.</summary>
    public IPAddress? PhoneAddress => _phoneAddress;

    /// <summary>
    /// The latency/robustness trade, live-tunable while running: more of it absorbs jitter spikes
    /// that would otherwise become dropouts, at the cost of delay.
    /// </summary>
    public int PrebufferMilliseconds
    {
        get => _prebufferMilliseconds;
        set
        {
            _prebufferMilliseconds = value;
            _highWaterMilliseconds = AudioPipeline.HighWaterFor(value);
        }
    }

    /// <summary>Backlog above which buffered audio may be trimmed. Derived from the prebuffer.</summary>
    public int HighWaterMilliseconds => _highWaterMilliseconds;

    public bool VoiceEnabled
    {
        get => _voice.Enabled;
        set => _voice.Enabled = value;
    }

    /// <summary>0 = off, 1 = maximum.</summary>
    public float VoiceStrength
    {
        get => _voice.Strength;
        set => _voice.Strength = value;
    }

    /// <summary>
    /// Counters as they stand right now. Deliberately survives a stop so a host can show the
    /// totals of the run that just ended; they are cleared by the next <see cref="Start"/>.
    /// </summary>
    public EngineStats Stats => new(
        _packetCount,
        _lostPackets,
        _latePackets,
        _rejectedPackets,
        _trimmedPackets,
        _audioBuffer?.BufferedDuration.TotalMilliseconds ?? 0,
        _playbackStarted);

    /// <summary>
    /// Opens the sockets and audio devices and starts the receive loops. Throws if the audio
    /// device or the port cannot be opened; partially built state is left in place for
    /// <see cref="StopAsync"/> to dismantle, so a caller that catches must still stop.
    /// </summary>
    public void Start(EngineOptions options)
    {
        if (_cts is not null) return;
        if (options.Port is < ControlProtocol.MinAudioPort or > ControlProtocol.MaxAudioPort)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                $"Audio port must be between {ControlProtocol.MinAudioPort} and {ControlProtocol.MaxAudioPort}.");
        }

        _audioBuffer = new BufferedWaveProvider(new WaveFormat(AudioPipeline.SampleRate, 16, 1))
        {
            BufferDuration = TimeSpan.FromMilliseconds(300),
            DiscardOnBufferOverflow = true,
            ReadFully = true,
        };
        _waveOut = new WaveOutEvent
        {
            DeviceNumber = options.OutputDeviceNumber,
            DesiredLatency = 60,
            NumberOfBuffers = 3,
        };
        _waveOut.PlaybackStopped += HandlePlaybackStopped;
        _waveOut.Init(_audioBuffer);

        _currentOptions = options;
        _monitorEnabled = options.MonitorEnabled;
        StartMonitorOutput(options);

        var pairingBytes = Encoding.UTF8.GetBytes(options.PairingKey);
        byte[] keyBytes;
        try
        {
            keyBytes = SHA256.HashData(pairingBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pairingBytes);
        }

        try
        {
            _aes = new AesGcm(keyBytes, AudioPipeline.TagSize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyBytes);
        }

        _udp = new UdpClient(AddressFamily.InterNetwork);
        _udp.Client.ExclusiveAddressUse = true;
        _udp.Client.ReceiveBufferSize = 1024 * 1024;
        _udp.Client.Bind(new IPEndPoint(IPAddress.Any, options.Port));

        _cts = new CancellationTokenSource();
        _packetCount = 0;
        _lostPackets = 0;
        _latePackets = 0;
        _rejectedPackets = 0;
        _trimmedPackets = 0;
        _sessionId = null;
        _lastSequence = null;
        _lastUpdateTick = 0;
        _lastPacketTick = 0;
        _playbackStarted = false;
        _lastStatus = EngineStatus.Stopped;
        _lastStatusMessage = string.Empty;
        _lastReportedEndpoint = null;

        StartAnalytics(options);

        RaiseStatus(EngineStatus.Listening, $"Listening on UDP {options.Port}");

        var token = _cts.Token;
        var udp = _udp ?? throw new InvalidOperationException("UDP receiver was not initialized.");
        var runId = ++_runId;
        _receiveTask = Task.Run(() => ReceiveLoopGuardedAsync(udp, token, runId), token);

        StartControlChannel(options, token, runId);
    }

    /// <summary>
    /// Tears the run down and waits for the loops to leave. Safe to call when nothing is
    /// running and safe to call concurrently — an in-flight stop is joined rather than a second
    /// teardown started, because double-disposing the sockets would surface as a spurious fault.
    /// </summary>
    public async Task StopAsync()
    {
        var inFlight = _stoppingTask;
        if (inFlight is not null)
        {
            await inFlight.ConfigureAwait(false);
            return;
        }

        var task = StopCoreAsync();
        _stoppingTask = task;
        try
        {
            await task.ConfigureAwait(false);
        }
        finally
        {
            if (ReferenceEquals(_stoppingTask, task)) _stoppingTask = null;
        }
    }

    private async Task StopCoreAsync()
    {
        var cts = _cts;
        var udp = _udp;
        var receiveTask = _receiveTask;
        var control = _control;
        var controlReceive = _controlReceiveTask;
        var controlStats = _controlStatsTask;

        _cts = null;
        _udp = null;
        _receiveTask = null;
        _control = null;
        _controlReceiveTask = null;
        _controlStatsTask = null;
        _controlKey = null;
        _phoneAddress = null;

        cts?.Cancel();
        udp?.Close();
        control?.Close();
        StopMonitorOutput();

        foreach (var task in new[] { controlReceive, controlStats })
        {
            if (task is null) continue;
            try { await task.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
        }

        if (receiveTask is not null)
        {
            try { await receiveTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
        }

        // Only once every loop that can enqueue a sample has left, so the final flush sees the
        // whole run rather than racing a packet that is still being counted.
        StopAnalytics();

        udp?.Dispose();
        _playbackStarted = false;
        if (_waveOut is not null)
        {
            var waveOut = _waveOut;
            _waveOut = null;
            waveOut.PlaybackStopped -= HandlePlaybackStopped;
            try { waveOut.Stop(); }
            catch (MmException) { }
            try { waveOut.Dispose(); }
            catch (MmException) { }
        }
        _audioBuffer = null;
        _aes?.Dispose();
        _aes = null;
        cts?.Dispose();

        RaiseStatus(EngineStatus.Stopped, "Stopped");
    }

    /// <summary>
    /// Opens the diagnostics pipeline for this run: the lock-free collector the receive loop
    /// pushes samples into, and the append-only session log its closed windows are written to.
    ///
    /// Both are built here rather than lazily on the first packet because creating either one
    /// allocates, and opening the log touches the disk — neither of which the receive loop is
    /// allowed to do. From the loop's point of view the collector simply already exists.
    /// </summary>
    private void StartAnalytics(EngineOptions options)
    {
        var collector = new AnalyticsCollector(options.FrontEnd, options.BuildVersion);
        collector.Connection.MarkAttempt();
        _analytics = collector;

        SessionRecorder? recorder = null;
        try
        {
            recorder = new SessionRecorder(BuildSessionHeader(options));
        }
        catch (Exception)
        {
            // The session log is a diagnostic convenience. If it cannot be opened — read-only
            // profile, full disk — the live diagnostics must still run; only the file is lost.
        }

        _sessionRecorder = recorder;
        AnalyticsSessionPath = recorder?.FilePath;

        collector.WindowClosed += window =>
        {
            recorder?.AppendWindow(window);
            AnalyticsWindowClosed?.Invoke(this, window);
        };
        collector.EventLogged += evt => recorder?.AppendEvent(evt);
    }

    /// <summary>
    /// Forces the open windows out before shutting the collector down. Without the flush the
    /// last partial 10 s window — and, for a run shorter than a minute, every session-level
    /// number there is — would be discarded with the thread that held it.
    /// </summary>
    private void StopAnalytics()
    {
        var analytics = _analytics;
        var recorder = _sessionRecorder;
        _analytics = null;
        _sessionRecorder = null;

        if (analytics is not null)
        {
            analytics.FlushFinal();
            analytics.Dispose();
        }

        recorder?.Dispose();
    }

    /// <summary>
    /// The context two sessions have to be compared against. A 250 ms interarrival tail on a
    /// phone hotspot and the same tail on a wired LAN mean completely different things, and none
    /// of that is recoverable from the packet numbers after the fact.
    /// </summary>
    private SessionHeader BuildSessionHeader(EngineOptions options) => new(
        StartedAt: DateTimeOffset.UtcNow,

        // Genuinely unknown here: nothing has authenticated yet. The address is appended as a
        // discovery event by the control loop as soon as the first packet is attributed, which
        // keeps the log strictly append-only instead of rewriting a line that was already
        // flushed to disk.
        PhoneAddress: string.Empty,
        PcHostName: Environment.MachineName,
        NetworkSubnet: DescribeLocalSubnet(),
        OutputDevice: DescribeOutputDevice(options.OutputDeviceNumber),
        MonitorDevice: options.MonitorEnabled ? DescribeOutputDevice(options.MonitorDeviceNumber) : "off",
        PrebufferMilliseconds: _prebufferMilliseconds,
        HighWaterMilliseconds: _highWaterMilliseconds,
        VoiceEnhanceEnabled: _voice.Enabled,
        VoiceStrength: (int)Math.Round(_voice.Strength * 100f),
        ProtocolVersion: AudioPipeline.ProtocolVersion.ToString(),
        FrontEnd: options.FrontEnd,
        BuildVersion: options.BuildVersion);

    private static string DescribeOutputDevice(int deviceNumber)
    {
        if (deviceNumber < 0) return "Windows default output";

        try { return WaveOut.GetCapabilities(deviceNumber).ProductName; }
        catch (MmException) { return $"output device {deviceNumber}"; }
    }

    /// <summary>The first usable private IPv4 address and its prefix, as "address/prefix".</summary>
    private static string DescribeLocalSubnet()
    {
        try
        {
            foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (network.OperationalStatus != OperationalStatus.Up) continue;
                if (network.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;

                foreach (var unicast in network.GetIPProperties().UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    if (IPAddress.IsLoopback(unicast.Address)) continue;
                    return $"{unicast.Address}/{unicast.PrefixLength}";
                }
            }
        }
        catch (NetworkInformationException)
        {
            // Nothing here is worth failing a run over; the field is descriptive only.
        }

        return string.Empty;
    }

    /// <summary>
    /// Opens the second, independent output used purely for local monitoring. It gets its own
    /// buffer so it can never stall the routed stream: it discards on overflow and is allowed to
    /// drift, because a monitor glitch is cosmetic while a glitch on the routed path is heard by
    /// everyone in the call.
    /// </summary>
    /// <summary>
    /// Whether the stream is also played to a local speaker so the operator can hear what they
    /// are sending.
    ///
    /// This has to take effect immediately. The monitor was originally read once at
    /// <see cref="Start"/>, which meant unticking the box while streaming changed the saved
    /// preference but left the speakers playing until the receiver was restarted — the control
    /// looked broken because, in the only sense that mattered to the user, it was.
    /// </summary>
    public bool MonitorEnabled
    {
        get => _monitorEnabled;
        set
        {
            if (_monitorEnabled == value) return;
            _monitorEnabled = value;

            var options = _currentOptions;
            if (!IsRunning || options is null) return;

            if (value)
            {
                StartMonitorOutput(options with { MonitorEnabled = true });
            }
            else
            {
                StopMonitorOutput();
            }
        }
    }

    private void StartMonitorOutput(EngineOptions options)
    {
        StopMonitorOutput();
        if (!options.MonitorEnabled) return;

        // Monitoring into the same endpoint we route to would be pointless and confusing.
        if (options.MonitorDeviceNumber == options.OutputDeviceNumber) return;

        try
        {
            _monitorBuffer = new BufferedWaveProvider(new WaveFormat(AudioPipeline.SampleRate, 16, 1))
            {
                BufferDuration = TimeSpan.FromMilliseconds(300),
                DiscardOnBufferOverflow = true,
                ReadFully = true,
            };
            _monitorOut = new WaveOutEvent
            {
                DeviceNumber = options.MonitorDeviceNumber,
                DesiredLatency = 80,
                NumberOfBuffers = 3,
            };
            _monitorOut.Init(_monitorBuffer);
            _monitorOut.Play();

            MonitorStarted?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            // Monitoring is a convenience. If the chosen device will not open, carry on routing
            // without it rather than failing the whole receiver.
            StopMonitorOutput();
        }
    }

    private void StopMonitorOutput()
    {
        try { _monitorOut?.Stop(); } catch (Exception) { }
        try { _monitorOut?.Dispose(); } catch (Exception) { }
        _monitorOut = null;
        _monitorBuffer = null;
    }

    private async Task ReceiveLoopGuardedAsync(UdpClient udp, CancellationToken cancellationToken, long runId)
    {
        try
        {
            await ReceiveLoopAsync(udp, cancellationToken, runId).ConfigureAwait(false);
        }
        catch (Exception) when (cancellationToken.IsCancellationRequested)
        {
            // Normal shutdown can surface as cancellation, socket disposal, or an audio stop race.
        }
        catch (Exception exception)
        {
            if (IsCurrentRun(runId)) RaiseFault(exception.Message);
        }
    }

    private void HandlePlaybackStopped(object? sender, StoppedEventArgs eventArgs)
    {
        // With ReadFully enabled this stream never ends normally. A stop while the receiver still
        // considers playback active indicates a device removal or WaveOut failure.
        if (!ReferenceEquals(sender, _waveOut) || _cts is null || !_playbackStarted) return;
        RaiseFault(eventArgs.Exception?.Message ?? "Windows audio output stopped unexpectedly.");
    }

    /// <summary>
    /// Records the failure before reporting it, so the session log keeps the reason a run ended
    /// even though the host is about to tear that run — and the collector — down.
    /// </summary>
    private void RaiseFault(string message)
    {
        _analytics?.RecordFault(message);
        Fault?.Invoke(this, message);
    }

    /// <summary>
    /// Opens the control channel on <c>audioPort + 1</c>: answers discovery probes so the phone
    /// can find this PC, and pushes receiver statistics back so the phone can tell "transmitting"
    /// apart from "arriving". A failure here is non-fatal — audio still works, you just lose
    /// discovery and delivery confirmation — so it degrades to a status, not an exception.
    /// </summary>
    private void StartControlChannel(EngineOptions options, CancellationToken token, long runId)
    {
        var audioPort = options.Port;
        var controlPort = ControlProtocol.ControlPort(audioPort);
        try
        {
            var control = new UdpClient(AddressFamily.InterNetwork);
            control.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            control.Client.Bind(new IPEndPoint(IPAddress.Any, controlPort));
            control.EnableBroadcast = true;

            _control = control;
            _controlKey = ControlProtocol.DeriveControlKey(options.PairingKey);
            _controlReceiveTask = Task.Run(() => ControlReceiveLoopAsync(control, options, token, runId), token);
            _controlStatsTask = Task.Run(() => ControlStatsLoopAsync(control, audioPort, token, runId), token);
        }
        catch (Exception exception)
        {
            _control = null;
            _controlKey = null;
            if (!IsCurrentRun(runId)) return;
            RaiseStatus(
                EngineStatus.ControlChannelUnavailable,
                $"Audio listening; control port {controlPort} unavailable ({exception.Message})");
        }
    }

    private async Task ControlReceiveLoopAsync(UdpClient control, EngineOptions options, CancellationToken token, long runId)
    {
        var audioPort = options.Port;
        var hostName = Environment.MachineName;

        // Probes repeat for as long as the phone is searching, so only a change of peer is worth
        // a line in the session log.
        IPEndPoint? lastProbe = null;
        IPEndPoint? lastKeyMismatch = null;

        while (!token.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await control.ReceiveAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }
            catch (ObjectDisposedException) { break; }
            catch (SocketException) { continue; }

            if (!ControlProtocol.TryParse(result.Buffer, out var type, out var payload))
            {
                // A control frame this build cannot read is dropped, as it must be — but a peer
                // speaking a different framing version is a fact worth recording rather than a
                // silence to be puzzled over later.
                if (ControlProtocol.TryPeekVersion(result.Buffer, result.Buffer.Length, out var peerVersion) &&
                    peerVersion != ControlProtocol.Version)
                {
                    _analytics?.Connection.MarkVersionMismatch();
                    _analytics?.RecordDiscoveryEvent(
                        "version-mismatch",
                        $"{result.RemoteEndPoint} speaks control protocol v{peerVersion}, this receiver speaks v{ControlProtocol.Version}");
                }

                continue;
            }

            var key = _controlKey;
            if (key is null) continue;

            // Settings pushes must be authenticated — unlike discovery, there is no reason to
            // honour one from a peer that cannot prove it shares the pairing key.
            if (type == ControlProtocol.TypeConfig)
            {
                if (!ControlProtocol.Verify(key, result.Buffer)) continue;
                if (!ControlProtocol.TryReadConfig(payload, out var dsp)) continue;
                if (runId != _runId) continue;
                _voice.Apply(dsp);
                _analytics?.RecordDiscoveryEvent(
                    "dsp-config",
                    $"enabled {dsp.Enabled}, HPF {dsp.HighPassHz} Hz, gate {dsp.Gate:F2}, comp {dsp.Compressor:F2}");
                DspConfigReceived?.Invoke(this, dsp);
                continue;
            }

            if (type != ControlProtocol.TypeProbe) continue;

            // Reply even when the HMAC does not verify. A phone with the wrong pairing key must
            // learn that this PC exists but disagrees, otherwise a key typo is indistinguishable
            // from an absent receiver — and the reply is signed with our key, so a mismatched
            // phone can detect the disagreement without either side leaking the key.
            var nonce = new byte[ControlProtocol.NonceSize];
            if (payload.Length >= ControlProtocol.NonceSize)
            {
                payload.AsSpan(0, ControlProtocol.NonceSize).CopyTo(nonce);
            }

            var announce = ControlProtocol.Build(
                key,
                ControlProtocol.TypeAnnounce,
                ControlProtocol.AnnouncePayload(
                    nonce,
                    audioPort,
                    hostName,
                    AudioPipeline.ProtocolVersion,
                    options.FrontEnd,
                    options.BuildVersion));

            try
            {
                await control.SendAsync(announce, result.RemoteEndPoint, token).ConfigureAwait(false);
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { break; }

            if (!result.RemoteEndPoint.Equals(lastProbe))
            {
                lastProbe = result.RemoteEndPoint;
                _analytics?.RecordDiscoveryEvent("probe", result.RemoteEndPoint.ToString());
            }

            // A probe that does not authenticate is a discovery failure from the phone's side:
            // it found a receiver that will not agree with it. Counted once per peer, because
            // probes repeat every second for as long as the phone keeps looking.
            if (!ControlProtocol.Verify(key, result.Buffer) && !result.RemoteEndPoint.Equals(lastKeyMismatch))
            {
                lastKeyMismatch = result.RemoteEndPoint;
                _analytics?.Connection.MarkDiscoveryFailure();
                _analytics?.RecordDiscoveryEvent("probe-key-mismatch", result.RemoteEndPoint.ToString());
            }

            if (runId != _runId) break;
        }
    }

    private async Task ControlStatsLoopAsync(UdpClient control, int audioPort, CancellationToken token, long runId)
    {
        var controlPort = ControlProtocol.ControlPort(audioPort);

        // The session header cannot name the phone, because at that point nothing has
        // authenticated. This loop already wakes twice a second off the audio path, so it is
        // where the address is noticed and logged — doing it from the receive loop would put a
        // string format and a flushed disk write in the packet path for no benefit.
        IPAddress? loggedPhone = null;

        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(500, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { break; }

            if (runId != _runId) break;
            var phone = _phoneAddress;
            var key = _controlKey;
            if (phone is null || key is null) continue;

            if (!phone.Equals(loggedPhone))
            {
                loggedPhone = phone;
                _analytics?.RecordDiscoveryEvent("phone", phone.ToString());
            }

            CheckForSilence(runId);

            var snapshot = Stats;
            var frame = ControlProtocol.Build(
                key,
                ControlProtocol.TypeStats,
                ControlProtocol.StatsPayload(
                    snapshot.Packets,
                    snapshot.Lost,
                    snapshot.Late,
                    snapshot.Rejected,
                    snapshot.Trimmed,
                    (int)snapshot.BufferedMilliseconds,
                    snapshot.Playing));

            try
            {
                await control.SendAsync(frame, new IPEndPoint(phone, controlPort), token).ConfigureAwait(false);
            }
            catch (SocketException) { }
            catch (ObjectDisposedException) { break; }
        }
    }

    /// <summary>Audio has to be absent for this long before the phone counts as paused.</summary>
    private const int PausedAfterMilliseconds = 1_500;

    /// <summary>
    /// And this long before it counts as gone. The phone's own reconnect gives up at 60 s, so a
    /// receiver that declared the phone lost sooner than the phone declares the receiver lost
    /// would routinely contradict a phone that was still trying and about to succeed.
    /// </summary>
    private const int LostAfterMilliseconds = 8_000;

    /// <summary>
    /// Reports audio that has stopped arriving.
    ///
    /// The receive loop cannot do this: its whole existence is driven by packets, so the one
    /// thing it can never notice is their absence. Without this the last status the loop raised
    /// stayed on screen indefinitely, and a receiver whose phone had been switched off half an
    /// hour earlier still read "Receiving audio" — the single most misleading thing it could say.
    ///
    /// Paused and lost are separated because they call for different actions: a pause is waited
    /// out, a loss means going and looking at the phone.
    /// </summary>
    private void CheckForSilence(long runId)
    {
        var lastPacket = _lastPacketTick;
        if (lastPacket == 0 || !IsCurrentRun(runId)) return;

        var analytics = _analytics;
        var silentFor = Environment.TickCount64 - lastPacket;

        if (silentFor < PausedAfterMilliseconds)
        {
            // Audio is flowing. Close any open silence episode; the receive loop owns the status
            // from here and will have already replaced whatever this method last raised.
            if (analytics?.Connection.MarkSilenceEnded() is { } elapsed)
            {
                analytics.RecordDiscoveryEvent("phone-resumed", $"after {elapsed:F0} ms of silence");
            }

            return;
        }

        if (analytics is not null && analytics.Connection.MarkSilenceBegan())
        {
            analytics.RecordDiscoveryEvent("phone-silent", $"no audio for {silentFor} ms");
        }

        if (silentFor >= LostAfterMilliseconds)
        {
            RaiseStatus(
                EngineStatus.PhoneLost,
                $"No audio for {silentFor / 1000} s — the phone has stopped sending");
        }
        else
        {
            RaiseStatus(EngineStatus.PhonePaused, "Phone connected but paused — no audio arriving");
        }
    }

    /// <summary>
    /// The audio path. Every working buffer is allocated once, before the loop: at 100 packets a
    /// second a per-packet allocation would be the only garbage the process produces, and a
    /// collection landing mid-stream is audible.
    /// </summary>
    private async Task ReceiveLoopAsync(UdpClient udp, CancellationToken cancellationToken, long runId)
    {
        var pcm = new byte[AudioPipeline.PacketPcmBytes];
        var nonce = new byte[12];
        var discard = new byte[AudioPipeline.PacketPcmBytes];
        var conceal = new byte[AudioPipeline.PacketPcmBytes];
        var lastGood = new byte[AudioPipeline.PacketPcmBytes];
        var haveLastGood = false;

        // Captured once: the collector exists for the whole run, and every call below is a
        // timestamp plus a push onto a lock-free ring, so recording a packet costs the same as
        // incrementing one of the counters beside it.
        var analytics = _analytics;

        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await udp.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            var data = result.Buffer;
            if (!TryDecryptPacket(data, nonce, pcm, out var sessionId, out var sequence, out var sampleRate) ||
                sampleRate != AudioPipeline.SampleRate)
            {
                _rejectedPackets++;
                analytics?.RecordRejected();

                // A sender speaking a different audio protocol is separated from a sender we
                // cannot authenticate, because they look identical in the rejection count and
                // call for completely different fixes. Only reported on the throttled tick, so
                // a flood of incompatible datagrams cannot turn into a flood of log lines.
                if (ShouldRaiseUpdate() && IsCurrentRun(runId))
                {
                    if (AudioPipeline.TryPeekProtocolVersion(data, out var senderVersion) &&
                        senderVersion != AudioPipeline.ProtocolVersion)
                    {
                        analytics?.Connection.MarkVersionMismatch();
                        analytics?.RecordDiscoveryEvent(
                            "version-mismatch",
                            $"{result.RemoteEndPoint} sends audio protocol v{senderVersion}, this receiver speaks v{AudioPipeline.ProtocolVersion}");
                        RaiseStatus(
                            EngineStatus.DatagramRejected,
                            $"Datagram rejected — the sender uses audio protocol v{senderVersion} and this receiver speaks v{AudioPipeline.ProtocolVersion}");
                    }
                    else
                    {
                        RaiseStatus(EngineStatus.DatagramRejected, "Datagram rejected — key or sender is incompatible");
                    }

                    RaiseEndpoint(result.RemoteEndPoint);
                    StatsUpdated?.Invoke(this, Stats);
                }
                continue;
            }

            // Remember where authenticated audio came from so statistics can be sent back.
            _phoneAddress = result.RemoteEndPoint.Address;
            _lastPacketTick = Environment.TickCount64;
            analytics?.Connection.MarkAuthenticatedPacket();

            if (_sessionId != sessionId)
            {
                _sessionId = sessionId;
                _lastSequence = null;
                ResetPlaybackForResync();
            }

            if (_lastSequence.HasValue)
            {
                var expected = _lastSequence.Value + 1;
                var delta = unchecked((int)(sequence - expected));
                if (delta < 0)
                {
                    _latePackets++;

                    // The single "late" counter hides the two failures it can mean. A delta of
                    // exactly -1 is the sequence already accepted arriving a second time — a
                    // duplicate, which is harmless. Anything older is a genuine reorder, which
                    // means the network delivered out of order and the jitter buffer was too
                    // shallow to absorb it. Splitting them costs one comparison here.
                    if (delta == -1) analytics?.RecordDuplicate();
                    else analytics?.RecordReordered();
                    continue;
                }

                if (delta > 0)
                {
                    _lostPackets += delta;
                    analytics?.RecordLost(delta);
                    if (delta <= AudioPipeline.MaxConcealedGapPackets)
                    {
                        for (var index = 0; index < delta; index++)
                        {
                            // Substituting digital silence for a lost frame produces a hard edge
                            // in the waveform, and a run of those is heard as crackle. Repeating
                            // the last good frame at a decaying level fades into the gap instead,
                            // which is far less audible for the short gaps that dominate here.
                            AudioPipeline.BuildConcealmentFrame(conceal, lastGood, haveLastGood, index);
                            _audioBuffer?.AddSamples(conceal, 0, conceal.Length);
                        }
                    }
                    else
                    {
                        ResetPlaybackForResync();
                    }
                }
            }

            var buffer = _audioBuffer;
            // Condition before buffering, and measure the level after conditioning so the trim
            // logic sees the gated signal rather than raw room noise.
            _voice.Process(pcm);
            _recentRms = AudioPipeline.SmoothedRms(_recentRms, pcm);
            buffer?.AddSamples(pcm, 0, pcm.Length);
            _monitorBuffer?.AddSamples(pcm, 0, pcm.Length);
            Buffer.BlockCopy(pcm, 0, lastGood, 0, AudioPipeline.PacketPcmBytes);
            haveLastGood = true;
            _lastSequence = sequence;
            _packetCount++;
            analytics?.RecordDelivered(
                (int)(buffer?.BufferedDuration.TotalMilliseconds ?? 0),
                LevelDbfs(_recentRms),
                _voice.GateActive);

            if (buffer is not null)
            {
                // Discarding a 10 ms frame to claw back latency is audible as a click when it
                // lands mid-syllable. Doing the same correction during a pause is inaudible. So
                // trim only while the input is quiet — unless the backlog has grown past a hard
                // ceiling, where latency matters more than one artefact.
                while (buffer.BufferedBytes >= AudioPipeline.PacketPcmBytes &&
                       AudioPipeline.ShouldTrim(
                           buffer.BufferedDuration.TotalMilliseconds,
                           _highWaterMilliseconds,
                           _recentRms))
                {
                    buffer.Read(discard, 0, discard.Length);
                    _trimmedPackets++;
                    analytics?.RecordTrimmed((int)buffer.BufferedDuration.TotalMilliseconds);
                }

                if (!_playbackStarted && buffer.BufferedDuration.TotalMilliseconds >= _prebufferMilliseconds)
                {
                    _waveOut?.Play();
                    _playbackStarted = true;
                }
            }

            if (ShouldRaiseUpdate() && IsCurrentRun(runId))
            {
                RaiseStatus(
                    _playbackStarted ? EngineStatus.Receiving : EngineStatus.Buffering,
                    _playbackStarted ? "Receiving audio" : "Buffering audio");
                RaiseEndpoint(result.RemoteEndPoint);
                StatsUpdated?.Invoke(this, Stats);
            }
        }
    }

    /// <summary>
    /// The conditioned input level as decibels relative to full scale, or <see cref="float.NaN"/>
    /// when the frame is digital silence and therefore has no logarithm.
    ///
    /// NaN specifically, not negative infinity. The collector packs the level into a short as
    /// centi-dBFS, and negative infinity is a real number to <c>Math.Clamp</c>: it saturated at
    /// -327.67 dBFS and was then averaged in as if it were a measurement, so a muted phone
    /// dragged <c>LevelDbfsAvg</c> down to an impossible figure. NaN is the sentinel the
    /// collector already recognises as "not measured", which is exactly what silence is.
    /// </summary>
    private static float LevelDbfs(float rms) => rms > 0f ? 20f * MathF.Log10(rms) : float.NaN;

    private void ResetPlaybackForResync()
    {
        _playbackStarted = false;
        try { _waveOut?.Pause(); }
        catch { }
        _audioBuffer?.ClearBuffer();
    }

    private bool TryDecryptPacket(
        byte[] data,
        byte[] nonce,
        byte[] pcm,
        out ulong sessionId,
        out uint sequence,
        out int sampleRate)
    {
        sessionId = 0;
        sequence = 0;
        sampleRate = 0;
        if (_aes is null) return false;

        return AudioPipeline.TryDecrypt(_aes, data, nonce, pcm, out sessionId, out sequence, out sampleRate);
    }

    /// <summary>
    /// Packets arrive every 10 ms; no display needs updating a hundred times a second, and a
    /// host that repainted at that rate would spend more time in layout than in audio.
    /// </summary>
    private bool ShouldRaiseUpdate()
    {
        var now = Environment.TickCount64;
        if (now - _lastUpdateTick < 250) return false;
        _lastUpdateTick = now;
        return true;
    }

    /// <summary>
    /// True while <paramref name="runId"/> is still the live run. A loop that was cancelled can
    /// still be unwinding, and anything it publishes now belongs to a run that no longer exists.
    /// </summary>
    private bool IsCurrentRun(long runId) => runId == _runId && _cts is not null;

    private void RaiseStatus(EngineStatus status, string message)
    {
        if (status == _lastStatus && message == _lastStatusMessage) return;
        _lastStatus = status;
        _lastStatusMessage = message;
        StatusChanged?.Invoke(this, new EngineStatusUpdate(status, message));
    }

    private void RaiseEndpoint(IPEndPoint endpoint)
    {
        if (endpoint.Equals(_lastReportedEndpoint)) return;
        _lastReportedEndpoint = endpoint;
        PhoneAddressChanged?.Invoke(this, endpoint);
    }
}
