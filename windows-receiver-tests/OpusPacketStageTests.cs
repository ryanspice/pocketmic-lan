using PocketMicReceiver;
using Xunit;

namespace PocketMicReceiver.Tests;

public class OpusPacketStageTests
{
    private sealed class Decoder : IOpusDecoder
    {
        public readonly List<string> Calls = new();
        public Decoder? Candidate;
        public bool Succeeds = true;
        public bool Disposed;
        public IOpusDecoder CreateFresh() { Calls.Add("fresh"); return Candidate = new Decoder { Succeeds = Succeeds }; }
        public IOpusDecoder CreateCopy() { Calls.Add("copy"); return Candidate = new Decoder { Succeeds = Succeeds }; }
        public bool TryDecode(byte[] data, int length, byte[] output) { Calls.Add("decode"); return Succeeds; }
        public bool TryGeneratePlc(byte[] output) { Calls.Add("plc"); return true; }
        public void Reset() => Calls.Add("reset");
        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void MissingFramesAreConcealedBeforeNextDecodeOnACopy()
    {
        using var active = new Decoder();
        Assert.True(OpusPacketStage.TryDecode(active, false, 2, new byte[1], 1, new byte[960],
            new byte[960], true, out var candidate, out var frames));
        using var committed = candidate;
        Assert.Equal(new[] { "copy" }, active.Calls);
        Assert.Equal(new[] { "plc", "plc", "decode" }, active.Candidate!.Calls);
        Assert.Equal(2, frames!.Length);
        Assert.False(active.Disposed);
    }

    [Fact]
    public void FailedGapOrSessionCandidateCannotMutateActivePredictor()
    {
        foreach (var fresh in new[] { true, false })
        {
            using var active = new Decoder { Succeeds = false };
            Assert.False(OpusPacketStage.TryDecode(active, fresh, 2, new byte[1], 1, new byte[960],
                new byte[960], true, out var candidate, out var frames));
            Assert.Null(candidate);
            Assert.Null(frames);
            Assert.True(active.Candidate!.Disposed);
            Assert.Equal(new[] { fresh ? "fresh" : "copy" }, active.Calls);
            Assert.False(active.Disposed);
        }
    }

    [Fact]
    public void ResyncStartsFreshAndDoesNotGenerateOldSessionConcealment()
    {
        using var active = new Decoder();
        Assert.True(OpusPacketStage.TryDecode(active, true, 500, new byte[1], 1, new byte[960],
            new byte[960], true, out var candidate, out var frames));
        using var committed = candidate;
        Assert.Equal(new[] { "decode" }, active.Candidate!.Calls);
        Assert.Null(frames);
    }

    [Fact]
    public void NativeCopyHasIndependentLifetimeAndEquivalentPrediction()
    {
        var active = new OpusDecoder();
        var warmup = new byte[960];
        Assert.True(active.TryGeneratePlc(warmup));
        using var copy = active.CreateCopy();
        var first = new byte[960];
        var second = new byte[960];
        Assert.True(active.TryGeneratePlc(first));
        Assert.True(copy.TryGeneratePlc(second));
        Assert.Equal(first, second);
        active.Dispose();
        Assert.True(copy.TryGeneratePlc(second));
    }
}
