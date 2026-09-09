using System.Diagnostics;
using System.Net.NetworkInformation;

namespace PocketMicReceiver;

/// <summary>Another receiver process found running. <see cref="IsClassic"/> is the WinForms build.</summary>
public sealed record ReceiverInstance(int ProcessId, string ProcessName, bool IsClassic);

/// <summary>
/// Keeps two receivers from fighting over the same sockets.
///
/// Both front ends bind the audio port with <c>ExclusiveAddressUse</c> and the control port
/// beside it, so whichever starts second fails with a socket error that says nothing about the
/// real cause. Rather than let the user decode that, a front end asks this class whether anyone
/// else is already listening and offers to shut them down first.
///
/// Two mechanisms, because neither is sufficient alone. The named mutex is authoritative but
/// only for builds that take it, and the shipped classic build predates it. The process scan
/// catches everything but cannot see a renamed executable. A front end should treat either
/// signal as "someone else is here".
/// </summary>
public static class SingleInstance
{
    /// <summary>
    /// Machine-wide rather than per-session: UDP ports are a machine resource, so two receivers
    /// in two logon sessions collide exactly as two in one session would.
    /// </summary>
    public const string MutexName = @"Global\PocketMicReceiver";

    /// <summary>
    /// Used when the Global namespace is refused. Creating a global kernel object needs a
    /// privilege that an interactive desktop user normally has but is not guaranteed, and losing
    /// cross-session detection is far better than failing to start.
    /// </summary>
    private const string FallbackMutexName = @"Local\PocketMicReceiver";

    private const string ClassicProcessName = "PocketMicReceiver";
    private const string ModernProcessName = "PocketMicReceiver.WinUI";

    private static readonly TimeSpan GracefulTimeout = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan ForcedTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Takes the run lock, or returns null when another receiver already holds it. The caller
    /// owns the returned mutex and must keep it alive for as long as it intends to be the one
    /// running receiver.
    /// </summary>
    public static Mutex? TryAcquire()
    {
        foreach (var name in new[] { MutexName, FallbackMutexName })
        {
            try
            {
                var mutex = new Mutex(true, name, out var createdNew);
                if (createdNew) return mutex;

                mutex.Dispose();
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                // The global name exists but is not ours to open, or the privilege is missing.
                // Either way there is still something worth checking in the local namespace.
            }
            catch (IOException)
            {
            }
        }

        return null;
    }

    /// <summary>Every other receiver process of either flavour, excluding this one.</summary>
    public static IReadOnlyList<ReceiverInstance> FindOthers()
    {
        var self = Environment.ProcessId;
        var found = new List<ReceiverInstance>();

        foreach (var (name, isClassic) in new[] { (ClassicProcessName, true), (ModernProcessName, false) })
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(name);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            foreach (var process in processes)
            {
                try
                {
                    if (process.Id != self && !process.HasExited)
                    {
                        found.Add(new ReceiverInstance(process.Id, name, isClassic));
                    }
                }
                catch (InvalidOperationException)
                {
                    // Exited between enumeration and inspection.
                }
                finally
                {
                    process.Dispose();
                }
            }
        }

        return found;
    }

    /// <summary>
    /// Asks the other receivers to stand down and waits until the ports are actually free.
    ///
    /// Success is measured on the ports, never on the processes, because closing the classic
    /// receiver's window stops its run and releases its sockets but deliberately leaves the
    /// process alive in the notification area. Killing it for still existing would discard a
    /// window the user asked to keep, so a tray-resident instance that has let go of the ports
    /// counts as done.
    /// </summary>
    public static async Task<bool> CloseOthersAsync(int audioPort, CancellationToken cancellationToken = default)
    {
        var others = FindOthers();
        if (others.Count == 0) return !ArePortsBound(audioPort);

        foreach (var instance in others)
        {
            RequestClose(instance.ProcessId);
        }

        if (await WaitForPortsAsync(audioPort, GracefulTimeout, cancellationToken).ConfigureAwait(false))
        {
            return true;
        }

        // Still holding the sockets after being asked politely: either there is no window to
        // close, or it refused. Only now is force justified.
        foreach (var instance in others)
        {
            ForceExit(instance.ProcessId);
        }

        return await WaitForPortsAsync(audioPort, ForcedTimeout, cancellationToken).ConfigureAwait(false);
    }

    private static void RequestClose(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            process.CloseMainWindow();
        }
        catch (ArgumentException)
        {
            // Already gone.
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static void ForceExit(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.HasExited) process.Kill();
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // A receiver running elevated cannot be terminated from here; the caller reports
            // that the ports are still busy rather than pretending it succeeded.
        }
    }

    private static async Task<bool> WaitForPortsAsync(int audioPort, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (true)
        {
            if (!ArePortsBound(audioPort)) return true;
            if (Environment.TickCount64 >= deadline) return false;

            try
            {
                await Task.Delay(PollInterval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Whether either receiver port is bound anywhere on the machine. Safe to read only before
    /// this process has opened its own sockets, which is the one moment it is asked.
    /// </summary>
    private static bool ArePortsBound(int audioPort)
    {
        var controlPort = ControlProtocol.ControlPort(audioPort);
        try
        {
            foreach (var endpoint in IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners())
            {
                if (endpoint.Port == audioPort || endpoint.Port == controlPort) return true;
            }
        }
        catch (NetworkInformationException)
        {
            // Without an answer, assume the ports are free and let the bind report the truth.
        }

        return false;
    }
}
