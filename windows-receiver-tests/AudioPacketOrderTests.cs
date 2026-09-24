using Xunit;

namespace PocketMicReceiver.Tests;

public sealed class AudioPacketOrderTests
{
    private static AudioPacketInfo Packet(
        ulong session = 10,
        uint sequence = 0,
        AudioPipeline.Codec codec = AudioPipeline.Codec.Opus) =>
        new(session, sequence, AudioPipeline.SampleRate, codec, PayloadLength: 2);

    [Fact]
    public void DuplicateAndLatePacketsAreRejectedWithoutChangingAcceptedOrder()
    {
        var order = new AudioPacketOrder();
        var first = Packet(sequence: 0);

        var initial = order.Inspect(first);
        Assert.Equal(AudioPacketOrderStatus.Accept, initial.Status);
        Assert.True(initial.NewSession);
        order.Commit(first);

        var duplicate = order.Inspect(first);
        Assert.Equal(AudioPacketOrderStatus.Late, duplicate.Status);
        Assert.Equal(-1, duplicate.SequenceDelta);

        var gap = order.Inspect(Packet(sequence: 2));
        Assert.Equal(AudioPacketOrderStatus.Accept, gap.Status);
        Assert.Equal(1, gap.MissingPackets);

        order.Commit(Packet(sequence: 2));
        Assert.Equal(AudioPacketOrderStatus.Late, order.Inspect(Packet(sequence: 1)).Status);
    }

    [Fact]
    public void CodecChangeWithinSessionIsRejectedButNewSessionMayChangeCodec()
    {
        var order = new AudioPacketOrder();
        var opus = Packet(sequence: 0, codec: AudioPipeline.Codec.Opus);
        order.Commit(opus);

        var sameSessionPcm = order.Inspect(Packet(sequence: 1, codec: AudioPipeline.Codec.Pcm));
        Assert.Equal(AudioPacketOrderStatus.CodecChangedWithinSession, sameSessionPcm.Status);

        var newSessionPcm = order.Inspect(Packet(session: 11, sequence: 0, codec: AudioPipeline.Codec.Pcm));
        Assert.Equal(AudioPacketOrderStatus.Accept, newSessionPcm.Status);
        Assert.True(newSessionPcm.NewSession);
    }

    [Fact]
    public void SequenceDeltaRemainsContinuousAcrossRollover()
    {
        var order = new AudioPacketOrder();
        var last = Packet(sequence: uint.MaxValue);
        order.Commit(last);

        var next = order.Inspect(Packet(sequence: 0));
        Assert.Equal(AudioPacketOrderStatus.Accept, next.Status);
        Assert.Equal(0, next.MissingPackets);
    }

    [Fact]
    public void ResetMakesTheNextAuthenticatedPacketANewSession()
    {
        var order = new AudioPacketOrder();
        var packet = Packet();
        order.Commit(packet);
        order.Reset();

        var decision = order.Inspect(Packet(session: 99));
        Assert.Equal(AudioPacketOrderStatus.Accept, decision.Status);
        Assert.True(decision.NewSession);
    }
}
