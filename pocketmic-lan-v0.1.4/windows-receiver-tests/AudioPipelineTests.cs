using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace PocketMicReceiver.Tests;

/// <summary>
/// Packet validation, sequencing and concealment tests. Every rejection path is covered
/// separately because they are not interchangeable: a length or header rejection means
/// unrelated traffic, whereas an authentication failure means the pairing key is wrong, and
/// collapsing the two produces exactly the wrong diagnosis for the user.
/// </summary>
public class AudioPipelineTests
{
    private const ulong SessionId = 0x0102030405060708UL;

    private static byte[] Key(string pairingKey) => SHA256.HashData(Encoding.UTF8.GetBytes(pairingKey));

    private static byte[] SamplePcm(short amplitude = 8_000)
    {
        var pcm = new byte[AudioPipeline.PacketPcmBytes];
        var samples = MemoryMarshal.Cast<byte, short>(pcm);
        for (var index = 0; index < samples.Length; index++)
        {
            samples[index] = (short)(index % 2 == 0 ? amplitude : -amplitude);
        }

        return pcm;
    }

    /// <summary>Builds the datagram the phone would send, byte for byte.</summary>
    private static byte[] Datagram(byte[] key, uint sequence, byte[] pcm)
    {
        var datagram = new byte[AudioPipeline.DatagramBytes];
        var span = datagram.AsSpan();
        "PMIC"u8.CopyTo(span);
        span[4] = AudioPipeline.ProtocolVersion;
        span[5] = AudioPipeline.FlagEncrypted;
        BinaryPrimitives.WriteUInt16BigEndian(span.Slice(6, 2), (ushort)AudioPipeline.HeaderSize);
        BinaryPrimitives.WriteUInt64BigEndian(span.Slice(8, 8), SessionId);
        BinaryPrimitives.WriteUInt32BigEndian(span.Slice(16, 4), sequence);
        BinaryPrimitives.WriteInt32BigEndian(span.Slice(20, 4), AudioPipeline.SampleRate);

        var nonce = new byte[12];
        BinaryPrimitives.WriteUInt64BigEndian(nonce.AsSpan(0, 8), SessionId);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(8, 4), sequence);

        using var aes = new AesGcm(key, AudioPipeline.TagSize);
        aes.Encrypt(
            nonce,
            pcm,
            span.Slice(AudioPipeline.HeaderSize, AudioPipeline.PacketPcmBytes),
            span.Slice(AudioPipeline.HeaderSize + AudioPipeline.PacketPcmBytes, AudioPipeline.TagSize),
            span[..AudioPipeline.HeaderSize]);
        return datagram;
    }

    private static bool Decrypt(byte[] key, byte[] datagram, out byte[] pcm)
    {
        using var aes = new AesGcm(key, AudioPipeline.TagSize);
        pcm = new byte[AudioPipeline.PacketPcmBytes];
        return AudioPipeline.TryDecrypt(aes, datagram, new byte[12], pcm, out _, out _, out _);
    }

    [Fact]
    public void DatagramBytesIsTheHeaderPayloadAndTagCombined()
    {
        Assert.Equal(1_000, AudioPipeline.DatagramBytes);
        Assert.Equal(
            AudioPipeline.HeaderSize + AudioPipeline.PacketPcmBytes + AudioPipeline.TagSize,
            AudioPipeline.DatagramBytes);
    }

    [Fact]
    public void TryDecryptAcceptsAWellFormedPacketAndRecoversItsHeader()
    {
        var key = Key("POCKETMIC-TEST");
        var pcm = SamplePcm();
        var datagram = Datagram(key, 0x11223344, pcm);
        var output = new byte[AudioPipeline.PacketPcmBytes];

        using var aes = new AesGcm(key, AudioPipeline.TagSize);
        var accepted = AudioPipeline.TryDecrypt(
            aes, datagram, new byte[12], output, out var sessionId, out var sequence, out var sampleRate);

        Assert.True(accepted);
        Assert.Equal(SessionId, sessionId);
        Assert.Equal(0x11223344u, sequence);
        Assert.Equal(AudioPipeline.SampleRate, sampleRate);
        Assert.Equal(pcm, output);
    }

    [Fact]
    public void TryDecryptRejectsADatagramOfTheWrongLength()
    {
        var key = Key("POCKETMIC-TEST");
        var datagram = Datagram(key, 1, SamplePcm());

        Assert.False(Decrypt(key, datagram.AsSpan(0, AudioPipeline.DatagramBytes - 1).ToArray(), out _));
        Assert.False(Decrypt(key, new byte[AudioPipeline.DatagramBytes + 1], out _));
        Assert.False(Decrypt(key, Array.Empty<byte>(), out _));
    }

    [Fact]
    public void TryDecryptRejectsADatagramWithoutTheMagic()
    {
        var key = Key("POCKETMIC-TEST");
        var datagram = Datagram(key, 1, SamplePcm());
        datagram[0] = (byte)'X';

        Assert.False(Decrypt(key, datagram, out _));
    }

    [Fact]
    public void TryDecryptRejectsAFutureProtocolVersionBeforeTouchingTheAead()
    {
        var key = Key("POCKETMIC-TEST");
        var datagram = Datagram(key, 1, SamplePcm());
        datagram[4] = AudioPipeline.ProtocolVersion + 1;

        Assert.False(Decrypt(key, datagram, out _));
    }

    /// <summary>
    /// Rejecting a future version before the AEAD is what stops it being misreported as a
    /// pairing-key failure. Being able to read the version out afterwards is what stops it being
    /// misreported as nothing at all — "rejected 6,000 datagrams" reads as a key mismatch to
    /// everyone who sees it, so the receiver has to be able to say which it was.
    /// </summary>
    [Fact]
    public void AFutureProtocolVersionCanStillBeReadBackAfterBeingRejected()
    {
        var key = Key("POCKETMIC-TEST");
        var datagram = Datagram(key, 1, SamplePcm());
        datagram[4] = AudioPipeline.ProtocolVersion + 1;

        Assert.False(Decrypt(key, datagram, out _));
        Assert.True(AudioPipeline.TryPeekProtocolVersion(datagram, out var version));
        Assert.Equal(AudioPipeline.ProtocolVersion + 1, version);
    }

    [Fact]
    public void PeekingTheProtocolVersionIgnoresTrafficThatIsNotOurs()
    {
        var key = Key("POCKETMIC-TEST");
        var datagram = Datagram(key, 1, SamplePcm());
        datagram[0] = (byte)'X';

        Assert.False(AudioPipeline.TryPeekProtocolVersion(datagram, out _));
        Assert.False(AudioPipeline.TryPeekProtocolVersion(new byte[3], out _));
    }

    [Fact]
    public void TryDecryptRejectsAPacketWithTheEncryptedFlagCleared()
    {
        var key = Key("POCKETMIC-TEST");
        var datagram = Datagram(key, 1, SamplePcm());
        datagram[5] &= unchecked((byte)~AudioPipeline.FlagEncrypted);

        Assert.False(Decrypt(key, datagram, out _));
    }

    [Fact]
    public void TryDecryptRejectsADeclaredHeaderLengthThatIsNotTheRealOne()
    {
        var key = Key("POCKETMIC-TEST");
        var datagram = Datagram(key, 1, SamplePcm());
        BinaryPrimitives.WriteUInt16BigEndian(datagram.AsSpan(6, 2), (ushort)(AudioPipeline.HeaderSize + 8));

        Assert.False(Decrypt(key, datagram, out _));
    }

    [Fact]
    public void TryDecryptRejectsAPacketEncryptedUnderADifferentPairingKey()
    {
        var datagram = Datagram(Key("POCKETMIC-OTHER"), 1, SamplePcm());

        Assert.False(Decrypt(Key("POCKETMIC-TEST"), datagram, out _));
    }

    [Fact]
    public void TryDecryptRejectsATamperedCiphertext()
    {
        var key = Key("POCKETMIC-TEST");
        var datagram = Datagram(key, 1, SamplePcm());
        datagram[AudioPipeline.HeaderSize]++;

        Assert.False(Decrypt(key, datagram, out _));
    }

    [Fact]
    public void SequenceDeltaIsZeroForThePacketThatWasExpectedNext()
    {
        Assert.Equal(0, AudioPipeline.SequenceDelta(5, 4));
    }

    [Fact]
    public void SequenceDeltaCountsThePacketsMissingFromAGap()
    {
        Assert.Equal(3, AudioPipeline.SequenceDelta(8, 4));
    }

    [Fact]
    public void SequenceDeltaIsNegativeForAReorderedOrDuplicatePacket()
    {
        Assert.Equal(-1, AudioPipeline.SequenceDelta(4, 4));
        Assert.Equal(-5, AudioPipeline.SequenceDelta(0, 4));
    }

    [Fact]
    public void SequenceDeltaIsContinuousAcrossTheUint32Rollover()
    {
        Assert.Equal(0, AudioPipeline.SequenceDelta(0, uint.MaxValue));
        Assert.Equal(1, AudioPipeline.SequenceDelta(1, uint.MaxValue));
        Assert.Equal(1, AudioPipeline.SequenceDelta(0, uint.MaxValue - 1));
        Assert.Equal(3, AudioPipeline.SequenceDelta(2, uint.MaxValue - 1));
    }

    [Fact]
    public void SequenceDeltaStaysNegativeForALatePacketFromBeforeTheRollover()
    {
        Assert.Equal(-2, AudioPipeline.SequenceDelta(uint.MaxValue, 0));
        Assert.Equal(-1, AudioPipeline.SequenceDelta(uint.MaxValue, uint.MaxValue));
    }

    [Fact]
    public void ConcealmentClearsTheFrameWhenThereIsNoPreviousFrameToRepeat()
    {
        var target = SamplePcm();
        var lastGood = SamplePcm();

        AudioPipeline.BuildConcealmentFrame(target, lastGood, haveLastGood: false, gapIndex: 0);

        Assert.All(target, value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void ConcealmentRepeatsTheLastGoodFrameAtAReducedLevel()
    {
        var lastGood = SamplePcm(10_000);
        var target = new byte[AudioPipeline.PacketPcmBytes];

        AudioPipeline.BuildConcealmentFrame(target, lastGood, haveLastGood: true, gapIndex: 0);

        // One packet into a gap the repeat is played at roughly 95 percent of the original level.
        var level = (int)MemoryMarshal.Cast<byte, short>(target)[0];
        Assert.InRange(level, 9_400, 9_600);
    }

    [Fact]
    public void ConcealmentFadesMonotonicallyAsTheGapGrows()
    {
        var lastGood = SamplePcm(10_000);
        var target = new byte[AudioPipeline.PacketPcmBytes];
        var previous = int.MaxValue;

        for (var gapIndex = 0; gapIndex < AudioPipeline.MaxConcealedGapPackets; gapIndex++)
        {
            AudioPipeline.BuildConcealmentFrame(target, lastGood, haveLastGood: true, gapIndex);
            var level = MemoryMarshal.Cast<byte, short>(target)[0];
            Assert.True(level < previous, $"gap {gapIndex} did not fade below the previous frame");
            previous = level;
        }
    }

    [Fact]
    public void ConcealmentReachesDigitalSilenceAtTheMaximumGap()
    {
        var lastGood = SamplePcm(10_000);
        var target = SamplePcm(10_000);

        AudioPipeline.BuildConcealmentFrame(
            target, lastGood, haveLastGood: true, gapIndex: AudioPipeline.MaxConcealedGapPackets - 1);

        Assert.All(target, value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void ConcealmentStaysSilentPastTheMaximumGap()
    {
        var lastGood = SamplePcm(10_000);
        var target = SamplePcm(10_000);

        AudioPipeline.BuildConcealmentFrame(
            target, lastGood, haveLastGood: true, gapIndex: AudioPipeline.MaxConcealedGapPackets + 50);

        Assert.All(target, value => Assert.Equal((byte)0, value));
    }

    [Fact]
    public void SmoothedRmsRisesTowardsALoudFrameAndFallsBackTowardsSilence()
    {
        var loud = SamplePcm(16_000);
        var silence = new byte[AudioPipeline.PacketPcmBytes];

        var rising = AudioPipeline.SmoothedRms(0f, loud);
        var falling = AudioPipeline.SmoothedRms(rising, silence);

        Assert.True(rising > 0f);
        Assert.True(falling < rising);
        Assert.True(falling > 0f);
    }

    [Fact]
    public void TrimIsRefusedWhileTheInputIsLoudAndTheBacklogIsBelowTheCeiling()
    {
        Assert.False(AudioPipeline.ShouldTrim(261, 260, 0.5f));
        Assert.False(AudioPipeline.ShouldTrim(419, 260, 0.5f));
    }

    [Fact]
    public void TrimIsAllowedOnceTheBacklogPassesTheHardCeilingEvenDuringSpeech()
    {
        Assert.True(AudioPipeline.ShouldTrim(421, 260, 0.5f));
    }

    [Fact]
    public void TrimIsAllowedDuringAQuietPassageAboveTheHighWaterMark()
    {
        Assert.True(AudioPipeline.ShouldTrim(261, 260, AudioPipeline.TrimSilenceRms / 2f));
    }

    [Fact]
    public void TrimIsRefusedWhileTheBacklogIsAtOrBelowTheHighWaterMark()
    {
        Assert.False(AudioPipeline.ShouldTrim(260, 260, 0f));
        Assert.False(AudioPipeline.ShouldTrim(100, 260, 0f));
    }

    [Fact]
    public void TrimIsRefusedWhenTheLevelIsExactlyAtTheQuietThreshold()
    {
        Assert.False(AudioPipeline.ShouldTrim(300, 260, AudioPipeline.TrimSilenceRms));
    }

    [Fact]
    public void HighWaterIsAtLeastAHundredAndTwentyMillisecondsAboveThePrebuffer()
    {
        Assert.Equal(160, AudioPipeline.HighWaterFor(40));
        Assert.Equal(220, AudioPipeline.HighWaterFor(100));
        Assert.Equal(400, AudioPipeline.HighWaterFor(200));

        for (var prebuffer = 40; prebuffer <= 300; prebuffer += 10)
        {
            Assert.True(AudioPipeline.HighWaterFor(prebuffer) >= prebuffer + 120);
        }
    }
}
