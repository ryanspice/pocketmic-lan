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
    private static int _showWindow;
    internal static bool ShowWindow => Volatile.Read(ref _showWindow) != 0;

    [STAThread]
    private static void Main(string[] args)
    {
        _showWindow = args.Contains("--show", StringComparer.OrdinalIgnoreCase) ? 1 : 0;
        if (args.Contains("--exit-for-update", StringComparer.OrdinalIgnoreCase))
        {
            var accepted = ReceiverActivation.RequestExitAsync(installationDirectory: AppContext.BaseDirectory).GetAwaiter().GetResult();
            Environment.ExitCode = accepted ? 0 : 3;
            return;
        }
        ApplicationConfiguration.Initialize();
        if (ModernReceiverLauncher.TryLaunch(args)) return;

        MainForm? window = null;
        var activation = ReceiverActivation.StartOrActivateAsync(() =>
        {
            Volatile.Write(ref _showWindow, 1);
            var current = Volatile.Read(ref window);
            if (current is { IsHandleCreated: true, IsDisposed: false })
            {
                try { current.BeginInvoke(new Action(current.RestoreFromTray)); }
                catch (InvalidOperationException) { /* The UI is closing. */ }
            }
        }, requestExit: () =>
        {
            var current = Volatile.Read(ref window);
            if (current is { IsHandleCreated: true, IsDisposed: false })
            {
                try { current.BeginInvoke(new Action(current.RequestExit)); }
                catch (InvalidOperationException) { /* The UI is already closing. */ }
            }
        }).GetAwaiter().GetResult();
        using var lifetime = activation.Instance;
        if (activation.Status != ReceiverActivationStatus.Primary)
        {
            if (activation.Status == ReceiverActivationStatus.ExistingUnresponsive)
                MessageBox.Show("PocketMic is already running but is not responding yet. Try opening it again shortly.",
                    "PocketMic", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new MainForm();
        Volatile.Write(ref window, form);
        Application.Run(form);
        Volatile.Write(ref window, null);
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
internal sealed class MainForm : MicaForm
{
    private const int DefaultPrebufferMilliseconds = AudioPipeline.DefaultPrebufferMilliseconds;
    private ReceiverTheme _theme = ReceiverTheme.FromSystem();

    private readonly PocketMicEngine _engine = new();
    private readonly CaptureDefaultRouting _captureRouting = DefaultDeviceSwitcher.CreateRoutingController();

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

    private readonly CheckBox _automaticBufferingCheck = new()
    {
        Text = "Automatic buffer (slider is the minimum)",
        AutoSize = true,
        Checked = true,
    };
    private readonly Label _bufferLabel = new() { AutoSize = true };
    private readonly Label _effectiveBufferLabel = new() { AutoSize = true };
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

    private CaptureDefaultRoute? _activeCaptureRoute;
    private string? _activeCaptureRouteName;

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
    private readonly Button _startButton = new() { Text = "Start receiver", AutoSize = true, Tag = ThemeRole.Primary };
    private readonly Button _copyIpButton = new() { Text = "Copy selected PC address", AutoSize = true };
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
    private readonly ComboBox _addressChoice = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
    private PairingAddress[] _pairingChoices = Array.Empty<PairingAddress>();
    private string? _selectedPairingAddress;

    private readonly AnalyticsPanel _analyticsPanel = new()
    {
        Width = 780,
        Height = 240,
        Margin = new Padding(0, 0, 0, 8),
    };

    private readonly Button _exportReportButton = new() { Text = "Export session report", AutoSize = true };

    private Task? _stoppingTask;
    private bool _handlingFault;
    private bool _restoringMonitorSelection;
    private bool _applyingVoiceControls;
    private bool _reflectingPhoneDsp;
    private bool _closing;
    private long _runId;

    public MainForm()
    {
        Text = "PocketMic Receiver";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        MinimumSize = new Size(860, 540);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Segoe UI", 10F);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = SystemColors.Control,
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
            Tag = ThemeRole.Accent,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
        };
        root.Controls.Add(title);

        root.Controls.Add(new Label
        {
            Text = "Receives encrypted microphone audio from the Android app over your local network.",
            AutoSize = true,
            Tag = ThemeRole.Muted,
            Margin = new Padding(0, 0, 0, 18),
        });

        _advancedCheck.CheckedChanged += (_, _) => ApplyAdvancedMode();

        var addressSection = SectionLabel("PC address to enter on the phone");
        root.Controls.Add(addressSection);
        root.Controls.Add(_addressChoice);
        _addressChoice.DisplayMember = nameof(PairingAddress.Label);
        _addressChoice.SelectedIndexChanged += (_, _) =>
            _selectedPairingAddress = (_addressChoice.SelectedItem as PairingAddress)?.Address;
        RefreshLocalAddresses();
        var pairingActions = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            WrapContents = true,
            Margin = new Padding(0, 6, 0, 18),
        };
        pairingActions.Controls.Add(_copyIpButton);
        pairingActions.Controls.Add(_showQrButton);
        root.Controls.Add(pairingActions);
        _copyIpButton.Margin = new Padding(0, 0, 8, 0);
        _showQrButton.Margin = Padding.Empty;
        _copyIpButton.Click += (_, _) =>
        {
            RefreshLocalAddresses();
            var first = _selectedPairingAddress;
            if (first is not null)
            {
                try
                {
                    Clipboard.SetText(first.ToString());
                    _copyIpButton.Text = "Copied PC address";
                }
                catch (ExternalException)
                {
                    MessageBox.Show(this, "The clipboard is busy. Try copying the IP again.",
                        "Could not copy IP", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
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
        _outputCombo.SelectedIndexChanged += (_, _) => DescribeRouting(_outputCombo.SelectedItem as DeviceItem);
        root.Controls.Add(_autoListenCheck);
        _advancedControls.Add(_autoListenCheck);

        root.Controls.Add(_advancedCheck);
        var bufferSection = SectionLabel("Jitter buffer");
        root.Controls.Add(bufferSection);
        root.Controls.Add(_automaticBufferingCheck);
        root.Controls.Add(_bufferLabel);
        root.Controls.Add(_bufferSlider);
        root.Controls.Add(_effectiveBufferLabel);
        _bufferSlider.ValueChanged += (_, _) => ApplyBufferSetting();
        _automaticBufferingCheck.CheckedChanged += (_, _) => ApplyBufferSetting();
        _advancedControls.AddRange(new Control[] { bufferSection, _automaticBufferingCheck, _bufferLabel, _bufferSlider, _effectiveBufferLabel });

        var voiceSection = SectionLabel("Voice processing");
        root.Controls.Add(voiceSection);
        root.Controls.Add(_voiceEnhanceCheck);
        root.Controls.Add(_voiceStrengthLabel);
        root.Controls.Add(_voiceStrength);
        _advancedControls.AddRange(new Control[] { voiceSection, _voiceEnhanceCheck, _voiceStrengthLabel, _voiceStrength });
        _voiceEnhanceCheck.CheckedChanged += (_, _) =>
        {
            if (!_applyingVoiceControls && !_reflectingPhoneDsp) ApplyVoiceSetting();
        };
        _voiceStrength.ValueChanged += (_, _) =>
        {
            if (!_applyingVoiceControls && !_reflectingPhoneDsp)
                ApplyVoiceSetting(selectDesktopPreset: true);
        };

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
            Tag = ThemeRole.Surface,
        };
        _statusLabel.Font = new Font(Font.FontFamily, 11F, FontStyle.Bold);
        _statusLabel.Tag = ThemeRole.Accent;
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
            Tag = ThemeRole.Muted,
            Margin = new Padding(0, 12, 0, 0),
        });

        // Applied last: the diagnostics controls are registered after ApplySettings has already
        // run, and every advanced control has to exist in the list before visibility is decided
        // or it would stay on screen in Basic mode until the checkbox was next toggled.
        ApplyAdvancedMode();

        SystemAppearanceChanged += (_, _) => ApplyContentTheme();
        ApplyContentTheme();

        FormClosing += HandleFormClosing;
    }

    private void ApplyContentTheme()
    {
        _theme = ReceiverTheme.FromSystem();
        SuspendLayout();
        try { _theme.Apply(this); }
        finally { ResumeLayout(true); }
    }

    private void SetTone(Label label, ThemeRole role)
    {
        label.Tag = role;
        label.ForeColor = _theme.Foreground(role);
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
        if (_activeCaptureRoute is not null)
        {
            _routingLabel.Text = $"Windows default microphone remains \"{_activeCaptureRouteName ?? "the previously selected virtual endpoint"}\". If playback now goes somewhere else, destination apps will not receive PocketMic audio. Restore before routing a different cable.";
            SetTone(_routingLabel, ThemeRole.Success);
            _makeDefaultMicButton.Enabled = true;
            _makeDefaultMicButton.Text = "Restore previous Windows microphone";
            return;
        }

        var renderName = device is null
            ? null
            : device.DeviceNumber < 0 ? DefaultDeviceSwitcher.DefaultRenderFriendlyName() : device.Name;
        if (!string.IsNullOrWhiteSpace(renderName))
        {
            var resolution = DefaultDeviceSwitcher.ResolveCaptureForRenderName(renderName);
            if (resolution.Endpoint is { } endpoint)
            {
                _routingLabel.Text = $"\"{renderName}\" maps to the matching microphone endpoint \"{endpoint.FriendlyName}\". You can route Windows defaults to that microphone.";
                SetTone(_routingLabel, ThemeRole.Success);
                _makeDefaultMicButton.Enabled = true;
                _makeDefaultMicButton.Text = "Use PocketMic as Windows microphone";
                return;
            }

            var expected = CaptureEndpointMatcher.ExpectedCaptureName(renderName);
            _routingLabel.Text = expected is null
                ? $"Playing to \"{renderName}\". PocketMic cannot confirm a matching microphone endpoint for this output; select the microphone manually in the destination app."
                : resolution.Matches.Count > 1
                    ? $"More than one microphone endpoint is named \"{expected}\". Automatic routing is disabled; choose the correct microphone manually."
                    : $"The matching microphone endpoint \"{expected}\" is not active. Automatic routing is disabled; select a microphone manually in Windows Sound settings.";
            SetTone(_routingLabel, ThemeRole.Warning);
            _makeDefaultMicButton.Enabled = false;
            _makeDefaultMicButton.Text = "Use PocketMic as Windows microphone";
            return;
        }

        _routingLabel.Text = "No active playback output could be identified. Select a microphone manually in the destination app.";
        SetTone(_routingLabel, ThemeRole.Warning);
        _makeDefaultMicButton.Enabled = false;
        _makeDefaultMicButton.Text = "Use PocketMic as Windows microphone";
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
                    DescribeRouting(device);
                    return;
                }
            }
        }

        DescribeRouting(_outputCombo.SelectedItem as DeviceItem);
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
        if (_activeCaptureRoute is { } activeRoute)
        {
            if (_captureRouting.TryRestore(activeRoute, onlyIfStillTarget: false, out var restoreError))
            {
                _activeCaptureRoute = null;
                _activeCaptureRouteName = null;
                _makeDefaultMicButton.Text = "Use PocketMic as Windows microphone";
                DescribeRouting(_outputCombo.SelectedItem as DeviceItem);
                _routingLabel.Text = "The previous Windows microphone assignment has been restored for each role.";
                SetTone(_routingLabel, ThemeRole.Muted);
            }
            else
            {
                MessageBox.Show(this, restoreError, "Could not restore Windows microphone", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return;
        }

        var selected = _outputCombo.SelectedItem as DeviceItem;
        var renderName = selected is null
            ? null
            : selected.DeviceNumber < 0 ? DefaultDeviceSwitcher.DefaultRenderFriendlyName() : selected.Name;
        var resolution = string.IsNullOrWhiteSpace(renderName)
            ? new CaptureEndpointResolution(string.Empty, Array.Empty<CaptureEndpointInfo>())
            : DefaultDeviceSwitcher.ResolveCaptureForRenderName(renderName);
        if (resolution.Endpoint is not { } endpoint)
        {
            DescribeRouting(selected);
            MessageBox.Show(this, _routingLabel.Text, "No unambiguous matching microphone", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_captureRouting.TryRouteTo(endpoint.Id, out var route, out var error) && route is not null)
        {
            _activeCaptureRoute = route;
            _activeCaptureRouteName = endpoint.FriendlyName;
            _routingLabel.Text = $"\"{endpoint.FriendlyName}\" is now the Windows default microphone for all three roles. Apps using System Default can receive PocketMic audio.";
            SetTone(_routingLabel, ThemeRole.Success);
            _makeDefaultMicButton.Text = "Restore previous Windows microphone";
            _makeDefaultMicButton.Enabled = true;
        }
        else
        {
            MessageBox.Show(
                this,
                $"Could not change the default recording device safely.\r\n\r\n{error}\r\n\r\n" +
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
        Tag = ThemeRole.Accent,
        AutoSize = true,
        Margin = new Padding(0, 0, 0, 6),
    };

    private void ApplySettings()
    {
        if (_settings.EnsurePairingKey()) _settings.Save();
        _portText.Text = _settings.Port.ToString();
        _keyText.Text = _settings.PairingKey;
        _autoListenCheck.Checked = _settings.AutoListen;
        var savedBuffer = Math.Clamp(_settings.PrebufferMilliseconds, _bufferSlider.Minimum, _bufferSlider.Maximum);
        var savedAutomaticBuffering = _settings.AutomaticBuffering;
        _automaticBufferingCheck.Checked = savedAutomaticBuffering;
        _bufferSlider.Value = savedBuffer;
        ApplyBufferSetting();
        _applyingVoiceControls = true;
        try
        {
            _voiceEnhanceCheck.Checked = _settings.VoiceEnhance;
            _voiceStrength.Value = Math.Clamp(_settings.VoiceStrength, _voiceStrength.Minimum, _voiceStrength.Maximum);
        }
        finally
        {
            _applyingVoiceControls = false;
        }
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
            var requested = _monitorCheck.Checked;
            // Apply live rather than only at the next start: the speakers must go quiet the
            // moment the box is unticked, not whenever the receiver happens to restart.
            _engine.MonitorEnabled = requested;
            if (requested && !_engine.MonitorEnabled)
            {
                _restoringMonitorSelection = true;
                _monitorCheck.Checked = false;
                _restoringMonitorSelection = false;
                AutoSelectMonitorDevice();
                MessageBox.Show(this, _engine.LastMonitorError ?? "The monitor output could not be opened.", "Monitor output unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            _monitorCombo.Enabled = _monitorCheck.Checked;
            _settings.MonitorEnabled = _engine.MonitorEnabled;
            _settings.Save();
        };
        _monitorCombo.Enabled = _monitorCheck.Checked;
        _monitorCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_restoringMonitorSelection || !_engine.IsRunning) return;
            if (_monitorCombo.SelectedItem is not DeviceItem selected) return;

            if (!_engine.TrySetMonitorDevice(selected.DeviceNumber, out var error))
            {
                RestoreMonitorDeviceSelection(_engine.MonitorDeviceNumber);
                MessageBox.Show(this, error, "Monitor output unchanged", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Save only after a live monitor opened on this device. When monitoring is off,
            // leave the last working device saved until the next successful open.
            if (_monitorCheck.Checked && _engine.MonitorEnabled)
            {
                _settings.MonitorDevice = selected.ToString();
                _settings.Save();
            }
        };

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
            // Explicit desktop/Start-menu launches remain visible. A failed auto-start also
            // remains visible so the user can correct the error instead of losing the window.
            if (!Program.ShowWindow && _settings.StartMinimized &&
                (!_autoListenCheck.Checked || _engine.IsRunning))
            {
                HideToTray();
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
                SetTone(_statusLabel, StatusTone(update.Status));
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
                UpdateEffectiveBufferLabel();
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
                // The processor already applied the phone config. Reflecting its enabled state
                // must not be interpreted as a desktop preset override.
                _reflectingPhoneDsp = true;
                try { _voiceEnhanceCheck.Checked = dsp.Enabled; }
                finally { _reflectingPhoneDsp = false; }
                ApplyVoiceSetting();
                _voiceStrengthLabel.Text =
                    $"Custom (from phone; move Strength to use desktop preset) — HPF {dsp.HighPassHz} Hz · gate {dsp.Gate * 100:F0}% · " +
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
            var text = $"Link: {assessment.Tier} — {assessment.Action} — recommends {assessment.Prebuffer} ms, {assessment.ConcealmentPackets} pkt concealment";
            PostUi(() =>
            {
                if (runId != _runId) return;
                _linkQualityLabel.Text = text;
                SetTone(_linkQualityLabel, assessment.Tier switch
                {
                    LinkQualityPolicy.LinkTier.Excellent => ThemeRole.Success,
                    LinkQualityPolicy.LinkTier.Good => ThemeRole.Accent,
                    LinkQualityPolicy.LinkTier.Degraded => ThemeRole.Warning,
                    LinkQualityPolicy.LinkTier.Poor => ThemeRole.Error,
                    _ => ThemeRole.Default,
                });
            });
        };

        // Raised from Start, on this thread, so the combo can be read directly. Persisting only
        // once the device has opened keeps a monitor that never worked out of the settings file.
        _engine.MonitorStarted += (_, _) =>
        {
            _settings.MonitorDevice = (_monitorCombo.SelectedItem as DeviceItem)?.ToString() ?? string.Empty;
            _settings.Save();
        };

        _engine.MonitorFailed += (_, message) =>
        {
            var runId = _runId;
            PostUi(() =>
            {
                if (runId != _runId || !_engine.IsRunning) return;
                _restoringMonitorSelection = true;
                _monitorCheck.Checked = false;
                _restoringMonitorSelection = false;
                _monitorCombo.Enabled = false;
                _settings.MonitorEnabled = false;
                _settings.Save();
                AutoSelectMonitorDevice();
                MessageBox.Show(this, message, "Monitor output unavailable", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            });
        };
    }

    private static ThemeRole StatusTone(EngineStatus status) => status switch
    {
        // Listening and buffering are "active but waiting": gold, the brand accent, not yet
        // green. Receiving is the live state, and stays green.
        EngineStatus.Listening or EngineStatus.Buffering => ThemeRole.Accent,
        EngineStatus.Receiving => ThemeRole.Success,
        EngineStatus.ControlChannelUnavailable or EngineStatus.DatagramRejected => ThemeRole.Warning,

        // A paused phone is amber and a lost one is red: the first is waited out, the second
        // means going and looking at the phone.
        EngineStatus.PhonePaused => ThemeRole.Warning,
        EngineStatus.PhoneLost => ThemeRole.Error,
        _ => ThemeRole.Default,
    };

    /// <summary>
    /// In automatic mode the slider is the user's minimum target. In manual mode it is the exact
    /// playback prebuffer target. The effective target is displayed independently below.
    /// </summary>
    private void ApplyBufferSetting()
    {
        _engine.ConfigureBuffering(
            _automaticBufferingCheck.Checked,
            _bufferSlider.Value,
            _bufferSlider.Maximum);

        var selected = _bufferSlider.Value;
        var descriptor = selected switch
        {
            < 70 => "lower target",
            < 120 => "moderate target",
            < 200 => "higher target",
            _ => "high target",
        };
        _bufferLabel.Text = _automaticBufferingCheck.Checked
            ? $"Minimum buffer {selected} ms — {descriptor}"
            : $"Manual buffer {selected} ms — {descriptor}";
        UpdateEffectiveBufferLabel();

        _settings.PrebufferMilliseconds = selected;
        _settings.AutomaticBuffering = _automaticBufferingCheck.Checked;
        _settings.Save();
    }

    private void UpdateEffectiveBufferLabel() =>
        _effectiveBufferLabel.Text = $"Effective playback prebuffer: {_engine.PrebufferMilliseconds} ms (trim above {_engine.HighWaterMilliseconds} ms)";

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        // Closing the window while a stream is live should not silently kill the microphone
        // feed the user is relying on, so X hides to the tray unless Exit was chosen.
        if (!_exitRequested && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }

        base.OnFormClosing(e);
        if (e.Cancel) return;

        _trayIcon.Visible = false;
        RestoreOwnedMicrophoneRouteOnExit();
    }

    private void RestoreOwnedMicrophoneRouteOnExit()
    {
        if (_activeCaptureRoute is not { } route) return;

        if (_captureRouting.TryRestore(route, onlyIfStillTarget: true, out var error))
        {
            _activeCaptureRoute = null;
            _activeCaptureRouteName = null;
            return;
        }

        MessageBox.Show(
            this,
            $"PocketMic could not fully restore the Windows microphone assignments before exiting.\r\n\r\n{error}",
            "Check Windows microphone settings",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
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

    private void ApplyVoiceSetting(bool selectDesktopPreset = false)
    {
        _engine.VoiceEnabled = _voiceEnhanceCheck.Checked;
        if (selectDesktopPreset)
            _engine.SelectVoicePreset(_voiceStrength.Value / 100f);
        else
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
        _trayIcon.Icon = Icon;
        _trayIcon.Text = "PocketMic Receiver";

        var menu = new ContextMenuStrip();
        menu.Items.Add("Show PocketMic", null, (_, _) => RestoreFromTray());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => RequestExit());
        _trayIcon.ContextMenuStrip = menu;
        _trayIcon.DoubleClick += (_, _) => RestoreFromTray();
        _trayIcon.Visible = true;

    }

    internal void RequestExit()
    {
        _exitRequested = true;
        Close();
    }

    internal void RestoreFromTray()
    {
        Show();
        WindowState = FormWindowState.Normal;
        ShowInTaskbar = true;
        _trayIcon.Visible = true;
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

    private void RestoreMonitorDeviceSelection(int? deviceNumber)
    {
        _restoringMonitorSelection = true;
        try
        {
            if (deviceNumber is { } number)
            {
                foreach (var item in _monitorCombo.Items)
                {
                    if (item is DeviceItem device && device.DeviceNumber == number)
                    {
                        _monitorCombo.SelectedItem = item;
                        return;
                    }
                }
            }

            AutoSelectMonitorDevice();
        }
        finally
        {
            _restoringMonitorSelection = false;
        }
    }

    private void RefreshLocalAddresses()
    {
        var previous = _selectedPairingAddress;
        _pairingChoices = PairingAddressRanker.LocalChoices().ToArray();
        _addressChoice.DataSource = _pairingChoices.ToList();
        _addressChoice.DisplayMember = nameof(PairingAddress.Label);
        _addressChoice.SelectedItem = _pairingChoices.FirstOrDefault(item => item.Address == previous) ?? _pairingChoices.FirstOrDefault();
        _selectedPairingAddress = (_addressChoice.SelectedItem as PairingAddress)?.Address;
        _copyIpButton.Enabled = _selectedPairingAddress is not null;
        _copyIpButton.Text = "Copy selected PC address";
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
            SetTone(_statusLabel, ThemeRole.Default);
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
        RefreshLocalAddresses();
        var addresses = _pairingChoices;
        var first = _selectedPairingAddress;
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
        using var qrData = qrGen.CreateQrCode(uri, QRCodeGenerator.ECCLevel.M);
        using var qrCode = new QRCode(qrData);
        using var bitmap = qrCode.GetGraphic(8);

        using var form = new MicaForm
        {
            Text = "PocketMic — Scan with phone",
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(bitmap.Width + 64, bitmap.Height + 112),
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
            Text = $"Scan this QR code with PocketMic on your phone\n{first}:{port}",
            Dock = DockStyle.Bottom,
            Height = 50,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9F),
        };
        form.Controls.Add(label);

        // VPNs, VMs and multiple adapters can put the phone on a different subnet from
        // the first address. Let the user generate a QR for the LAN they are actually on.
        using var addressChoice = new ComboBox { Dock = DockStyle.Top,
            DropDownStyle = ComboBoxStyle.DropDownList };
        addressChoice.DisplayMember = nameof(PairingAddress.Label);
        addressChoice.Items.AddRange(addresses.Cast<object>().ToArray());
        addressChoice.SelectedItem = addresses.FirstOrDefault(item => item.Address == first) ?? addresses[0];
        Image? selectedImage = null;
        addressChoice.SelectedIndexChanged += (_, _) =>
        {
            if (addressChoice.SelectedItem is not PairingAddress selected) return;
            _selectedPairingAddress = selected.Address;
            _addressChoice.SelectedItem = _pairingChoices.FirstOrDefault(item => item.Address == selected.Address);
            var selectedUri = $"pmic://{selected.Address}:{port}/{key}";
            using var data = qrGen.CreateQrCode(selectedUri, QRCodeGenerator.ECCLevel.M);
            using var code = new QRCode(data);
            var nextImage = code.GetGraphic(8);
            pictureBox.Image = nextImage;
            selectedImage?.Dispose();
            selectedImage = nextImage;
            label.Text = $"Scan this QR code with PocketMic on your phone\n{selected.Address}:{port}";
        };
        form.Controls.Add(addressChoice);

        form.SystemAppearanceChanged += (_, _) => ReceiverTheme.FromSystem().Apply(form);
        ReceiverTheme.FromSystem().Apply(form);
        form.ShowDialog(this);
        pictureBox.Image = null;
        selectedImage?.Dispose();
    }

    private static string FormatStats(EngineStats stats) =>
        $"Packets: {stats.Packets:N0}   Lost: {stats.Lost:N0}   Late: {stats.Late:N0}   " +
        $"Rejected: {stats.Rejected:N0}   Trimmed: {stats.Trimmed:N0}   Queued audio: {stats.BufferedMilliseconds:0} ms";

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
