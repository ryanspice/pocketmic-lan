using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PocketMicReceiver;

public enum ReceiverActivationStatus { Primary, ActivatedExisting, ExistingUnresponsive }

public sealed record ReceiverActivationResult(ReceiverActivationStatus Status, ReceiverActivation? Instance);

/// <summary>
/// Shared by both front ends. Owns one receiver UI per current Windows user/logon session.
/// The host keeps the primary Instance alive and disposes it at shutdown. SHOW callbacks run
/// on a worker and must promptly queue/marshal to the UI, including while its window starts.
/// This class never closes another process and is separate from legacy port coexistence.
/// </summary>
public sealed class ReceiverActivation : IDisposable
{
    private static readonly byte[] ShowCommand = "SHOW"u8.ToArray();
    private static readonly byte[] ExitCommand = "EXIT"u8.ToArray();
    private static readonly byte[] Acknowledgement = "OK"u8.ToArray();
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(1);
    private readonly NamedPipeServerStream _pipe;
    private readonly Action _showWindow;
    private readonly Action? _requestExit;
    private readonly CancellationTokenSource _stop = new();
    private readonly CancellationToken _stopping;
    private readonly ManualResetEventSlim _release = new(false);
    private readonly TaskCompletionSource _mutexReleased = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object _callbackGate = new();
    private readonly Thread _ownerThread;
    private int _disposed;

    private ReceiverActivation(string pipeName, Action showWindow, Action? requestExit)
    {
        _showWindow = showWindow;
        _requestExit = requestExit;
        _ownerThread = Thread.CurrentThread;
        _stopping = _stop.Token;
        _pipe = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Message, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        _ = ListenAsync();
    }

    /// <summary>
    /// Call before starting either UI. Primary means this host may start; ActivatedExisting
    /// means exit normally. ExistingUnresponsive means report the blocked activation and exit,
    /// without launching a competing receiver. Both front ends must use the same applicationId.
    /// </summary>
    public static async Task<ReceiverActivationResult> StartOrActivateAsync(Action showWindow,
        TimeSpan? timeout = null, CancellationToken cancellationToken = default,
        string applicationId = "PocketMicReceiver", Action? requestExit = null)
    {
        ArgumentNullException.ThrowIfNull(showWindow);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        var wait = timeout ?? TimeSpan.FromSeconds(4);
        if (wait <= TimeSpan.Zero || wait > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        cancellationToken.ThrowIfCancellationRequested();
        var (mutexName, pipeName) = Names(applicationId);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(wait);

        try
        {
            while (true)
            {
                deadline.Token.ThrowIfCancellationRequested();
                var primary = await TryStartPrimaryAsync(mutexName, pipeName, showWindow, requestExit, deadline.Token).ConfigureAwait(false);
                if (primary is not null)
                {
                    if (deadline.IsCancellationRequested)
                    {
                        primary.Dispose();
                        deadline.Token.ThrowIfCancellationRequested();
                    }
                    return new(ReceiverActivationStatus.Primary, primary);
                }
                if (await SendCommandAsync(pipeName, ShowCommand, deadline.Token).ConfigureAwait(false))
                    return new(ReceiverActivationStatus.ActivatedExisting, null);
                // The incumbent may have exited between the ownership check and pipe connect.
                // Retry acquisition as well as IPC; never promote just because IPC timed out.
                await Task.Delay(25, deadline.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(ReceiverActivationStatus.ExistingUnresponsive, null);
        }
    }

    /// <summary>Ask an existing host to exit gracefully for an update; never starts a receiver.</summary>
    public static async Task<bool> RequestExitAsync(CancellationToken cancellationToken = default,
        string applicationId = "PocketMicReceiver", string? installationDirectory = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationId);
        return await SendCommandAsync(Names(applicationId).Pipe, ExitCommand, cancellationToken,
            installationDirectory).ConfigureAwait(false);
    }

    private static (string Mutex, string Pipe) Names(string applicationId)
    {
        using var identity = WindowsIdentity.GetCurrent();
        using var process = Process.GetCurrentProcess();
        var user = identity.User?.Value ?? throw new InvalidOperationException("Windows user identity is unavailable.");
        var scope = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{applicationId}\0{user}\0{process.SessionId}")))[..32];
        var name = $"PocketMic.Activation.{scope}";
        return ($@"Local\{name}", name);
    }

    private static async Task<ReceiverActivation?> TryStartPrimaryAsync(string mutexName, string pipeName,
        Action showWindow, Action? requestExit, CancellationToken cancellationToken)
    {
        var started = new TaskCompletionSource<ReceiverActivation?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => started.TrySetCanceled(cancellationToken));
        var thread = new Thread(() =>
        {
            ReceiverActivation? owner = null;
            Mutex? mutex = null;
            var acquired = false;
            try
            {
                mutex = new Mutex(false, mutexName);
                try { acquired = mutex.WaitOne(0); }
                catch (AbandonedMutexException) { acquired = true; }
                if (!acquired || cancellationToken.IsCancellationRequested)
                {
                    started.TrySetResult(null);
                    return;
                }
                // Publish Primary only after its pipe exists, closing the usual startup gap.
                owner = new ReceiverActivation(pipeName, showWindow, requestExit);
                if (!started.TrySetResult(owner))
                {
                    owner.Dispose();
                    return;
                }
                owner._release.Wait();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                started.TrySetResult(null);
            }
            catch (Exception error)
            {
                started.TrySetException(error);
            }
            finally
            {
                if (acquired) mutex!.ReleaseMutex();
                mutex?.Dispose();
                owner?._mutexReleased.TrySetResult();
            }
        }) { IsBackground = true, Name = "PocketMic activation owner" };
        thread.Start();
        return await started.Task.ConfigureAwait(false);
    }

    private static async Task<bool> SendCommandAsync(string pipeName, byte[] command, CancellationToken cancellationToken,
        string? installationDirectory = null)
    {
        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attempt.CancelAfter(RequestTimeout);
        using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        try
        {
            await client.ConnectAsync(150, attempt.Token).ConfigureAwait(false);
            if (installationDirectory is not null)
            {
                // An installer must never shut down a developer's separate receiver copy.
                if (!GetNamedPipeServerProcessId(client.SafePipeHandle, out var ownerId)) return false;
                using var owner = Process.GetProcessById(checked((int)ownerId));
                var path = owner.MainModule?.FileName;
                var directory = Path.GetFullPath(installationDirectory);
                if (!string.Equals(path, Path.Combine(directory, "PocketMicReceiver.exe"), StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(path, Path.Combine(directory, "modern", "PocketMicReceiver.WinUI.exe"), StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            // The newly launched process can transfer foreground permission to this exact
            // authenticated pipe server. A denial is harmless: SHOW still restores the UI.
            if (command == ShowCommand && GetNamedPipeServerProcessId(client.SafePipeHandle, out var serverProcessId) && serverProcessId != 0)
                AllowSetForegroundWindow(serverProcessId);
            await client.WriteAsync(command, attempt.Token).ConfigureAwait(false);
            await client.FlushAsync(attempt.Token).ConfigureAwait(false);
            var response = new byte[Acknowledgement.Length];
            await client.ReadExactlyAsync(response, attempt.Token).ConfigureAwait(false);
            return response.AsSpan().SequenceEqual(Acknowledgement);
        }
        catch (Exception error) when (error is IOException or TimeoutException or UnauthorizedAccessException or OperationCanceledException
            or System.ComponentModel.Win32Exception or ArgumentException or InvalidOperationException)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return false;
        }
    }

    private async Task ListenAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            try
            {
                await _pipe.WaitForConnectionAsync(_stopping).ConfigureAwait(false);
                using var request = CancellationTokenSource.CreateLinkedTokenSource(_stopping);
                request.CancelAfter(RequestTimeout);
                var command = new byte[ShowCommand.Length + 1];
                var count = await _pipe.ReadAsync(command, request.Token).ConfigureAwait(false);
                Action? callback = null;
                if (count == ShowCommand.Length && _pipe.IsMessageComplete)
                {
                    if (command.AsSpan(0, count).SequenceEqual(ShowCommand)) callback = _showWindow;
                    else if (command.AsSpan(0, count).SequenceEqual(ExitCommand)) callback = _requestExit;
                }
                if (callback is not null)
                {
                    if (!InvokeCallback(callback)) return;
                    await _pipe.WriteAsync(Acknowledgement, request.Token).ConfigureAwait(false);
                    await _pipe.FlushAsync(request.Token).ConfigureAwait(false);
                }
            }
            catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException)
            {
                if (_stopping.IsCancellationRequested) return;
            }
            catch (Exception)
            {
                // A host callback failure must not permanently disable later SHOW requests.
            }
            finally
            {
                // Failed reads/writes can set PipeState.Broken, making IsConnected false.
                // That state still requires Disconnect before another connection can wait.
                try { _pipe.Disconnect(); }
                catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException) { }
            }
        }
    }

    private bool InvokeCallback(Action callback)
    {
        // Keep monitor ownership and exception unwinding in one synchronous frame. A host
        // exception must release this gate before the async listener begins its next request.
        lock (_callbackGate)
        {
            if (_disposed != 0) return false;
            callback();
            return true;
        }
    }

    public void Dispose()
    {
        lock (_callbackGate)
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _stop.Cancel();
            _pipe.Dispose();
            _release.Set();
        }
        // A Mutex must be released by its acquiring thread, even if Dispose is called by a
        // different UI/async thread. The ownership thread performs that release before exit.
        if (Thread.CurrentThread != _ownerThread) _mutexReleased.Task.GetAwaiter().GetResult();
        _stop.Dispose();
        _release.Dispose();
    }

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint serverProcessId);

    [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
