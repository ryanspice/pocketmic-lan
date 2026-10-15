using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Text;
using Xunit;

namespace PocketMicReceiver.Tests;

public sealed class ReceiverActivationTests
{
    private static string Identity() => $"PocketMic.Activation.Test.{Guid.NewGuid():N}";

    [Fact]
    public async Task UpdateExitUsesItsOwnCallbackWithoutShowingOrCreatingAReceiver()
    {
        var id = Identity();
        Assert.False(await ReceiverActivation.RequestExitAsync(applicationId: id));
        var shown = 0;
        var exits = 0;
        var first = await ReceiverActivation.StartOrActivateAsync(() => shown++, applicationId: id,
            requestExit: () => exits++);
        using var owner = first.Instance;
        Assert.True(await ReceiverActivation.RequestExitAsync(applicationId: id));
        Assert.Equal(1, exits);
        Assert.Equal(0, shown);
        var next = await ReceiverActivation.StartOrActivateAsync(() => { }, applicationId: id);
        Assert.Equal(ReceiverActivationStatus.ActivatedExisting, next.Status);
    }

    [Fact]
    public async Task UpdateExitCannotCloseAReceiverOutsideTheInstallationDirectory()
    {
        var id = Identity();
        var exits = 0;
        var first = await ReceiverActivation.StartOrActivateAsync(() => { }, applicationId: id,
            requestExit: () => exits++);
        using var owner = first.Instance;
        Assert.False(await ReceiverActivation.RequestExitAsync(applicationId: id,
            installationDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
        Assert.Equal(0, exits);
        var next = await ReceiverActivation.StartOrActivateAsync(() => { }, applicationId: id);
        Assert.Equal(ReceiverActivationStatus.ActivatedExisting, next.Status);
    }

    [Fact]
    public async Task UpdateExitWithoutRegisteredHandlerIsRejected()
    {
        var id = Identity();
        var first = await ReceiverActivation.StartOrActivateAsync(() => throw new Exception("Must not show"), applicationId: id);
        using var owner = first.Instance;
        Assert.False(await ReceiverActivation.RequestExitAsync(applicationId: id));
    }

    [Fact]
    public async Task SecondLaunchActivatesFirstWithoutStartingAnotherOwner()
    {
        var id = Identity();
        var shown = 0;
        var first = await ReceiverActivation.StartOrActivateAsync(() => Interlocked.Increment(ref shown), applicationId: id);
        using var owner = Assert.IsType<ReceiverActivation>(first.Instance);
        Assert.Equal(ReceiverActivationStatus.Primary, first.Status);

        var second = await ReceiverActivation.StartOrActivateAsync(() => throw new Exception("Wrong callback"), applicationId: id);

        Assert.Equal(ReceiverActivationStatus.ActivatedExisting, second.Status);
        Assert.Null(second.Instance);
        Assert.Equal(1, Volatile.Read(ref shown));
    }

    [Fact]
    public async Task ConcurrentLaunchesHaveExactlyOneOwnedPrimary()
    {
        var id = Identity();
        var shown = 0;
        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            ReceiverActivation.StartOrActivateAsync(() => Interlocked.Increment(ref shown), applicationId: id)));
        try
        {
            Assert.Single(results, result => result.Status == ReceiverActivationStatus.Primary);
            Assert.Equal(5, results.Count(result => result.Status == ReceiverActivationStatus.ActivatedExisting));
            Assert.Equal(5, Volatile.Read(ref shown));
        }
        finally { foreach (var result in results) result.Instance?.Dispose(); }
    }

    [Fact]
    public async Task DisposalOnAnotherThreadReleasesOwnedMutexAndPipeForNextLaunch()
    {
        var id = Identity();
        var first = await ReceiverActivation.StartOrActivateAsync(() => { }, applicationId: id);
        var owner = Assert.IsType<ReceiverActivation>(first.Instance);
        await Task.Run(owner.Dispose);
        owner.Dispose();

        var next = await ReceiverActivation.StartOrActivateAsync(() => { }, applicationId: id);
        using var replacement = next.Instance;
        Assert.Equal(ReceiverActivationStatus.Primary, next.Status);
    }

    [Fact]
    public async Task ExistingOwnerWithoutPipeTimesOutInsteadOfStartingCompetitor()
    {
        var id = Identity();
        var names = Names(id);
        using var holder = new MutexHolder(names.Mutex);
        var elapsed = Stopwatch.StartNew();

        var result = await ReceiverActivation.StartOrActivateAsync(() => throw new Exception("Must not start"),
            TimeSpan.FromMilliseconds(250), applicationId: id);

        Assert.Equal(ReceiverActivationStatus.ExistingUnresponsive, result.Status);
        Assert.Null(result.Instance);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task SecondaryRetriesUntilIncumbentsDelayedPipeBecomesReady()
    {
        var id = Identity();
        var names = Names(id);
        using var holder = new MutexHolder(names.Mutex);
        var activating = ReceiverActivation.StartOrActivateAsync(() => throw new Exception("Must not start"),
            TimeSpan.FromSeconds(3), applicationId: id);
        await Task.Delay(250);
        using var server = new NamedPipeServerStream(names.Pipe, PipeDirection.InOut, 1,
            PipeTransmissionMode.Message, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await server.WaitForConnectionAsync(deadline.Token);
        var command = new byte[5];
        var count = await server.ReadAsync(command, deadline.Token);
        Assert.Equal("SHOW", Encoding.ASCII.GetString(command, 0, count));
        await server.WriteAsync("OK"u8.ToArray(), deadline.Token);

        var result = await activating;
        Assert.Equal(ReceiverActivationStatus.ActivatedExisting, result.Status);
        Assert.Null(result.Instance);
    }

    [Fact]
    public async Task CancellationStopsStartupWaitWithoutTakingOverExistingOwner()
    {
        var id = Identity();
        using var holder = new MutexHolder(Names(id).Mutex);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            ReceiverActivation.StartOrActivateAsync(() => { }, cancellationToken: cancellation.Token, applicationId: id));
    }

    [Theory]
    [InlineData("QUIT")]
    [InlineData("SHOW-extra")]
    [InlineData("show")]
    public async Task OnlyExactShowCommandInvokesCallbackAndBadClientsDoNotBreakListener(string command)
    {
        var id = Identity();
        var shown = 0;
        var first = await ReceiverActivation.StartOrActivateAsync(() => Interlocked.Increment(ref shown), applicationId: id);
        using var owner = first.Instance;
        using (var bad = new NamedPipeClientStream(".", Names(id).Pipe, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await bad.ConnectAsync(deadline.Token);
            try
            {
                await bad.WriteAsync(Encoding.ASCII.GetBytes(command), deadline.Token);
                Assert.Equal(0, await bad.ReadAsync(new byte[2], deadline.Token));
            }
            catch (IOException) { /* Disconnect with unread excess input is also a rejection. */ }
        }
        Assert.Equal(0, Volatile.Read(ref shown));

        var next = await ReceiverActivation.StartOrActivateAsync(() => { }, applicationId: id);
        Assert.Equal(ReceiverActivationStatus.ActivatedExisting, next.Status);
        Assert.Equal(1, Volatile.Read(ref shown));
    }

    [Fact]
    public async Task StalledClientIsBoundedAndDoesNotPermanentlyBlockActivation()
    {
        var id = Identity();
        var shown = 0;
        var first = await ReceiverActivation.StartOrActivateAsync(() => Interlocked.Increment(ref shown), applicationId: id);
        using var owner = first.Instance;
        using var stalled = new NamedPipeClientStream(".", Names(id).Pipe, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await stalled.ConnectAsync(1_000);

        var next = await ReceiverActivation.StartOrActivateAsync(() => { }, applicationId: id);

        Assert.Equal(ReceiverActivationStatus.ActivatedExisting, next.Status);
        Assert.Equal(1, Volatile.Read(ref shown));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task HostCallbackFailureDoesNotDisableLaterActivation(int failures)
    {
        var id = Identity();
        var attempts = 0;
        var first = await ReceiverActivation.StartOrActivateAsync(() =>
        {
            if (Interlocked.Increment(ref attempts) <= failures) throw new InvalidOperationException("Host not ready");
        }, applicationId: id);
        using var owner = first.Instance;

        var next = await ReceiverActivation.StartOrActivateAsync(() => { }, applicationId: id);

        Assert.Equal(ReceiverActivationStatus.ActivatedExisting, next.Status);
        Assert.Equal(failures + 1, Volatile.Read(ref attempts));
    }

    [Fact]
    public async Task ClientDisconnectingBeforeCommandDoesNotPoisonListener()
    {
        var id = Identity();
        var shown = 0;
        var first = await ReceiverActivation.StartOrActivateAsync(() => Interlocked.Increment(ref shown), applicationId: id);
        using var owner = first.Instance;
        var server = (NamedPipeServerStream)typeof(ReceiverActivation)
            .GetField("_pipe", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        using (var disconnected = new NamedPipeClientStream(".", Names(id).Pipe, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
        {
            await disconnected.ConnectAsync(1_000);
            await WaitUntilAsync(() => server.IsConnected);
            // Closing without SHOW forces the server's ReadAsync to return EOF/Broken, or
            // ConnectNamedPipe to report the peer closed before its completion was observed.
        }
        await WaitUntilAsync(() => !server.IsConnected);

        var next = await ReceiverActivation.StartOrActivateAsync(() => { }, applicationId: id);

        Assert.Equal(ReceiverActivationStatus.ActivatedExisting, next.Status);
        Assert.Equal(1, Volatile.Read(ref shown));
    }

    [Fact]
    public async Task ClientDisconnectingBeforeAcknowledgementDoesNotPoisonListener()
    {
        var id = Identity();
        var shown = 0;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var clientClosed = new ManualResetEventSlim();
        var first = await ReceiverActivation.StartOrActivateAsync(() =>
        {
            if (Interlocked.Increment(ref shown) != 1) return;
            entered.TrySetResult();
            // A bounded test gate makes the client close immediately before the server's
            // ACK write. Application callbacks still only queue UI work and never wait.
            if (!clientClosed.Wait(TimeSpan.FromSeconds(2))) throw new TimeoutException();
        }, applicationId: id);
        using var owner = first.Instance;
        var server = (NamedPipeServerStream)typeof(ReceiverActivation)
            .GetField("_pipe", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(owner)!;
        using (var disconnected = new NamedPipeClientStream(".", Names(id).Pipe, PipeDirection.InOut,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly))
        {
            await disconnected.ConnectAsync(1_000);
            await disconnected.WriteAsync("SHOW"u8.ToArray());
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
        clientClosed.Set();
        await WaitUntilAsync(() => !server.IsConnected);

        var next = await ReceiverActivation.StartOrActivateAsync(() => { }, applicationId: id);

        Assert.Equal(ReceiverActivationStatus.ActivatedExisting, next.Status);
        Assert.Equal(2, Volatile.Read(ref shown));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        while (!condition()) await Task.Delay(1, deadline.Token);
    }

    private static (string Mutex, string Pipe) Names(string id) =>
        ((string Mutex, string Pipe))typeof(ReceiverActivation).GetMethod("Names", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { id })!;

    private sealed class MutexHolder : IDisposable
    {
        private readonly ManualResetEventSlim _ready = new();
        private readonly ManualResetEventSlim _release = new();
        private readonly Thread _thread;

        public MutexHolder(string name)
        {
            _thread = new Thread(() =>
            {
                using var mutex = new Mutex(false, name);
                mutex.WaitOne();
                _ready.Set();
                _release.Wait();
                mutex.ReleaseMutex();
            }) { IsBackground = true };
            _thread.Start();
            Assert.True(_ready.Wait(TimeSpan.FromSeconds(3)), "Test incumbent failed to acquire mutex.");
        }

        public void Dispose()
        {
            _release.Set();
            Assert.True(_thread.Join(TimeSpan.FromSeconds(3)), "Test incumbent failed to release mutex.");
            _ready.Dispose();
            _release.Dispose();
        }
    }
}
