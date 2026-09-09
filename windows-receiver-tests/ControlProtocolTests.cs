using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace PocketMicReceiver.Tests;

/// <summary>
/// Wire-format and authentication tests for the control channel. These are the receiver half
/// of a contract the phone implements independently in Kotlin, so the byte offsets and the
/// quantisation of every config field are asserted literally rather than through the encoder.
/// </summary>
public class ControlProtocolTests
{
    private static readonly byte[] Key = ControlProtocol.DeriveControlKey("POCKETMIC-TEST");
    private static readonly byte[] OtherKey = ControlProtocol.DeriveControlKey("POCKETMIC-OTHER");

    private static byte[] Nonce()
    {
        var nonce = new byte[ControlProtocol.NonceSize];
        for (var index = 0; index < nonce.Length; index++) nonce[index] = (byte)((index * 7) + 1);
        return nonce;
    }

    [Fact]
    public void ControlPortSitsOnePortAboveTheAudioStream()
    {
        Assert.Equal(49_501, ControlProtocol.ControlPort(49_500));
    }

    [Fact]
    public void HighestAudioPortLeavesTheFinalUdpPortForControl()
    {
        Assert.Equal(65_534, ControlProtocol.MaxAudioPort);
        Assert.Equal(65_535, ControlProtocol.ControlPort(ControlProtocol.MaxAudioPort));
    }

    [Fact]
    public void DeriveControlKeyIsDeterministicForTheSamePairingKey()
    {
        Assert.Equal(Key, ControlProtocol.DeriveControlKey("POCKETMIC-TEST"));
        Assert.Equal(32, Key.Length);
    }

    [Fact]
    public void DeriveControlKeyDiffersBetweenPairingKeys()
    {
        Assert.NotEqual(Key, OtherKey);
    }

    [Fact]
    public void DeriveControlKeyIsDomainSeparatedFromTheAudioKey()
    {
        var audioKey = SHA256.HashData(Encoding.UTF8.GetBytes("POCKETMIC-TEST"));
        var expected = HMACSHA256.HashData(audioKey, "pocketmic-control-v1"u8.ToArray());

        Assert.NotEqual(audioKey, Key);
        Assert.Equal(expected, Key);
    }

    [Fact]
    public void BuildAndParseRoundTripPreservesTypeAndPayload()
    {
        var payload = Nonce();

        var frame = ControlProtocol.Build(Key, ControlProtocol.TypeProbe, payload);

        Assert.True(ControlProtocol.TryParse(frame, out var type, out var parsed));
        Assert.Equal(ControlProtocol.TypeProbe, type);
        Assert.Equal(payload, parsed);
    }

    /// <summary>
    /// Deliberate: a receiver that answered with the wrong key must be distinguishable from no
    /// receiver at all, so the structural parse succeeds and authentication is a separate step.
    /// </summary>
    [Fact]
    public void ParseSucceedsStructurallyEvenWhenTheHmacIsWrong()
    {
        var payload = Nonce();
        var frame = ControlProtocol.Build(OtherKey, ControlProtocol.TypeAnnounce, payload);

        Assert.True(ControlProtocol.TryParse(frame, out var type, out var parsed));
        Assert.Equal(ControlProtocol.TypeAnnounce, type);
        Assert.Equal(payload, parsed);
        Assert.False(ControlProtocol.Verify(Key, frame));
    }

    [Fact]
    public void ParseRejectsAFrameOfTheWrongLength()
    {
        Assert.False(ControlProtocol.TryParse(new byte[ControlProtocol.FrameSize - 1], out _, out _));
        Assert.False(ControlProtocol.TryParse(new byte[ControlProtocol.FrameSize + 1], out _, out _));
    }

    [Fact]
    public void ParseRejectsForeignTrafficWithoutTheMagic()
    {
        var frame = ControlProtocol.Build(Key, ControlProtocol.TypeProbe, Nonce());
        frame[0] = (byte)'X';

        Assert.False(ControlProtocol.TryParse(frame, out _, out _));
    }

    [Fact]
    public void ParseRejectsAnUnknownProtocolVersion()
    {
        var frame = ControlProtocol.Build(Key, ControlProtocol.TypeProbe, Nonce());
        frame[5] = 2;

        Assert.False(ControlProtocol.TryParse(frame, out _, out _));
    }

    [Fact]
    public void ParseRejectsADeclaredPayloadLongerThanTheFrame()
    {
        var frame = ControlProtocol.Build(Key, ControlProtocol.TypeProbe, Nonce());
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(8, 2), (ushort)(ControlProtocol.MaxPayload + 1));

        Assert.False(ControlProtocol.TryParse(frame, out _, out _));
    }

    [Fact]
    public void VerifyAcceptsAFrameSignedWithTheSameKey()
    {
        var frame = ControlProtocol.Build(Key, ControlProtocol.TypeStats, new byte[] { 1, 2, 3 });

        Assert.True(ControlProtocol.Verify(Key, frame));
    }

    [Fact]
    public void VerifyRejectsAFrameSignedWithADifferentPairingKey()
    {
        var frame = ControlProtocol.Build(OtherKey, ControlProtocol.TypeStats, new byte[] { 1, 2, 3 });

        Assert.False(ControlProtocol.Verify(Key, frame));
    }

    [Fact]
    public void VerifyRejectsAFrameWhosePayloadWasTampered()
    {
        var frame = ControlProtocol.Build(Key, ControlProtocol.TypeConfig, new byte[] { 1, 2, 3, 4, 5, 6, 7 });
        frame[ControlProtocol.HeaderSize]++;

        Assert.False(ControlProtocol.Verify(Key, frame));
    }

    [Fact]
    public void VerifyRejectsAFrameWhoseTagWasTampered()
    {
        var frame = ControlProtocol.Build(Key, ControlProtocol.TypeStats, new byte[] { 9 });
        frame[ControlProtocol.SignedSize]++;

        Assert.False(ControlProtocol.Verify(Key, frame));
    }

    [Fact]
    public void VerifyRejectsAFrameOfTheWrongLength()
    {
        Assert.False(ControlProtocol.Verify(Key, new byte[ControlProtocol.FrameSize - 1]));
    }

    [Fact]
    public void EveryFrameIsPaddedToTheFixedSizeSoAReplyCanNeverAmplify()
    {
        var probe = ControlProtocol.Build(Key, ControlProtocol.TypeProbe, Nonce());
        var announce = ControlProtocol.Build(
            Key,
            ControlProtocol.TypeAnnounce,
            ControlProtocol.AnnouncePayload(Nonce(), 49_500, "RYAN-DESKTOP"));
        var stats = ControlProtocol.Build(
            Key,
            ControlProtocol.TypeStats,
            ControlProtocol.StatsPayload(1, 2, 3, 4, 5, 137, playing: true));

        Assert.Equal(ControlProtocol.FrameSize, probe.Length);
        Assert.Equal(ControlProtocol.FrameSize, announce.Length);
        Assert.Equal(ControlProtocol.FrameSize, stats.Length);
        Assert.True(announce.Length <= probe.Length);
        Assert.True(stats.Length <= probe.Length);
    }

    [Fact]
    public void AnnouncePayloadForTheLongestPossibleHostNameStillFitsInOneFrame()
    {
        // 63 UTF-16 units is the truncation limit and no character costs more than three UTF-8
        // bytes per unit, so the worst case is a fully multi-byte name rather than an ASCII one.
        var widest = new string((char)0x4E2D, 200);

        var payload = ControlProtocol.AnnouncePayload(Nonce(), 49_500, widest);
        var frame = ControlProtocol.Build(Key, ControlProtocol.TypeAnnounce, payload);

        Assert.True(payload.Length <= ControlProtocol.MaxPayload);
        Assert.Equal(ControlProtocol.FrameSize, frame.Length);
    }

    [Fact]
    public void BuildRejectsPayloadLargerThanTheFrameCanCarry()
    {
        Assert.Throws<ArgumentException>(() =>
            ControlProtocol.Build(Key, ControlProtocol.TypeConfig, new byte[ControlProtocol.MaxPayload + 1]));
    }

    [Fact]
    public void BuildAcceptsAPayloadOfExactlyMaxPayload()
    {
        var frame = ControlProtocol.Build(Key, ControlProtocol.TypeConfig, new byte[ControlProtocol.MaxPayload]);

        Assert.Equal(ControlProtocol.FrameSize, frame.Length);
        Assert.True(ControlProtocol.TryParse(frame, out _, out var parsed));
        Assert.Equal(ControlProtocol.MaxPayload, parsed.Length);
    }

    [Fact]
    public void AnnouncePayloadRoundTripsNoncePortAndHostName()
    {
        var nonce = Nonce();

        var payload = ControlProtocol.AnnouncePayload(nonce, 49_500, "RYAN-DESKTOP");

        var nameLength = (int)payload[ControlProtocol.NonceSize + 2];
        Assert.Equal(nonce, payload.AsSpan(0, ControlProtocol.NonceSize).ToArray());
        Assert.Equal(
            49_500,
            (int)BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(ControlProtocol.NonceSize, 2)));
        Assert.Equal("RYAN-DESKTOP".Length, nameLength);
        Assert.Equal(
            "RYAN-DESKTOP",
            Encoding.UTF8.GetString(payload, ControlProtocol.NonceSize + 3, nameLength));
    }

    [Fact]
    public void TryReadConfigRejectsAPayloadShorterThanSevenBytes()
    {
        Assert.False(ControlProtocol.TryReadConfig(new byte[6], out _));
        Assert.False(ControlProtocol.TryReadConfig(Array.Empty<byte>(), out _));
    }

    [Fact]
    public void TryReadConfigAcceptsExactlySevenBytes()
    {
        Assert.True(ControlProtocol.TryReadConfig(new byte[7], out var config));
        Assert.False(config.Enabled);
        Assert.Equal(0, config.HighPassHz);
    }

    [Fact]
    public void TryReadConfigIgnoresTrailingBytesFromANewerSender()
    {
        var payload = new byte[7 + 8];
        payload[0] = 1;
        payload[1] = 21;

        Assert.True(ControlProtocol.TryReadConfig(payload, out var config));
        Assert.True(config.Enabled);
        Assert.Equal(84, config.HighPassHz);
    }

    [Fact]
    public void TryReadConfigDecodesTheQuantisationThePhoneApplies()
    {
        // Exactly the bytes ControlProtocol.configPayload emits for the shipped defaults.
        var payload = new byte[] { 1, 21, 60, 55, 35, 70, 40 };

        Assert.True(ControlProtocol.TryReadConfig(payload, out var config));
        Assert.True(config.Enabled);
        Assert.Equal(84, config.HighPassHz);
        Assert.Equal(0.60, config.Gate, 5);
        Assert.Equal(0.55, config.Compressor, 5);
        Assert.Equal(3.5, config.PresenceDb, 5);
        Assert.Equal(0.70, config.Makeup, 5);
        Assert.Equal(0.40, config.NoiseReduction, 5);
    }

    [Fact]
    public void TryReadConfigDecodesTheTopOfEveryByteField()
    {
        var payload = new byte[] { 0, 255, 100, 100, 200, 100, 100 };

        Assert.True(ControlProtocol.TryReadConfig(payload, out var config));
        Assert.False(config.Enabled);
        Assert.Equal(1_020, config.HighPassHz);
        Assert.Equal(1.0, config.Gate, 5);
        Assert.Equal(20.0, config.PresenceDb, 5);
    }

    /// <summary>
    /// The version trailer sits after the variable-length name, so a receiver whose machine name
    /// is long and multi-byte must not push it out of the frame — that would turn version
    /// negotiation off for exactly the machines whose names are unusual.
    /// </summary>
    [Fact]
    public void TheVersionTrailerSurvivesTheLongestPossibleHostName()
    {
        var widest = new string((char)0x4E2D, 200);

        var payload = ControlProtocol.AnnouncePayload(
            Nonce(), 49_500, widest, AudioPipeline.ProtocolVersion, ReceiverFrontEnd.Modern, "0.1.3");
        var frame = ControlProtocol.Build(Key, ControlProtocol.TypeAnnounce, payload);

        Assert.True(payload.Length <= ControlProtocol.MaxPayload);
        Assert.Equal(ControlProtocol.FrameSize, frame.Length);
        Assert.True(ControlProtocol.TryReadAnnounce(payload, out var announce));
        Assert.True(announce.HasVersions);
        Assert.Equal(ReceiverFrontEnd.Modern, announce.FrontEnd);
        Assert.Equal("0.1.3", announce.BuildVersion);
    }

    [Fact]
    public void AnnounceRoundTripsTheProtocolVersionsFrontEndAndBuild()
    {
        var payload = ControlProtocol.AnnouncePayload(
            Nonce(), 49_500, "RYAN-DESKTOP", 7, ReceiverFrontEnd.Classic, "0.1.2");

        Assert.True(ControlProtocol.TryReadAnnounce(payload, out var announce));
        Assert.Equal(49_500, announce.AudioPort);
        Assert.Equal("RYAN-DESKTOP", announce.HostName);
        Assert.True(announce.HasVersions);
        Assert.Equal(7, announce.AudioProtocolVersion);
        Assert.Equal(ControlProtocol.Version, announce.ControlProtocolVersion);
        Assert.Equal(ReceiverFrontEnd.Classic, announce.FrontEnd);
        Assert.Equal("0.1.2", announce.BuildVersion);
    }

    /// <summary>
    /// Byte offsets asserted literally rather than through the decoder, because the phone
    /// implements this layout independently in Kotlin and a shared encoder would let both drift
    /// together without a single test noticing.
    /// </summary>
    [Fact]
    public void TheVersionTrailerSitsImmediatelyAfterTheHostName()
    {
        var payload = ControlProtocol.AnnouncePayload(
            Nonce(), 49_500, "PC", 1, ReceiverFrontEnd.Modern, "0.1.3");

        var trailer = ControlProtocol.NonceSize + 3 + 2;
        Assert.Equal(2, (int)payload[ControlProtocol.NonceSize + 2]);
        Assert.Equal(1, (int)payload[trailer]);
        Assert.Equal(ControlProtocol.Version, payload[trailer + 1]);
        Assert.Equal((byte)ReceiverFrontEnd.Modern, payload[trailer + 2]);
        Assert.Equal(5, (int)payload[trailer + 3]);
        Assert.Equal("0.1.3"u8.ToArray(), payload.AsSpan(trailer + 4, 5).ToArray());
        Assert.Equal(trailer + 4 + 5, payload.Length);
    }

    /// <summary>
    /// A receiver built before the trailer existed announces without it. Reporting an invented
    /// version for that receiver would be worse than reporting none: the phone would compare
    /// against a number nobody ever sent.
    /// </summary>
    [Fact]
    public void AnAnnounceWithoutAVersionTrailerStillDecodesAndReportsNoVersions()
    {
        var full = ControlProtocol.AnnouncePayload(Nonce(), 49_500, "OLD-PC");
        var legacy = full.AsSpan(0, ControlProtocol.NonceSize + 3 + 6).ToArray();

        Assert.True(ControlProtocol.TryReadAnnounce(legacy, out var announce));
        Assert.Equal(49_500, announce.AudioPort);
        Assert.Equal("OLD-PC", announce.HostName);
        Assert.False(announce.HasVersions);
        Assert.Equal(0, announce.AudioProtocolVersion);
        Assert.Equal(ReceiverFrontEnd.Unknown, announce.FrontEnd);
        Assert.Equal(string.Empty, announce.BuildVersion);
    }

    [Fact]
    public void TryReadAnnounceRejectsATruncatedPayload()
    {
        var payload = ControlProtocol.AnnouncePayload(Nonce(), 49_500, "PC");

        Assert.False(ControlProtocol.TryReadAnnounce(new byte[ControlProtocol.NonceSize + 2], out _));
        Assert.False(ControlProtocol.TryReadAnnounce(
            payload.AsSpan(0, ControlProtocol.NonceSize + 3).ToArray(), out _));
    }

    /// <summary>
    /// Same discipline as <c>AudioPipeline.TryDecrypt</c>: refuse the frame, but keep enough to
    /// say why, or a v2 peer is indistinguishable from no peer at all.
    /// </summary>
    [Fact]
    public void PeekingTheVersionWorksOnAFrameParseRefuses()
    {
        var frame = ControlProtocol.Build(Key, ControlProtocol.TypeAnnounce, Nonce());
        frame[5] = 9;

        Assert.False(ControlProtocol.TryParse(frame, out _, out _));
        Assert.True(ControlProtocol.TryPeekVersion(frame, frame.Length, out var version));
        Assert.Equal(9, version);
    }

    [Fact]
    public void PeekingTheVersionIgnoresTrafficThatIsNotOurs()
    {
        var frame = ControlProtocol.Build(Key, ControlProtocol.TypeProbe, Nonce());
        frame[0] = (byte)'X';

        Assert.False(ControlProtocol.TryPeekVersion(frame, frame.Length, out _));
        Assert.False(ControlProtocol.TryPeekVersion(new byte[8], 8, out _));
    }

    [Fact]
    public void StatsPayloadWritesFortyFiveBigEndianBytes()
    {
        var payload = ControlProtocol.StatsPayload(1_234_567, 89, 7, 3, 2, 137, playing: true);

        Assert.Equal(45, payload.Length);
        Assert.Equal(1_234_567L, BinaryPrimitives.ReadInt64BigEndian(payload.AsSpan(0, 8)));
        Assert.Equal(89L, BinaryPrimitives.ReadInt64BigEndian(payload.AsSpan(8, 8)));
        Assert.Equal(7L, BinaryPrimitives.ReadInt64BigEndian(payload.AsSpan(16, 8)));
        Assert.Equal(3L, BinaryPrimitives.ReadInt64BigEndian(payload.AsSpan(24, 8)));
        Assert.Equal(2L, BinaryPrimitives.ReadInt64BigEndian(payload.AsSpan(32, 8)));
        Assert.Equal(137, BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(40, 4)));
        Assert.Equal(1, (int)payload[44]);
    }

    [Fact]
    public void StatsPayloadFitsInOneControlFrame()
    {
        var payload = ControlProtocol.StatsPayload(long.MaxValue, 0, 0, 0, 0, int.MaxValue, playing: false);

        Assert.True(payload.Length <= ControlProtocol.MaxPayload);
        Assert.Equal(0, (int)payload[44]);
    }
}
