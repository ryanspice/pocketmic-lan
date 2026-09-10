using System.Drawing;
using System.Drawing.Imaging;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using NAudio;
using NAudio.Wave;
using QRCoder;

namespace PocketMicReceiver;

internal static class Program
{
    /// <summary>
    /// Held for as long as this process intends to be the one running receiver. Kept in a static
    /// so the garbage collector cannot finalize it — releasing the mutex would let a second
    /// receiver start while this one is still holding the sockets.
    /// </summary>
    private static Mutex? _instanceLock;

    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        // Before any window exists, because a receiver that fails to bind reports a socket error
        // that says nothing about the real cause. Two front ends sharing one settings file and
        // one pair of UDP ports cost a live debugging session: the second silently lost the
        // bind, and the phone reported "no reply from PC" with nothing actually wrong.
        if (!EnsureSoleReceiver()) return;

        Application.Run(new MainForm());
    }

    /// <summary>
    /// Makes sure no other receiver is holding the sockets before this one tries to bind them.
    ///
    /// The other instance is asked about rather than closed silently — it may be mid-call — and
    /// declining exits, because two receivers on one machine is the situation this prevents.
    /// <see cref="ReceiverSettings.SilentTakeover"/> skips the question for the launcher path,
    /// where the user has already chosen which front end they want.
    /// </summary>
    /// <returns>False when the user chose to leave the other instance running and exit.</returns>
    private static bool EnsureSoleReceiver()
    {
        var settings = ReceiverSettings.Load();
        var others = SingleInstance.FindOthers();

        if (others.Count == 0)
        {
            // Nothing to negotiate with. If the run lock was refused anyway, its holder is a
            // receiver this build cannot see, and the port bind will describe that far more
            // accurately than a guess would.
            _instanceLock = SingleInstance.TryAcquire();
            return true;
        }

        if (!settings.SilentTakeover)
        {
            var answer = MessageBox.Show(
                others.Any(instance => !instance.IsClassic)
                    ? "The new PocketMic receiver is already running and is holding the network ports this app needs.\r\n\r\nClose it and continue?"
                    : "Another copy of PocketMic Receiver is already running and is holding the network ports this app needs.\r\n\r\nClose it and continue?",
                "PocketMic is already running",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button1);

            if (answer != DialogResult.Yes) return false;
        }

        if (!SingleInstance.CloseOthersAsync(settings.Port).GetAwaiter().GetResult())
        {
            MessageBox.Show(
                "The other receiver did not release the network ports.\r\n\r\n" +
                "Close it from its own window or the notification area, then start this one again.",
                "PocketMic",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        _instanceLock = SingleInstance.TryAcquire();
        return true;
    }
}

/// <summary>
/// The WinForms front end. It owns layout, device selection, settings and the tray icon, and
/// nothing else: the sockets, audio devices and counters live in <see cref="PocketMicEngine"/>
/// so the WinUI 3 front end drives the same engine rather than a second copy of it.
///
/// Engine events arrive on worker threads, so every handler here marshals through
/// <see cref="PostUi"/> and re-checks <c>_runId</c> inside the posted action — a callback queued
/// before a stop can otherwise land on controls belonging to the run that replaced it.
/// </summary>
internal sealed class MainForm : Form
{
    private const int DefaultPrebufferMilliseconds = AudioPipeline.DefaultPrebufferMilliseconds;

    private readonly PocketMicEngine _engine = new();

    private readonly TextBox _portText = new() { Text = "49500", Width = 110 };
    private readonly TextBox _keyText = new() { Width = 280, UseSystemPasswordChar = true };
    private readonly ComboBox _outputCombo = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 430 };
    private readonly CheckBox _autoListenCheck = new()
    {
        Text = "Start listening automatically (lets the phone discover this PC)",
        AutoSize = true,
        Checked = true,
    };

    private readonly TrackBar _bufferSlider = new()
    {
        Minimum = 40,
        Maximum = 300,
        TickFrequency = 20,
        SmallChange = 10,
        LargeChange = 20,
        Width = 320,
        Value = DefaultPrebufferMilliseconds,
    };

    private readonly Label _bufferLabel = new() { AutoSize = true };
    private readonly CheckBox _minimizeToTrayCheck = new()
    {
        Text = "Close to tray instead of exiting",
        AutoSize = true,
        Checked = true,
    };

    private readonly NotifyIcon _trayIcon = new() { Visible = false };
    private bool _exitRequested;

    private readonly CheckBox _voiceEnhanceCheck = new()
    {
        Text = "Voice enhancement (high-pass, noise gate, presence EQ, compressor, limiter)",
        AutoSize = true,
        Checked = true,
    };

    private readonly TrackBar _voiceStrength = new()
    {
        Minimum = 0,
        Maximum = 100,
        TickFrequency = 10,
        Width = 320,
        Value = 60,
    };

    private readonly Label _voiceStrengthLabel = new() { AutoSize = true };
    private readonly Label _routingLabel = new() { AutoSize = true, MaximumSize = new Size(760, 0) };
    private readonly Button _makeDefaultMicButton = new()
    {
        Text = "Use PocketMic as Windows microphone",
        AutoSize = true,
        Enabled = false,
    };

    private string? _previousDefaultCaptureId;
    private bool _isDefaultMic;

    // Routing the stream into a virtual cable makes it available to other applications but
    // silences it for the person running PocketMic, because a cable is not a speaker. The
    // monitor is a second, independent output so you can hear what you are sending.
    private readonly CheckBox _monitorCheck = new()
    {
        Text = "Also play through speakers so I can hear it",
        AutoSize = true,
        Checked = true,
    };

    private readonly ComboBox _monitorCombo = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 430,
    };

    /// <summary>
    /// Controls hidden unless Advanced mode is on. Everything here has a sensible default that
    /// auto-discovery or auto-selection already fills in, so the ordinary path is: open the app,
    /// press nothing, talk.
    /// </summary>
    private readonly List<Control> _advancedControls = new();

    private readonly CheckBox _advancedCheck = new()
    {
        Text = "Advanced mode",
        AutoSize = true,
    };

    private readonly ReceiverSettings _settings = ReceiverSettings.Load();
    private readonly Button _startButton = new() { Text = "Start receiver", AutoSize = true };
    private readonly Button _copyIpButton = new() { Text = "Copy first IP", AutoSize = true };
    private readonly Button _showQrButton = new() { Text = "Show QR for phone", AutoSize = true };
    private readonly Label _statusLabel = new() { Text = "Stopped", AutoSize = true };
    private readonly Label _sourceLabel = new() { Text = "Phone: —", AutoSize = true };
    private readonly Label _statsLabel = new()
    {
        Text = "Packets: 0   Lost: 0   Late: 0   Rejected: 0   Trimmed: 0   Buffer: 0 ms",
        AutoSize = true,
    };
    private readonly Label _linkQualityLabel = new()
    {
        Text = "Link: —",
        AutoSize = true,
    };
    private readonly TextBox _addressesText = new() { Multiline = true, ReadOnly = true, Height = 78, Dock = DockStyle.Fill };

    private readonly AnalyticsPanel _analyticsPanel = new()
    {
        Width = 780,
        Height = 240,
        Margin = new Padding(0, 0, 0, 8),
    };

    private readonly Button _exportReportButton = new() { Text = "Export session report", AutoSize = true };

    private Task? _stoppingTask;
    private bool _handlingFault;
    private bool _closing;
    private long _runId;

    public MainForm()
    {
        Text = "PocketMic Receiver";
        MinimumSize = new Size(860, 540);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 1,
            RowCount = 1,
            AutoScroll = true,
        };
        Controls.Add(root);

        var title = new Label
        {
            Text = "PocketMic Receiver",
            Font = new Font(Font.FontFamily, 21F, FontStyle.Bold),
            ForeColor = GoldInk,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
        };
        root.Controls.Add(title);

        root.Controls.Add(new Label
        {
            Text = "Receives encrypted microphone audio from the Android app over your local network.",
            AutoSize = true,
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 0, 0, 18),
        });

        root.Controls.Add(_advancedCheck);
        _advancedCheck.CheckedChanged += (_, _) => ApplyAdvancedMode();

        var addressSection = SectionLabel("PC address to enter on the phone");
        root.Controls.Add(addressSection);
        RefreshLocalAddresses();
        root.Controls.Add(_addressesText);
        root.Controls.Add(_copyIpButton);
        root.Controls.Add(_showQrButton);
        _advancedControls.AddRange(new Control[] { addressSection, _addressesText, _copyIpButton, _showQrButton });
        _copyIpButton.Margin = new Padding(0, 6, 6, 18);
        _showQrButton.Margin = new Padding(0, 6, 0, 18);
        _copyIpButton.Click += (_, _) =>
        {
            var first = GetLocalIpv4Addresses().FirstOrDefault();
            if (first is not null)
            {
                try { Clipboard.SetText(first.ToString()); }
                catch (ExternalException) { }
            }
        };
        _showQrButton.Click += (_, _) => ShowQrCode();

        var connectionGrid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 16),
        };
        connectionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        connectionGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        connectionGrid.Controls.Add(new Label { Text = "UDP port", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 16, 8) }, 0, 0);
        connectionGrid.Controls.Add(_portText, 1, 0);
        connectionGrid.Controls.Add(new Label { Text = "Pairing key", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 16, 8) }, 0, 1);
        connectionGrid.Controls.Add(_keyText, 1, 1);
        connectionGrid.Controls.Add(new Label { Text = "Playback output", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 16, 8) }, 0, 2);
        connectionGrid.Controls.Add(_outputCombo, 1, 2);
        connectionGrid.Controls.Add(new Label { Text = "Monitor through", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 16, 8) }, 0, 3);
        connectionGrid.Controls.Add(_monitorCombo, 1, 3);
        root.Controls.Add(connectionGrid);
        root.Controls.Add(_monitorCheck);
        root.Controls.Add(_routingLabel);
        root.Controls.Add(_makeDefaultMicButton);
        _makeDefaultMicButton.Click += (_, _) => MakeVirtualCableDefaultMicrophone();
        root.Controls.Add(_autoListenCheck);
        root.Controls.Add(_minimizeToTrayCheck);
        _advancedControls.AddRange(new Control[] { connectionGrid, _autoListenCheck, _minimizeToTrayCheck });

        var bufferSection = SectionLabel("Jitter buffer");
        root.Controls.Add(bufferSection);
        root.Controls.Add(_bufferLabel);
        root.Controls.Add(_bufferSlider);
        _bufferSlider.ValueChanged += (_, _) => ApplyBufferSetting();
        _advancedControls.AddRange(new Control[] { bufferSection, _bufferLabel, _bufferSlider });

        var voiceSection = SectionLabel("Voice processing");
        root.Controls.Add(voiceSection);
        root.Controls.Add(_voiceEnhanceCheck);
        root.Controls.Add(_voiceStrengthLabel);
        root.Controls.Add(_voiceStrength);
        _advancedControls.AddRange(new Control[] { voiceSection, _voiceEnhanceCheck, _voiceStrengthLabel, _voiceStrength });
        _voiceEnhanceCheck.CheckedChanged += (_, _) => ApplyVoiceSetting();
        _voiceStrength.ValueChanged += (_, _) => ApplyVoiceSetting();

        SubscribeToEngine();
        PopulateOutputs();
        ApplySettings();
        SetUpTrayIcon();

        _startButton.Height = 42;
        _startButton.Padding = new Padding(14, 4, 14, 4);
        _startButton.Click += async (_, _) => await ToggleReceiverAsync();
        root.Controls.Add(_startButton);

        var statusPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(12),
            Margin = new Padding(0, 16, 0, 16),
            BackColor = WarmPaper,
        };
        _statusLabel.Font = new Font(Font.FontFamily, 11F, FontStyle.Bold);
        _statusLabel.ForeColor = GoldInk;
        statusPanel.Controls.Add(_statusLabel);
        statusPanel.Controls.Add(_sourceLabel);
        statusPanel.Controls.Add(_statsLabel);
        statusPanel.Controls.Add(_linkQualityLabel);
        root.Controls.Add(statusPanel);

        var diagnosticsSection = SectionLabel("Diagnostics");
        root.Controls.Add(diagnosticsSection);
        root.Controls.Add(_analyticsPanel);
        root.Controls.Add(_exportReportButton);
        _exportReportButton.Click += (_, _) => ExportSessionReport();
        _advancedControls.AddRange(new Control[] { diagnosticsSection, _analyticsPanel, _exportReportButton });

        root.Controls.Add(new Label
        {
            Text = "For Discord, Teams, OBS, or games: select CABLE Input as the playback output here, then select CABLE Output as the microphone in the target app. For a quick test, select your speakers instead.",
            AutoSize = true,
            MaximumSize = new Size(780, 0),
            ForeColor = Color.DimGray,
            Margin = new Padding(0, 12, 0, 0),
        });

        // Applied last: the diagnostics controls are registered after ApplySettings has already
        // run, and every advanced control has to exist in the list before visibility is decided
        // or it would stay on screen in Basic mode until the checkbox was next toggled.
        ApplyAdvancedMode();

        FormClosing += HandleFormClosing;
    }

    /// <summary>
    /// Names of virtual audio cables whose render endpoint is mirrored to a capture endpoint.
    /// Selecting one of these is what turns PocketMic into something other applications can
    /// pick as a microphone, so choose it automatically rather than making the user work it out.
    /// Ordered by preference.
    /// </summary>
    private static readonly string[] VirtualCableHints =
    {
        "CABLE Input",          // VB-CABLE, the common case
        "VoiceMeeter Input",    // VoiceMeeter VAIO
        "VoiceMeeter Aux Input",
        "VB-Audio",
        "Virtual Cable",
        "PocketMic",            // our own driver, once it exists
    };

    private void DescribeRouting(DeviceItem? device)
    {
        if (device is null) return;
        var name = device.ToString();
        var isCable = VirtualCableHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase));

        if (isCable)
        {
            _routingLabel.Text =
                $"Routing through \"{name}\" — other apps can select PocketMic as a microphone.";
            _routingLabel.ForeColor = Color.DarkGreen;
            _makeDefaultMicButton.Enabled = DefaultDeviceSwitcher.FindVirtualCaptureDevice() is not null;
        }
        else
        {
            _routingLabel.Text =
                $"Playing to \"{name}\". This is audible here but not available to other apps as a microphone.";
            _routingLabel.ForeColor = Color.DarkOrange;
        }
    }

    private void AutoSelectVirtualCable()
    {
        foreach (var hint in VirtualCableHints)
        {
            foreach (var item in _outputCombo.Items)
            {
                if (item is not DeviceItem device) continue;
                if (device.ToString().Contains(hint, StringComparison.OrdinalIgnoreCase))
                {
                    _outputCombo.SelectedItem = item;
                    _routingLabel.Text =
                        $"Routing through \"{device}\" — select its matching Output device as your microphone in Discord, OBS, or Teams.";
                    _routingLabel.ForeColor = Color.DarkGreen;
                    _makeDefaultMicButton.Enabled = DefaultDeviceSwitcher.FindVirtualCaptureDevice() is not null;
                    return;
                }
            }
        }

        _routingLabel.Text =
            "No virtual audio cable detected. Audio will play through speakers only. " +
            "To use PocketMic as a microphone in other apps, install VB-CABLE (vb-audio.com/Cable) and restart this app.";
        _routingLabel.ForeColor = Color.DarkOrange;
    }

    /// <summary>
    /// Points Windows' default recording device at the virtual cable's capture endpoint, which
    /// is the last manual step between "PocketMic is running" and "apps hear the phone".
    ///
    /// Deliberately a button rather than something done on startup: this is a system-wide
    /// setting, and silently repointing someone's microphone — then leaving it pointed at a
    /// dead cable after PocketMic closes — would be hostile. The previous default is captured
    /// so it can be restored.
    /// </summary>
    private void MakeVirtualCableDefaultMicrophone()
    {
        var device = DefaultDeviceSwitcher.FindVirtualCaptureDevice();
        if (device is null)
        {
            MessageBox.Show(
                this,
                "No virtual audio cable recording device was found.\r\n\r\n" +
                "Install VB-CABLE from vb-audio.com/Cable, reboot, then restart PocketMic.",
                "PocketMic",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        // Toggle back if we already redirected it.
        if (_isDefaultMic)
        {
            var restoreError = "No previous microphone was recorded to restore.";
            var restored = _previousDefaultCaptureId is not null &&
                DefaultDeviceSwitcher.TrySetDefaultCapture(_previousDefaultCaptureId, out restoreError);

            if (restored)
            {
                _isDefaultMic = false;
                _makeDefaultMicButton.Text = "Use PocketMic as Windows microphone";
                _routingLabel.Text = "Previous Windows microphone restored.";
                _routingLabel.ForeColor = Color.DimGray;
            }
            else
            {
                MessageBox.Show(this, restoreError, "PocketMic", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            return;
        }

        _previousDefaultCaptureId ??= DefaultDeviceSwitcher.CurrentDefaultCaptureId();

        if (DefaultDeviceSwitcher.TrySetDefaultCapture(device.ID, out var error))
        {
            _isDefaultMic = true;
            _routingLabel.Text =
                $"\"{device.FriendlyName}\" is now the Windows default microphone. " +
                "Apps set to System Default will hear your phone.";
            _routingLabel.ForeColor = Color.DarkGreen;
            _makeDefaultMicButton.Text = "Restore previous Windows microphone";
        }
        else
        {
            MessageBox.Show(
                this,
                $"Could not change the default recording device.\r\n\r\n{error}\r\n\r\n" +
                "You can set it manually in Windows Sound settings instead.",
                "PocketMic",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static Label SectionLabel(string text) => new()
    {
        Text = text,
        Font = new Font("Segoe UI", 11F, FontStyle.Bold),
        ForeColor = GoldInk,
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 6),
    };

    // Warm paper + gold, matching the design system used by the Android app and the web
    // surface (rgba(184,138,59) accent on a paper background).
    private static readonly Color GoldInk = Color.FromArgb(138, 101, 35);
    private static readonly Color Gold = Color.FromArgb(184, 138, 59);
    private static readonly Color WarmPaper = Color.FromArgb(246, 241, 231);

    private void ApplySettings()
    {
        _portText.Text = _settings.Port.ToString();
        _keyText.Text = _settings.PairingKey;
        _autoListenCheck.Checked = _settings.AutoListen;
        _minimizeToTrayCheck.Checked = _settings.MinimizeToTray;
        _bufferSlider.Value = Math.Clamp(_settings.PrebufferMilliseconds, _bufferSlider.Minimum, _bufferSlider.Maximum);
        ApplyBufferSetting();
        _voiceEnhanceCheck.Checked = _settings.VoiceEnhance;
        _voiceStrength.Value = Math.Clamp(_settings.VoiceStrength, _voiceStrength.Minimum, _voiceStrength.Maximum);
        ApplyVoiceSetting();

        var restored = false;
        if (!string.IsNullOrEmpty(_settings.OutputDevice))
        {
            foreach (var item in _outputCombo.Items)
            {
                if (item is DeviceItem device && device.ToString() == _settings.OutputDevice)
                {
                    _outputCombo.SelectedItem = item;
                    restored = true;
                    break;
                }
            }
        }

        if (restored)
        {
            // A restored device still has to be explained — the routing line is the only place
            // Basic mode says where audio is actually going.
            DescribeRouting(_outputCombo.SelectedItem as DeviceItem);
        }
        else
        {
            AutoSelectVirtualCable();
        }

        _advancedCheck.Checked = _settings.AdvancedMode;
        ApplyAdvancedMode();

        _monitorCheck.Checked = _settings.MonitorEnabled;
        AutoSelectMonitorDevice();
        _monitorCheck.CheckedChanged += (_, _) =>
        {
            _settings.MonitorEnabled = _monitorCheck.Checked;
            _monitorCombo.Enabled = _monitorCheck.Checked;
            // Apply live rather than only at the next start: the speakers must go quiet the
            // moment the box is unticked, not whenever the receiver happens to restart.
            _engine.MonitorEnabled = _monitorCheck.Checked;
            _settings.Save();
        };
        _monitorCombo.Enabled = _monitorCheck.Checked;

        _autoListenCheck.CheckedChanged += (_, _) =>
        {
            _settings.AutoListen = _autoListenCheck.Checked;
            _settings.Save();
        };

        // Auto-listen has to run after the window exists, otherwise a failure would try to show
        // a message box against an unshown form.
        Shown += async (_, _) =>
        {
            if (_autoListenCheck.Checked && _keyText.Text.Length >= 8 && !_engine.IsRunning)
            {
                await ToggleReceiverAsync();
            }
        };
    }

    /// <summary>
    /// Engine events arrive on socket and audio threads. Each handler snapshots the run
    /// generation at the moment it is raised and re-checks it inside the posted action, so a
    /// callback queued just before a stop cannot repaint the run that replaced it.
    /// </summary>
    private void SubscribeToEngine()
    {
        _engine.StatusChanged += (_, update) =>
        {
            var runId = _runId;
            PostUi(() =>
            {
                if (runId != _runId || !_engine.IsRunning) return;
                _statusLabel.Text = update.Message;
                _statusLabel.ForeColor = StatusColor(update.Status);
            });
        };

        _engine.StatsUpdated += (_, stats) =>
        {
            var runId = _runId;
            var text = FormatStats(stats);
            PostUi(() =>
            {
                if (runId != _runId || !_engine.IsRunning) return;
                _statsLabel.Text = text;
            });
        };

        _engine.PhoneAddressChanged += (_, endpoint) =>
        {
            var runId = _runId;
            var source = endpoint.ToString();
            PostUi(() =>
            {
                if (runId != _runId || !_engine.IsRunning) return;
                _sourceLabel.Text = $"Phone: {source}";
            });
        };

        _engine.DspConfigReceived += (_, dsp) =>
        {
            var runId = _runId;
            PostUi(() =>
            {
                if (runId != _runId) return;
                // Setting the checkbox re-runs ApplyVoiceSetting, which would overwrite the
                // label with the strength descriptor, so the custom text is written after it.
                _voiceEnhanceCheck.Checked = dsp.Enabled;
                _voiceStrengthLabel.Text =
                    $"Custom (from phone) — HPF {dsp.HighPassHz} Hz · gate {dsp.Gate * 100:F0}% · " +
                    $"comp {dsp.Compressor * 100:F0}% · presence {dsp.PresenceDb:F1} dB";
            });
        };

        _engine.Fault += (_, message) =>
        {
            var runId = _runId;
            PostUi(() => _ = HandleReceiverFaultAsync(message, runId));
        };

        // Deliberately not gated on the engine still running: the last window of a session is
        // flushed during the stop, and that final snapshot is the one worth looking at.
        _engine.AnalyticsWindowClosed += (_, window) =>
        {
            var runId = _runId;
            PostUi(() =>
            {
                if (runId != _runId) return;
                _analyticsPanel.PushWindow(window);
            });
        };

        _engine.LinkQualityChanged += (_, assessment) =>
        {
            var runId = _runId;
            var text = $"Link: {assessment.Tier} — {assessment.Action} — {assessment.Prebuffer}ms buffer, {assessment.ConcealmentPackets} pkt concealment";
            PostUi(() =>
            {
                if (runId != _runId) return;
                _linkQualityLabel.Text = text;
                _linkQualityLabel.ForeColor = assessment.Tier switch
                {
                    LinkQualityPolicy.LinkTier.Excellent => System.Drawing.Color.DarkGreen,
                    LinkQualityPolicy.LinkTier.Good => Gold,
                    LinkQualityPolicy.LinkTier.Degraded => System.Drawing.Color.DarkOrange,
                    LinkQualityPolicy.LinkTier.Poor => System.Drawing.Color.Firebrick,
                    _ => SystemColors.ControlText,
                };
            });
        };

        // Raised from Start, on this thread, so the combo can be read directly. Persisting only
        // once the device has opened keeps a monitor that never worked out of the settings file.
        _engine.MonitorStarted += (_, _) =>
        {
            _settings.MonitorDevice = (_monitorCombo.SelectedItem as DeviceItem)?.ToString() ?? string.Empty;
            _settings.Save();
        };
    }

    private static Color StatusColor(EngineStatus status) => status switch
    {
        // Listening and buffering are "active but waiting": gold, the brand accent, not yet
        // green. Receiving is the live state, and stays green.
        EngineStatus.Listening or EngineStatus.Buffering => Gold,
        EngineStatus.Receiving => Color.DarkGreen,
        EngineStatus.ControlChannelUnavailable or EngineStatus.DatagramRejected => Color.DarkOrange,

        // A paused phone is amber and a lost one is red: the first is waited out, the second
        // means going and looking at the phone.
        EngineStatus.PhonePaused => Color.DarkOrange,
        EngineStatus.PhoneLost => Color.Firebrick,
        _ => SystemColors.ControlText,
    };

    /// <summary>
    /// The buffer is the direct latency/robustness trade: more of it absorbs jitter spikes that
    /// would otherwise become dropouts, at the cost of delay. The measured worst-case gap on
    /// this network is around 250 ms, so anything below that will still drop out occasionally.
    /// </summary>
    private void ApplyBufferSetting()
    {
        _engine.PrebufferMilliseconds = _bufferSlider.Value;

        var prebuffer = _engine.PrebufferMilliseconds;
        var descriptor = prebuffer switch
        {
            < 70 => "lowest latency, expect dropouts on Wi-Fi",
            < 120 => "balanced",
            < 200 => "stable",
            _ => "most robust, noticeably delayed",
        };
        _bufferLabel.Text = $"Buffer {prebuffer} ms — {descriptor}   (trim above {_engine.HighWaterMilliseconds} ms)";

        _settings.PrebufferMilliseconds = prebuffer;
        _settings.Save();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Closing the window while a stream is live should not silently kill the microphone
        // feed the user is relying on, so X hides to the tray unless Exit was chosen.
        if (!_exitRequested &&
            _minimizeToTrayCheck.Checked &&
            e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        _trayIcon.Visible = false;
        base.OnFormClosing(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }

    /// <summary>
    /// Hides everything that auto-discovery, auto-selection or a measured default already
    /// handles. What remains in Basic is the status, where audio is going, and the two buttons
    /// that actually change something.
    /// </summary>
    private void ApplyAdvancedMode()
    {
        var advanced = _advancedCheck.Checked;
        SuspendLayout();
        foreach (var control in _advancedControls)
        {
            control.Visible = advanced;
        }
        ResumeLayout(true);

        _settings.AdvancedMode = advanced;
        _settings.Save();

        // Basic mode has far less to show, so give the window a height that suits it rather
        // than leaving a large empty area behind.
        MinimumSize = new Size(860, advanced ? 540 : 380);
        if (!advanced && Height > 560) Height = 480;
    }

    private void ApplyVoiceSetting()
    {
        _engine.VoiceEnabled = _voiceEnhanceCheck.Checked;
        _engine.VoiceStrength = _voiceStrength.Value / 100f;
        _voiceStrength.Enabled = _voiceEnhanceCheck.Checked;

        var descriptor = _voiceStrength.Value switch
        {
            < 25 => "light touch",
            < 55 => "moderate",
            < 80 => "broadcast",
            _ => "aggressive — may sound processed",
        };
        _voiceStrengthLabel.Text = _voiceEnhanceCheck.Checked
            ? $"Strength {_voiceStrength.Value}% — {descriptor}"
            : "Voice enhancement off — raw microphone audio";

        _settings.VoiceEnhance = _voiceEnhanceCheck.Checked;
        _settings.VoiceStrength = _voiceStrength.Value;
        _settings.Save();
    }

    private void SetUpTrayIcon()
    {
        _trayIcon.Icon = SystemIcons.Application;
        _trayIcon.Text = "PocketMic Receiver";

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show PocketMic", null, (_, _) => RestoreFromTray());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) =>
        {
            _exitRequested = true;
            Close();
        });
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();

        _minimizeToTrayCheck.CheckedChanged += (_, _) =>
        {
            _settings.MinimizeToTray = _minimizeToTrayCheck.Checked;
            _settings.Save();
        };
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        ShowInTaskbar = true;
        _trayIcon.Visible = false;
        Activate();
    }

    private void HideToTray()
    {
        _trayIcon.Visible = true;
        ShowInTaskbar = false;
        Hide();
        _trayIcon.ShowBalloonTip(
            2000,
            "PocketMic Receiver",
            _engine.IsRunning ? "Still listening for your phone." : "Running in the tray.",
            ToolTipIcon.Info);
    }

    private void PersistSettings(int port, string pairingKey)
    {
        _settings.Port = port;
        _settings.PairingKey = pairingKey;
        _settings.AutoListen = _autoListenCheck.Checked;
        _settings.OutputDevice = (_outputCombo.SelectedItem as DeviceItem)?.ToString() ?? string.Empty;
        _settings.Save();
    }

    private void PopulateOutputs()
    {
        _outputCombo.Items.Clear();
        _monitorCombo.Items.Clear();
        _outputCombo.Items.Add(new DeviceItem(-1, "Windows default output"));
        _monitorCombo.Items.Add(new DeviceItem(-1, "Windows default output"));
        try
        {
            for (var index = 0; index < WaveOut.DeviceCount; index++)
            {
                try
                {
                    var capabilities = WaveOut.GetCapabilities(index);
                    _outputCombo.Items.Add(new DeviceItem(index, capabilities.ProductName));
                    _monitorCombo.Items.Add(new DeviceItem(index, capabilities.ProductName));
                }
                catch (MmException)
                {
                    // A stale or disconnected endpoint should not prevent the app from opening.
                }
            }
        }
        catch (MmException)
        {
            // Keep the default entry; Start will surface a useful error if no output is usable.
        }
        _outputCombo.SelectedIndex = 0;
        _monitorCombo.SelectedIndex = 0;
    }

    /// <summary>
    /// Picks a monitor device that is a real speaker, never a virtual cable — monitoring into
    /// the same cable the stream is routed to would feed it back on itself and be inaudible
    /// anyway.
    /// </summary>
    private void AutoSelectMonitorDevice()
    {
        if (!string.IsNullOrEmpty(_settings.MonitorDevice))
        {
            foreach (var item in _monitorCombo.Items)
            {
                if (item is DeviceItem saved && saved.ToString() == _settings.MonitorDevice)
                {
                    _monitorCombo.SelectedItem = item;
                    return;
                }
            }
        }

        foreach (var item in _monitorCombo.Items)
        {
            if (item is not DeviceItem device || device.DeviceNumber < 0) continue;
            var name = device.ToString();
            var isCable = VirtualCableHints.Any(hint => name.Contains(hint, StringComparison.OrdinalIgnoreCase));
            if (!isCable)
            {
                _monitorCombo.SelectedItem = item;
                return;
            }
        }

        _monitorCombo.SelectedIndex = 0;
    }

    private void RefreshLocalAddresses()
    {
        var addresses = GetLocalIpv4Addresses();
        _addressesText.Text = addresses.Count > 0
            ? string.Join(Environment.NewLine, addresses)
            : "No active private IPv4 address was found.";
    }

    private async Task ToggleReceiverAsync()
    {
        if (_stoppingTask is not null)
        {
            await _stoppingTask;
            return;
        }

        _startButton.Enabled = false;
        try
        {
            if (!_engine.IsRunning)
            {
                await StartReceiverAsync();
            }
            else
            {
                await StopReceiverAsync();
            }
        }
        finally
        {
            if (!IsDisposed && !Disposing && !_closing)
            {
                _startButton.Enabled = true;
            }
        }
    }

    private async Task StartReceiverAsync()
    {
        if (_stoppingTask is not null)
        {
            await _stoppingTask;
        }
        if (_engine.IsRunning || _closing) return;

        if (!int.TryParse(_portText.Text, out var port) ||
            port is < ControlProtocol.MinAudioPort or > ControlProtocol.MaxAudioPort)
        {
            MessageBox.Show(
                this,
                $"Enter a valid UDP audio port ({ControlProtocol.MinAudioPort}-{ControlProtocol.MaxAudioPort}).",
                "PocketMic",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var pairingKey = _keyText.Text;
        if (pairingKey.Length < 8)
        {
            MessageBox.Show(this, "The pairing key must be at least 8 characters and must exactly match the phone.", "PocketMic", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            RefreshLocalAddresses();
            var selected = _outputCombo.SelectedItem as DeviceItem ?? new DeviceItem(-1, "Default");
            var monitor = _monitorCombo.SelectedItem as DeviceItem;

            // Bumped before the engine starts so any event it raises during Start is already
            // attributed to this run.
            _runId++;
            _analyticsPanel.ResetHistory();
            _engine.Start(new EngineOptions(
                Port: port,
                PairingKey: pairingKey,
                OutputDeviceNumber: selected.DeviceNumber,
                MonitorDeviceNumber: monitor?.DeviceNumber ?? -1,
                MonitorEnabled: _monitorCheck.Checked && monitor is not null,
                FrontEnd: ReceiverFrontEnd.Classic,
                BuildVersion: ReceiverBuild.VersionOf(typeof(MainForm).Assembly)));

            _portText.Enabled = false;
            _keyText.Enabled = false;
            _outputCombo.Enabled = false;
            _startButton.Text = "Stop receiver";
            _sourceLabel.Text = "Phone: waiting…";
            _statsLabel.Text = FormatStats(_engine.Stats);

            PersistSettings(port, pairingKey);
        }
        catch (Exception exception)
        {
            await StopReceiverAsync();
            MessageBox.Show(this, exception.Message, "Could not start PocketMic", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task StopReceiverAsync()
    {
        if (_stoppingTask is not null)
        {
            await _stoppingTask;
            return;
        }

        var task = StopReceiverCoreAsync();
        _stoppingTask = task;
        try
        {
            await task;
        }
        finally
        {
            if (ReferenceEquals(_stoppingTask, task)) _stoppingTask = null;
        }
    }

    private async Task StopReceiverCoreAsync()
    {
        await _engine.StopAsync();

        // Written here rather than through the engine's Stopped event: by the time that is
        // raised the engine is no longer running, which is exactly what the posted handlers
        // treat as "belongs to a dead run".
        if (!IsDisposed)
        {
            _portText.Enabled = true;
            _keyText.Enabled = true;
            _outputCombo.Enabled = true;
            _startButton.Text = "Start receiver";
            _statusLabel.Text = "Stopped";
            _statusLabel.ForeColor = SystemColors.ControlText;
            _sourceLabel.Text = "Phone: —";
            _statsLabel.Text = FormatStats(_engine.Stats);
        }
    }

    private async Task HandleReceiverFaultAsync(string message, long runId)
    {
        if (_handlingFault || _closing || runId != _runId || !_engine.IsRunning) return;
        _handlingFault = true;
        _startButton.Enabled = false;
        try
        {
            await StopReceiverAsync();
            if (!IsDisposed && !Disposing && !_closing && runId == _runId)
            {
                MessageBox.Show(this, message, "PocketMic receiver stopped", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        finally
        {
            _handlingFault = false;
            if (!IsDisposed && !Disposing && !_closing)
            {
                _startButton.Enabled = true;
            }
        }
    }

    /// <summary>
    /// Writes the running (or just-finished) session log out as a Markdown report. This is the
    /// artefact you attach to a bug report — "it crackles sometimes" against measured loss,
    /// jitter percentiles and trim counts — which is why it is a deliberate export rather than
    /// something written on every stop.
    /// </summary>
    private void ExportSessionReport()
    {
        var sessionPath = _engine.AnalyticsSessionPath;
        if (sessionPath is null || !File.Exists(sessionPath))
        {
            MessageBox.Show(
                this,
                "No session has been recorded yet. Start the receiver, let it run for a while, then export.",
                "PocketMic",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Title = "Export session report",
            Filter = "Markdown (*.md)|*.md",
            FileName = Path.ChangeExtension(Path.GetFileName(sessionPath), ".md"),
            InitialDirectory = Path.GetDirectoryName(sessionPath) ?? string.Empty,
        };

        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            SessionReportWriter.WriteMarkdown(sessionPath, dialog.FileName);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(
                this,
                exception.Message,
                "Could not write the session report",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    /// <summary>
    /// Generates a QR code encoding the receiver's IP, port, and pairing key in the format
    /// pmic://&lt;ip&gt;:&lt;port&gt;/&lt;key&gt; and shows it in a modal dialog. The PocketMic
    /// Android app scans this to fill all connection fields automatically.
    /// </summary>
    private void ShowQrCode()
    {
        var first = GetLocalIpv4Addresses().FirstOrDefault();
        if (first is null)
        {
            MessageBox.Show(this, "No active IPv4 address was found.", "PocketMic",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!int.TryParse(_portText.Text, out var port) || port is < 1 or > 65534)
        {
            MessageBox.Show(this, "Enter a valid UDP port first.", "PocketMic",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var key = _keyText.Text;
        if (key.Length < 8)
        {
            MessageBox.Show(this, "Enter a pairing key (at least 8 characters) first.", "PocketMic",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var uri = $"pmic://{first}:{port}/{key}";

        using var qrGen = new QRCodeGenerator();
        var qrData = qrGen.CreateQrCode(uri, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new QRCode(qrData);
        using var bitmap = qrCode.GetGraphic(20);

        var form = new Form
        {
            Text = "PocketMic — Scan with phone",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(bitmap.Width + 40, bitmap.Height + 80),
        };

        var pictureBox = new PictureBox
        {
            Image = bitmap,
            SizeMode = PictureBoxSizeMode.CenterImage,
            Dock = DockStyle.Fill,
        };
        form.Controls.Add(pictureBox);

        var label = new Label
        {
            Text = $"Scan this QR code with PocketMic on your phone\n{uri}",
            Dock = DockStyle.Bottom,
            Height = 50,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9F),
        };
        form.Controls.Add(label);

        form.ShowDialog(this);
    }

    private static string FormatStats(EngineStats stats) =>
        $"Packets: {stats.Packets:N0}   Lost: {stats.Lost:N0}   Late: {stats.Late:N0}   " +
        $"Rejected: {stats.Rejected:N0}   Trimmed: {stats.Trimmed:N0}   Buffer: {stats.BufferedMilliseconds:0} ms";

    private void PostUi(Action action)
    {
        if (IsDisposed || Disposing || !IsHandleCreated) return;

        try
        {
            BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // The window closed or was disposed between the handle check and BeginInvoke.
        }
    }

    private static IReadOnlyList<IPAddress> GetLocalIpv4Addresses()
    {
        static int InterfacePriority(NetworkInterfaceType type) => type switch
        {
            NetworkInterfaceType.Wireless80211 => 0,
            NetworkInterfaceType.Ethernet => 1,
            NetworkInterfaceType.GigabitEthernet => 1,
            _ => 2,
        };

        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(network => network.OperationalStatus == OperationalStatus.Up &&
                                  network.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel)
                .OrderBy(network => InterfacePriority(network.NetworkInterfaceType))
                .SelectMany(network => network.GetIPProperties().UnicastAddresses)
                .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork &&
                                  !IPAddress.IsLoopback(address.Address))
                .Select(address => address.Address)
                .Distinct()
                .ToArray();
        }
        catch (NetworkInformationException)
        {
            return Array.Empty<IPAddress>();
        }
    }

    private async void HandleFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (_closing) return;

        eventArgs.Cancel = true;
        _closing = true;
        _startButton.Enabled = false;
        try
        {
            await StopReceiverAsync();
        }
        finally
        {
            Close();
        }
    }

    private sealed record DeviceItem(int DeviceNumber, string Name)
    {
        public override string ToString() => Name;
    }
}
