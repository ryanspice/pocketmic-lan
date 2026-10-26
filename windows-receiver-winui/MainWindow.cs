using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Shapes;
using NAudio.Wave;
using QRCoder;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;
using Path = System.IO.Path;

namespace PocketMicReceiver.WinUI;

/// <summary>WinUI presentation only; audio, protocol and reversible routing remain in Core.</summary>
public sealed class MainWindow : Window
{
    private readonly PocketMicEngine _engine = new();
    private readonly ReceiverSettings _settings = ReceiverSettings.Load();
    private readonly CaptureDefaultRouting _routing = DefaultDeviceSwitcher.CreateRoutingController();
    private CaptureDefaultRoute? _ownedRoute;
    private readonly Grid _root = new();
    private readonly StackPanel _advanced = new() { Spacing = 16 };
    private readonly ComboBox _output = new() { Header = "Playback output", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ComboBox _monitorOutput = new() { Header = "Monitor through", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ToggleSwitch _monitor = new() { Header = "Also listen through speakers" };
    private readonly ToggleSwitch _advancedToggle = new() { Header = "Advanced settings" };
    private readonly CheckBox _autoListen = new() { Content = "Start listening automatically" };
    private readonly TextBox _port = new() { Header = "UDP audio port", MaxLength = 5 };
    private readonly PasswordBox _key = new() { Header = "Pairing key", PasswordRevealMode = PasswordRevealMode.Peek };
    private readonly ComboBox _addresses = new() { Header = "PC address (recommended address is labelled)", HorizontalAlignment = HorizontalAlignment.Stretch };
    private PairingAddress[] _addressChoices = Array.Empty<PairingAddress>();
    private string? _selectedAddress;
    private readonly CheckBox _automaticBuffer = new() { Content = "Automatic buffer (slider is the minimum)" };
    private readonly Slider _buffer = new() { Header = "Jitter buffer", Minimum = 40, Maximum = 300, StepFrequency = 10, TickFrequency = 20 };
    private readonly TextBlock _effectiveBuffer = Text("", caption: true);
    private readonly ToggleSwitch _voice = new() { Header = "Voice enhancement" };
    private readonly Slider _strength = new() { Header = "Desktop strength", Minimum = 0, Maximum = 100, StepFrequency = 1 };
    private readonly TextBlock _voiceDetails = Text("", caption: true);
    private readonly Button _start = new() { Content = "Start receiver", HorizontalAlignment = HorizontalAlignment.Left };
    private readonly Button _route = new() { Content = "Use PocketMic as Windows microphone" };
    private readonly TextBlock _routeDescription = Text("", caption: true);
    private readonly TextBlock _status = Text("Stopped");
    private readonly TextBlock _source = Text("Phone: waiting…", caption: true);
    private readonly TextBlock _stats = Text("No packets received yet", caption: true);
    private readonly TextBlock _link = Text("Link: waiting…", caption: true);
    private readonly TextBlock _analyticsSummary = Text("Start a session to collect diagnostics.", caption: true);
    private readonly InfoBar _notice = new() { IsClosable = true, IsOpen = false };
    private readonly List<WindowStats> _history = new();
    private readonly List<MetricChart> _charts = new();
    private readonly Forms.NotifyIcon _tray = new();
    private readonly AppWindow _appWindow;
    private readonly IntPtr _hwnd;
    private Drawing.Icon? _icon;
    private bool _loading = true;
    private bool _reflecting;
    private bool _busy;
    private bool _exiting;
    private bool _closed;
    private bool _showRequested;
    private long _run;
    private Task? _stopTask;

    private sealed record Device(int Number, string Name)
    {
        public override string ToString() => Name;
    }

    private static readonly string[] CableNames = { "CABLE Input", "VoiceMeeter Input", "VoiceMeeter Aux Input", "Virtual Cable", "PocketMic" };

    public MainWindow()
    {
        Title = "PocketMic Receiver";
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        _appWindow = AppWindow.GetFromWindowId(Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hwnd));
        _appWindow.Resize(new SizeInt32(820, 700));
        BuildContent();
        PopulateDevices();
        RestoreSettings();
        SubscribeToEngine();
        CreateTray();
        _appWindow.Closing += (_, args) =>
        {
            if (_exiting) return;
            args.Cancel = true;
            _appWindow.Hide();
        };
        Closed += (_, _) =>
        {
            _closed = true;
            _tray.Visible = false;
            _tray.Dispose();
            _icon?.Dispose();
        };
        _loading = false;
        ConfigureBuffer();
        DescribeRoute();
    }

    private static TextBlock Text(string text, bool caption = false) => new()
    {
        Text = text, TextWrapping = TextWrapping.Wrap,
        Style = (Style)Application.Current.Resources[caption ? "CaptionTextBlockStyle" : "BodyTextBlockStyle"],
    };

    private static Border Card(UIElement child) => new()
    {
        Style = (Style)Application.Current.Resources["ReceiverCardStyle"], Child = child,
    };

    private void BuildContent()
    {
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition());
        var titleBar = new Grid { Height = 40, Padding = new Thickness(24, 0, 140, 0) };
        titleBar.Children.Add(new TextBlock { Text = "PocketMic", VerticalAlignment = VerticalAlignment.Center });
        _root.Children.Add(titleBar);
        SetTitleBar(titleBar);
        var body = new StackPanel { Spacing = 16, MaxWidth = 860, HorizontalAlignment = HorizontalAlignment.Stretch };
        body.Children.Add(new TextBlock { Text = "Your phone. Your microphone.", Style = (Style)Application.Current.Resources["TitleTextBlockStyle"], TextWrapping = TextWrapping.Wrap });
        body.Children.Add(Text("Encrypted microphone audio over your local network. PocketMic 0.1.7", caption: true));
        body.Children.Add(_notice);
        var status = new StackPanel { Spacing = 6 };
        _status.Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"];
        status.Children.Add(_status); status.Children.Add(_source); status.Children.Add(_stats); status.Children.Add(_link);
        body.Children.Add(Card(status));
        var outputs = new StackPanel { Spacing = 12 };
        outputs.Children.Add(_output); outputs.Children.Add(_routeDescription); outputs.Children.Add(_route);
        outputs.Children.Add(_monitor); outputs.Children.Add(_monitorOutput);
        body.Children.Add(Card(outputs));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        actions.SizeChanged += (_, args) => actions.Orientation = args.NewSize.Width < 420
            ? Orientation.Vertical : Orientation.Horizontal;
        _start.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
        var qr = new Button { Content = "Pair phone with QR" };
        qr.Click += async (_, _) => await ShowQrAsync();
        actions.Children.Add(_start); actions.Children.Add(qr);
        body.Children.Add(actions); body.Children.Add(_advancedToggle);
        var pairing = new StackPanel { Spacing = 12 };
        pairing.Children.Add(Text("Scan the PC code with PocketMic, or enter the same pairing key manually on both devices."));
        pairing.Children.Add(_addresses);
        _addresses.DisplayMemberPath = nameof(PairingAddress.Label);
        _addresses.SelectionChanged += (_, _) =>
        {
            if (_addresses.SelectedItem is PairingAddress selected) _selectedAddress = selected.Address;
        };
        var refresh = new Button { Content = "Refresh PC addresses" };
        refresh.Click += (_, _) => RefreshAddresses();
        pairing.Children.Add(refresh);
        var copy = new Button { Content = "Copy selected address" };
        copy.Click += (_, _) =>
        {
            var address = _addresses.SelectedItem as PairingAddress;
            if (address is null) { Notice("No LAN address", "Connect this PC to your private network."); return; }
            try
            {
                var data = new DataPackage(); data.SetText(address.Address); Clipboard.SetContent(data);
            }
            catch (Exception error) when (error is ExternalException or InvalidOperationException)
            { Notice("Could not copy address", "The clipboard is busy. Try again, or copy the address from the list."); }
        };
        pairing.Children.Add(copy); pairing.Children.Add(_port); pairing.Children.Add(_key); pairing.Children.Add(_autoListen);
        body.Children.Add(Card(pairing));
        var processing = new StackPanel { Spacing = 12 };
        processing.Children.Add(_automaticBuffer); processing.Children.Add(_buffer); processing.Children.Add(_effectiveBuffer);
        processing.Children.Add(_voice); processing.Children.Add(_strength); processing.Children.Add(_voiceDetails);
        _advanced.Children.Add(Card(processing));
        var diagnostics = new StackPanel { Spacing = 12 };
        diagnostics.Children.Add(new TextBlock { Text = "Session diagnostics", Style = (Style)Application.Current.Resources["SubtitleTextBlockStyle"] });
        diagnostics.Children.Add(_analyticsSummary);
        AddChart(diagnostics, "Loss (%)", window => window.LossPercent);
        AddChart(diagnostics, "Jitter p95 (ms)", window => window.InterarrivalP95Ms);
        AddChart(diagnostics, "Buffer depth (ms)", window => window.BufferDepthAvgMs);
        var export = new Button { Content = "Export session report" };
        export.Click += async (_, _) => await ExportAsync();
        diagnostics.Children.Add(export); _advanced.Children.Add(Card(diagnostics));
        body.Children.Add(_advanced);
        body.Children.Add(Text("For Discord, Teams, OBS or games: choose CABLE Input here and CABLE Output as that app’s microphone. Choose speakers for a quick test.", caption: true));
        var scroll = new ScrollViewer { Content = body, Padding = new Thickness(24, 12, 24, 24),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(scroll, 1); _root.Children.Add(scroll);
        // Root, ScrollViewer and body have no opaque brush: the system Mica canvas shows through.
        Content = _root;
        _start.Click += async (_, _) => await ToggleAsync();
        _route.Click += (_, _) => ChangeRoute();
        _output.SelectionChanged += (_, _) => { if (!_loading) { DescribeRoute(); SaveSettings(); } };
        _monitor.Toggled += (_, _) => ChangeMonitor();
        _monitorOutput.SelectionChanged += (_, _) => ChangeMonitorDevice();
        _advancedToggle.Toggled += (_, _) =>
        {
            _advanced.Visibility = _advancedToggle.IsOn ? Visibility.Visible : Visibility.Collapsed;
            if (!_loading) SaveSettings();
        };
        _autoListen.Checked += (_, _) => { if (!_loading) SaveSettings(); };
        _autoListen.Unchecked += (_, _) => { if (!_loading) SaveSettings(); };
        _automaticBuffer.Checked += (_, _) => ConfigureBuffer();
        _automaticBuffer.Unchecked += (_, _) => ConfigureBuffer();
        _buffer.ValueChanged += (_, _) => ConfigureBuffer();
        _voice.Toggled += (_, _) => ChangeVoice(selectPreset: false);
        _strength.ValueChanged += (_, _) => ChangeVoice(selectPreset: true);
    }

    private void RestoreSettings()
    {
        if (_settings.EnsurePairingKey()) _settings.Save();
        _port.Text = _settings.Port.ToString(CultureInfo.InvariantCulture); _key.Password = _settings.PairingKey;
        _autoListen.IsChecked = _settings.AutoListen; _advancedToggle.IsOn = _settings.AdvancedMode;
        _advanced.Visibility = _settings.AdvancedMode ? Visibility.Visible : Visibility.Collapsed;
        _automaticBuffer.IsChecked = _settings.AutomaticBuffering;
        _buffer.Value = Math.Clamp(_settings.PrebufferMilliseconds, 40, 300);
        _voice.IsOn = _settings.VoiceEnhance; _strength.Value = Math.Clamp(_settings.VoiceStrength, 0, 100);
        _monitor.IsOn = _settings.MonitorEnabled; _monitorOutput.IsEnabled = _monitor.IsOn;
        _engine.VoiceEnabled = _settings.VoiceEnhance; _engine.VoiceStrength = (float)_strength.Value / 100;
        UpdateVoiceText(); _strength.IsEnabled = _voice.IsOn;
        RefreshAddresses();
    }

    internal async void Start(bool show)
    {
        _showRequested |= show;
        PresentWindow();
        if (_settings.AutoListen) await ToggleAsync();
        if (!_showRequested && !_exiting && _settings.StartMinimized && (!_settings.AutoListen || _engine.IsRunning)) _appWindow.Hide();
    }

    internal void ShowReceiver()
    {
        _showRequested = true;
        PresentWindow();
    }

    private void PresentWindow()
    {
        if (_closed || _exiting) return;
        _appWindow.Show(); if (IsIconic(_hwnd)) ShowWindow(_hwnd, 9);
        Activate(); SetForegroundWindow(_hwnd);
    }

    private void CreateTray()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "PocketMic.ico");
        try { _icon = new Drawing.Icon(iconPath); _appWindow.SetIcon(iconPath); }
        catch (Exception error) when (error is IOException or ArgumentException or ExternalException)
        { _icon = (Drawing.Icon)Drawing.SystemIcons.Application.Clone(); }
        _tray.Icon = _icon; _tray.Text = "PocketMic Receiver";
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Show PocketMic", null, (_, _) => DispatcherQueue.TryEnqueue(ShowReceiver));
        menu.Items.Add("Exit", null, (_, _) => RequestExit());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => DispatcherQueue.TryEnqueue(ShowReceiver);
        _tray.Visible = true;
    }

    internal void RequestExit() => DispatcherQueue.TryEnqueue(async () => await ExitAsync());

    private async Task ExitAsync()
    {
        if (_exiting || _closed) return;
        _exiting = true; _start.IsEnabled = false;
        try
        {
            await StopAsync();
            if (_ownedRoute is { } route && !_routing.TryRestore(route, onlyIfStillTarget: true, out var error))
            {
                _appWindow.Show();
                await new ContentDialog { XamlRoot = _root.XamlRoot, Title = "Check Windows microphone settings",
                    Content = error, CloseButtonText = "Close" }.ShowAsync();
            }
            _ownedRoute = null; SaveSettings(); _tray.Visible = false; Close();
        }
        catch (Exception error)
        { _exiting = false; ShowReceiver(); Notice("Could not exit cleanly", error.Message, InfoBarSeverity.Error); _start.IsEnabled = true; }
    }

    private void PopulateDevices()
    {
        var devices = new List<Device> { new(-1, "Windows default output") };
        try
        {
            for (var index = 0; index < WaveOut.DeviceCount; index++)
                try { devices.Add(new Device(index, WaveOut.GetCapabilities(index).ProductName)); }
                catch (NAudio.MmException) { }
        }
        catch (NAudio.MmException) { }
        _output.ItemsSource = devices; _monitorOutput.ItemsSource = devices.ToList();
        _output.SelectedItem = devices.FirstOrDefault(device => device.Name == _settings.OutputDevice)
            ?? devices.FirstOrDefault(device => CableNames.Any(hint => device.Name.Contains(hint, StringComparison.OrdinalIgnoreCase))) ?? devices[0];
        _monitorOutput.SelectedItem = devices.FirstOrDefault(device => device.Name == _settings.MonitorDevice)
            ?? devices.FirstOrDefault(device => device.Number >= 0 && !CableNames.Any(hint => device.Name.Contains(hint, StringComparison.OrdinalIgnoreCase))) ?? devices[0];
    }

    private void ChangeMonitor()
    {
        _monitorOutput.IsEnabled = _monitor.IsOn;
        if (_loading || _reflecting) return;
        if (_engine.IsRunning)
        {
            _engine.MonitorEnabled = _monitor.IsOn;
            if (_monitor.IsOn && !_engine.MonitorEnabled)
            {
                Reflect(() => _monitor.IsOn = false); _monitorOutput.IsEnabled = false;
                Notice("Monitor output unavailable", _engine.LastMonitorError ?? "Select a different speaker output.");
            }
        }
        SaveSettings();
    }

    private void ChangeMonitorDevice()
    {
        if (_loading || _reflecting || !_engine.IsRunning || _monitorOutput.SelectedItem is not Device selected) return;
        if (!_engine.TrySetMonitorDevice(selected.Number, out var error))
        {
            Reflect(() => _monitorOutput.SelectedItem = _monitorOutput.Items.Cast<Device>()
                .FirstOrDefault(device => device.Number == _engine.MonitorDeviceNumber) ?? _monitorOutput.Items[0]);
            Notice("Monitor output unchanged", error); return;
        }
        if (_engine.MonitorEnabled) { _settings.MonitorDevice = selected.Name; _settings.Save(); }
    }

    private void ConfigureBuffer()
    {
        if (_loading) return;
        _engine.ConfigureBuffering(_automaticBuffer.IsChecked == true, (int)_buffer.Value);
        _effectiveBuffer.Text = $"Effective playback prebuffer: {_engine.PrebufferMilliseconds} ms · trim above {_engine.HighWaterMilliseconds} ms";
        SaveSettings();
    }

    private void ChangeVoice(bool selectPreset)
    {
        _strength.IsEnabled = _voice.IsOn;
        if (_loading || _reflecting) return;
        _engine.VoiceEnabled = _voice.IsOn;
        if (selectPreset) _engine.SelectVoicePreset((float)_strength.Value / 100);
        UpdateVoiceText(); SaveSettings();
    }

    private void UpdateVoiceText() => _voiceDetails.Text = _voice.IsOn
        ? $"Desktop strength: {_strength.Value:F0}% · move Strength to replace phone Custom DSP"
        : "Voice enhancement off — raw microphone audio";

    private void SaveSettings()
    {
        if (_loading) return;
        if (int.TryParse(_port.Text, out var port) && port is >= ControlProtocol.MinAudioPort and <= ControlProtocol.MaxAudioPort) _settings.Port = port;
        if (_key.Password.Length >= 8) _settings.PairingKey = _key.Password;
        _settings.OutputDevice = (_output.SelectedItem as Device)?.Name ?? "";
        _settings.MonitorEnabled = _monitor.IsOn; _settings.AutoListen = _autoListen.IsChecked == true;
        _settings.AdvancedMode = _advancedToggle.IsOn; _settings.AutomaticBuffering = _automaticBuffer.IsChecked == true;
        _settings.PrebufferMilliseconds = (int)_buffer.Value; _settings.VoiceEnhance = _voice.IsOn;
        _settings.VoiceStrength = (int)_strength.Value; _settings.MinimizeToTray = true; _settings.Save();
    }

    private async Task ToggleAsync()
    {
        if (_busy || _exiting || _stopTask is not null) return;
        _busy = true; _start.IsEnabled = false;
        try
        {
            if (_engine.IsRunning) { await StopAsync(); return; }
            if (!int.TryParse(_port.Text, out var port) || port is < ControlProtocol.MinAudioPort or > ControlProtocol.MaxAudioPort)
            { Notice("Invalid audio port", "Choose an audio port from 1 to 65534."); return; }
            if (_key.Password.Length < 8)
            { Notice("Pairing key required", "Use the same key on the phone and PC (at least 8 characters)."); return; }
            _run++; _history.Clear(); foreach (var chart in _charts) chart.Draw(_history);
            _analyticsSummary.Text = "Collecting this session’s diagnostics…";
            _engine.ConfigureBuffering(_automaticBuffer.IsChecked == true, (int)_buffer.Value);
            _engine.Start(new EngineOptions(port, _key.Password, (_output.SelectedItem as Device)?.Number ?? -1,
                (_monitorOutput.SelectedItem as Device)?.Number ?? -1, _monitor.IsOn,
                ReceiverFrontEnd.Modern, ReceiverBuild.VersionOf(typeof(MainWindow).Assembly)));
            _port.IsEnabled = _key.IsEnabled = _output.IsEnabled = false;
            _start.Content = "Stop receiver"; _source.Text = "Phone: waiting…"; SaveSettings();
        }
        catch (Exception error)
        {
            try { await StopAsync(); }
            catch (Exception stopError) { Notice("Could not stop receiver", stopError.Message, InfoBarSeverity.Error); return; }
            Notice("Could not start receiver", error.Message, InfoBarSeverity.Error);
        }
        finally { _busy = false; if (!_exiting) _start.IsEnabled = true; }
    }

    private async Task StopAsync()
    {
        if (_stopTask is not null) { await _stopTask; return; }
        _start.IsEnabled = false;
        _stopTask = _engine.StopAsync();
        try { await _stopTask; }
        finally { _stopTask = null; if (!_closed) _start.IsEnabled = !_busy && !_exiting; }
        if (_closed) return;
        _port.IsEnabled = _key.IsEnabled = _output.IsEnabled = true;
        _start.Content = "Start receiver"; _status.Text = "Stopped"; _source.Text = "Phone: —"; ShowStats(_engine.Stats);
    }

    private void SubscribeToEngine()
    {
        _engine.StatusChanged += (_, value) => Post(() => _status.Text = value.Message, requireRunning: true);
        _engine.PhoneAddressChanged += (_, endpoint) => Post(() => _source.Text = $"Phone: {endpoint}", requireRunning: true);
        _engine.StatsUpdated += (_, stats) => Post(() => ShowStats(stats), requireRunning: true);
        _engine.LinkQualityChanged += (_, value) => Post(() => _link.Text =
            $"Link: {value.Tier} · {value.Action} · recommends {value.Prebuffer} ms / {value.ConcealmentPackets} concealment packets", requireRunning: true);
        _engine.DspConfigReceived += (_, dsp) => Post(() =>
        {
            Reflect(() => _voice.IsOn = dsp.Enabled); _strength.IsEnabled = dsp.Enabled;
            _voiceDetails.Text = $"Custom from phone · HPF {dsp.HighPassHz} Hz · gate {dsp.Gate:P0} · compressor {dsp.Compressor:P0} · presence {dsp.PresenceDb:F1} dB";
        }, requireRunning: true);
        _engine.MonitorStarted += (_, _) =>
        {
            var openedDevice = _engine.MonitorDeviceNumber;
            Post(() =>
            {
                if (!_engine.MonitorEnabled || _engine.MonitorDeviceNumber != openedDevice) return;
                _settings.MonitorDevice = _monitorOutput.Items.Cast<Device>().FirstOrDefault(device => device.Number == openedDevice)?.Name ?? "";
                _settings.Save();
            }, requireRunning: true);
        };
        _engine.MonitorFailed += (_, message) => Post(() =>
        {
            Reflect(() => _monitor.IsOn = false); _monitorOutput.IsEnabled = false;
            _settings.MonitorEnabled = false; _settings.Save(); Notice("Monitor output unavailable", message);
        }, requireRunning: true);
        _engine.Fault += (_, message) => Post(async () =>
        {
            if (_exiting || !_engine.IsRunning) return;
            try { await StopAsync(); Notice("Receiver stopped", message, InfoBarSeverity.Error); }
            catch (Exception error) { Notice("Could not stop receiver", error.Message, InfoBarSeverity.Error); }
        });
        _engine.AnalyticsWindowClosed += (_, window) => Post(() =>
        {
            if (window.WindowKind == "10s")
            {
                if (_history.Count == 180) _history.RemoveAt(0);
                _history.Add(window); foreach (var chart in _charts) chart.Draw(_history);
            }
            _analyticsSummary.Text = $"{window.WindowKind} · loss {window.LossPercent:F2}% · jitter p95 {window.InterarrivalP95Ms:F1} ms · buffer {window.BufferDepthAvgMs:F1} ms · dropped analytics {window.DroppedSamples}";
        });
    }

    private void Post(Action action, bool requireRunning = false)
    {
        var run = _run;
        DispatcherQueue.TryEnqueue(() =>
        { if (!_closed && run == _run && (!requireRunning || _engine.IsRunning)) action(); });
    }

    private void Reflect(Action action)
    {
        _reflecting = true; try { action(); } finally { _reflecting = false; }
    }

    private void ShowStats(EngineStats stats)
    {
        _stats.Text = $"Packets {stats.Packets:N0} · lost {stats.Lost:N0} · late {stats.Late:N0} · rejected {stats.Rejected:N0} · trimmed {stats.Trimmed:N0} · queued {stats.BufferedMilliseconds:F0} ms";
        _effectiveBuffer.Text = $"Effective prebuffer: {_engine.PrebufferMilliseconds} ms · trim above {_engine.HighWaterMilliseconds} ms";
    }

    private void DescribeRoute()
    {
        if (_ownedRoute is not null)
        {
            _route.Content = "Restore previous Windows microphone"; _route.IsEnabled = true;
            _routeDescription.Text = "PocketMic owns a reversible Windows microphone assignment. Restore it before routing another cable."; return;
        }
        var name = (_output.SelectedItem as Device) is { Number: >= 0 } device ? device.Name : DefaultDeviceSwitcher.DefaultRenderFriendlyName();
        var endpoint = name is null ? null : DefaultDeviceSwitcher.ResolveCaptureForRenderName(name).Endpoint;
        _route.IsEnabled = endpoint is not null; _route.Content = "Use PocketMic as Windows microphone";
        _routeDescription.Text = endpoint is null ? "Choose speakers for a test, or a recognized virtual cable to route audio into other apps."
            : $"Maps to microphone: {endpoint.FriendlyName}. Changing Windows defaults requires the button below.";
    }

    private void ChangeRoute()
    {
        if (_ownedRoute is { } route)
        {
            if (_routing.TryRestore(route, onlyIfStillTarget: false, out var restoreError)) _ownedRoute = null;
            else Notice("Microphone restore failed", restoreError, InfoBarSeverity.Error);
            DescribeRoute(); return;
        }
        var selected = _output.SelectedItem as Device;
        var name = selected is { Number: >= 0 } ? selected.Name : DefaultDeviceSwitcher.DefaultRenderFriendlyName();
        var endpoint = name is null ? null : DefaultDeviceSwitcher.ResolveCaptureForRenderName(name).Endpoint;
        if (endpoint is null) { DescribeRoute(); return; }
        if (!_routing.TryRouteTo(endpoint.Id, out _ownedRoute, out var error)) Notice("Microphone routing failed", error, InfoBarSeverity.Error);
        DescribeRoute();
    }

    private async Task ShowQrAsync()
    {
        var version = 0;
        try
        {
            if (_root.XamlRoot is null) return;
            RefreshAddresses();
            var addresses = _addressChoices;
            if (addresses.Length == 0 || !int.TryParse(_port.Text, out var port) || port is < 1 or > 65534 || _key.Password.Length < 8)
            { Notice("Pairing is not ready", "Connect to a LAN and enter a valid port and pairing key."); return; }
            var key = _key.Password;
            var choice = new ComboBox { Header = "PC address", ItemsSource = addresses, DisplayMemberPath = nameof(PairingAddress.Label), HorizontalAlignment = HorizontalAlignment.Stretch };
            choice.SelectedItem = addresses.FirstOrDefault(address => address.Address == _selectedAddress) ?? addresses[0];
            _selectedAddress = ((PairingAddress)choice.SelectedItem).Address;
            var image = new Image { MaxWidth = 320, Height = 320, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Stretch };
            image.SizeChanged += (_, args) => image.Height = Math.Min(320, Math.Max(1, args.NewSize.Width));
            AutomationProperties.SetName(image, "Pairing QR code; scan with PocketMic on your phone");
            async Task RenderAsync()
            {
                var request = ++version;
                using var generator = new QRCodeGenerator();
                var selected = (PairingAddress)choice.SelectedItem;
                using var data = generator.CreateQrCode($"pmic://{selected.Address}:{port}/{key}", QRCodeGenerator.ECCLevel.M);
                using var code = new PngByteQRCode(data);
                var bytes = code.GetGraphic(8); // Opaque black/white modules and quiet zone.
                using var stream = new InMemoryRandomAccessStream();
                using (var writer = new DataWriter(stream.GetOutputStreamAt(0))) { writer.WriteBytes(bytes); await writer.StoreAsync(); }
                stream.Seek(0); var bitmap = new BitmapImage(); await bitmap.SetSourceAsync(stream);
                if (request == version) image.Source = bitmap;
            }
            choice.SelectionChanged += async (_, _) =>
            {
                if (choice.SelectedItem is PairingAddress selected)
                {
                    _selectedAddress = selected.Address;
                    _addresses.SelectedItem = _addressChoices.FirstOrDefault(address => address.Address == selected.Address);
                }
                try { await RenderAsync(); }
                catch (Exception)
                {
                    version++; image.Source = null;
                    Notice("Could not create pairing code", "Try another PC address or reopen the pairing dialog.", InfoBarSeverity.Error);
                }
            };
            await RenderAsync();
            var content = new StackPanel { Spacing = 12 };
            content.Children.Add(choice); content.Children.Add(image);
            content.Children.Add(Text("Scan this code in PocketMic. The image remains black and white in every Windows theme.", caption: true));
            var dialog = new ContentDialog { XamlRoot = _root.XamlRoot, Title = "Pair your phone", Content = content, CloseButtonText = "Done" };
            await dialog.ShowAsync();
        }
        catch (Exception)
        {
            // QR inputs contain the pairing key; never surface generator or stream exception details.
            Notice("Could not show pairing code", "Finish any open dialog and try pairing again.", InfoBarSeverity.Error);
        }
        finally { version++; }
    }

    private async Task ExportAsync()
    {
        var session = _engine.AnalyticsSessionPath;
        if (session is null || !File.Exists(session)) { Notice("No session recorded", "Start the receiver and collect some audio first."); return; }
        try
        {
            var picker = new FileSavePicker { SuggestedFileName = Path.GetFileNameWithoutExtension(session), SuggestedStartLocation = PickerLocationId.DocumentsLibrary };
            picker.FileTypeChoices.Add("Markdown report", new List<string> { ".md" });
            WinRT.Interop.InitializeWithWindow.Initialize(picker, _hwnd);
            var file = await picker.PickSaveFileAsync();
            if (file is not null) SessionReportWriter.WriteMarkdown(session, file.Path);
        }
        catch (Exception error) { Notice("Could not export report", error.Message, InfoBarSeverity.Error); }
    }

    private void Notice(string title, string message, InfoBarSeverity severity = InfoBarSeverity.Warning)
    { _notice.Title = title; _notice.Message = message; _notice.Severity = severity; _notice.IsOpen = true; }

    private void RefreshAddresses()
    {
        var previous = _addresses.SelectedItem is PairingAddress current ? current.Address : _selectedAddress;
        try
        {
            var candidates = NetworkInterface.GetAllNetworkInterfaces()
                .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up && adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses
                    .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address.Address))
                    .Select(address => new PairingAddress(adapter.Name, adapter.NetworkInterfaceType.ToString(), address.Address.ToString(),
                        adapter.GetIPProperties().GatewayAddresses.Any(gateway => gateway.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(gateway.Address)),
                        adapter.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.Wireless80211 &&
                        !new[] { "virtual", "vpn", "tunnel", "hyper-v", "vmware", "virtualbox", "wsl" }
                            .Any(hint => $"{adapter.Name} {adapter.Description}".Contains(hint, StringComparison.OrdinalIgnoreCase))))
                .ToArray();
            _addressChoices = PairingAddressRanker.Rank(candidates).ToArray();
        }
        catch (NetworkInformationException) { _addressChoices = Array.Empty<PairingAddress>(); }
        _addresses.ItemsSource = _addressChoices;
        _addresses.SelectedItem = _addressChoices.FirstOrDefault(address => address.Address == previous) ??
            _addressChoices.FirstOrDefault(address => address.IsPhysical && address.HasDefaultGateway) ?? _addressChoices.FirstOrDefault();
        _selectedAddress = (_addresses.SelectedItem as PairingAddress)?.Address;
        if (_addressChoices.Length == 0) _addresses.PlaceholderText = "No active IPv4 addresses";
    }

    private void AddChart(StackPanel target, string label, Func<WindowStats, double> value)
    {
        var chart = new MetricChart(label, value); _charts.Add(chart); target.Children.Add(chart.Content);
        chart.Canvas.SizeChanged += (_, _) => chart.Draw(_history);
    }

    private sealed class MetricChart
    {
        public StackPanel Content { get; } = new() { Spacing = 4 };
        public Canvas Canvas { get; } = new() { Height = 48, HorizontalAlignment = HorizontalAlignment.Stretch };
        private readonly Polyline _line = new() { Style = (Style)Application.Current.Resources["ReceiverChartLineStyle"] };
        private readonly TextBlock _label;
        private readonly string _name;
        private readonly Func<WindowStats, double> _value;
        public MetricChart(string name, Func<WindowStats, double> value)
        {
            _name = name; _value = value; _label = Text(name + " · waiting…", caption: true);
            Content.Children.Add(_label); Content.Children.Add(Canvas); Canvas.Children.Add(_line);
            AutomationProperties.SetName(Canvas, name + " trend");
        }
        public void Draw(IReadOnlyList<WindowStats> windows)
        {
            var points = new PointCollection(); var width = Math.Max(1, Canvas.ActualWidth);
            var peak = Math.Max(0.01, windows.Count == 0 ? 1 : windows.Max(_value) * 1.1);
            for (var index = 0; index < windows.Count; index++) points.Add(new Point(index * width / Math.Max(1, windows.Count - 1), 46 - Math.Clamp(_value(windows[index]) / peak, 0, 1) * 44));
            _line.Points = points; _label.Text = windows.Count == 0 ? _name + " · waiting…" : $"{_name} · {_value(windows[^1]):F2}";
        }
    }

    [DllImport("user32.dll", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern bool ShowWindow(IntPtr window, int command);
    [DllImport("user32.dll", ExactSpelling = true)] private static extern bool SetForegroundWindow(IntPtr window);
}
