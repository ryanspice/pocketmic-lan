using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace PocketMicReceiver.WinUI;

public partial class App : Application
{
    private readonly bool _show;
    public static MainWindow? ReceiverWindow { get; private set; }
    private static int _pendingShow;
    private static int _pendingExit;

    public App(bool show)
    {
        _show = show;
        UnhandledException += (_, args) => StartupDiagnostics.Failure("XAML unhandled exception", args.Exception);
        StartupDiagnostics.Write("Initializing App.xaml resources");
        try { InitializeComponent(); }
        catch (Exception error) { StartupDiagnostics.Failure("InitializeComponent", error); throw; }
        StartupDiagnostics.Write("App.xaml resources initialized");
    }

    internal static void RequestShow()
    {
        Interlocked.Exchange(ref _pendingShow, 1);
        var window = ReceiverWindow;
        window?.DispatcherQueue.TryEnqueue(() =>
        {
            if (Interlocked.Exchange(ref _pendingShow, 0) != 0) window.ShowReceiver();
        });
    }

    internal static void RequestExit()
    {
        Interlocked.Exchange(ref _pendingExit, 1);
        var window = ReceiverWindow;
        window?.DispatcherQueue.TryEnqueue(() =>
        {
            if (Interlocked.Exchange(ref _pendingExit, 0) != 0) window.RequestExit();
        });
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            StartupDiagnostics.Write("Constructing MainWindow");
            ReceiverWindow = new MainWindow();
            StartupDiagnostics.Write("MainWindow constructed");
            StartupDiagnostics.UiInitialized = true;
            if (Interlocked.Exchange(ref _pendingExit, 0) != 0)
            {
                ReceiverWindow.RequestExit();
                return;
            }
            var pendingShow = Interlocked.Exchange(ref _pendingShow, 0) != 0;
            var show = _show || pendingShow;
            ReceiverWindow.Start(show);
            StartupDiagnostics.Write("Window startup dispatched");
        }
        catch (Exception error) { StartupDiagnostics.Failure("OnLaunched", error); throw; }
    }
}

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
        {
            if (eventArgs.ExceptionObject is Exception error) StartupDiagnostics.Failure("Runtime unhandled exception", error);
        };
        try
        {
            WinRT.ComWrappersSupport.InitializeComWrappers();
            var activation = ReceiverActivation.StartOrActivateAsync(App.RequestShow, requestExit: App.RequestExit).GetAwaiter().GetResult();
            if (activation.Status != ReceiverActivationStatus.Primary)
            {
                Environment.ExitCode = activation.Status == ReceiverActivationStatus.ActivatedExisting ? 0 : 2;
                return;
            }
            using var owner = activation.Instance;
            // A second launch only activates the owner; preserve that owner's startup evidence.
            StartupDiagnostics.Begin();
            StartupDiagnostics.Write("Starting XAML application");
            Application.Start(initialization =>
            {
                SynchronizationContext.SetSynchronizationContext(
                    new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
                _ = new App(Array.Exists(args, value => value.Equals("--show", StringComparison.OrdinalIgnoreCase)));
            });
        }
        catch (Exception error) { StartupDiagnostics.Failure("Program.Main", error); throw; }
    }
}

/// <summary>Bounded local startup evidence; never reads settings, arguments, or exception data.</summary>
internal static class StartupDiagnostics
{
    private static readonly object Gate = new();
    private static readonly string LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PocketMic", "receiver-winui-startup.log");
    internal static bool UiInitialized { get; set; }

    internal static void Begin()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.WriteAllText(LogPath, $"PocketMic WinUI startup {DateTimeOffset.UtcNow:O}{Environment.NewLine}");
        }
        catch { /* Diagnostics must not introduce a startup failure. */ }
    }

    internal static void Failure(string stage, Exception error)
    {
        var entry = new StringBuilder(stage);
        for (var depth = 0; depth < 3 && error is not null; depth++, error = error.InnerException!)
        {
            entry.AppendLine().Append(error.GetType().FullName).Append(" HRESULT 0x").Append(error.HResult.ToString("X8"));
            // After window construction, audio may process private inputs. Keep only type/stack then.
            if (!UiInitialized) entry.AppendLine().Append(Bounded(error.Message, 2048));
            entry.AppendLine().Append(Bounded(error.StackTrace ?? "(no managed stack)", 8192));
        }
        Write(entry.ToString());
    }

    internal static void Write(string text)
    {
        try
        {
            lock (Gate)
            {
                if (new FileInfo(LogPath).Length >= 65536) return;
                File.AppendAllText(LogPath, $"{DateTimeOffset.UtcNow:O} {Bounded(text, 16384)}{Environment.NewLine}");
            }
        }
        catch { }
    }

    private static string Bounded(string text, int limit) => text.Length <= limit ? text : text[..limit] + " [truncated]";
}
