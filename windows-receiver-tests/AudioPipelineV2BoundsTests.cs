using System.Buffers.Binary;
using System.Security.Cryptography;
using Xunit;

namespace PocketMicReceiver.Tests;

public sealed class AudioPipelineV2BoundsTests
{
    private static readonly byte[] Key = new byte[32];

    private sealed class CountingDecoder : IOpusDecoder
    {
        public int DecodeCalls { get; private set; }

        public bool TryDecode(byte[] opusData, int length, byte[] pcmOutput)
        {
            DecodeCalls++;
            throw new InvalidOperationException("Rejected packet must not reach the decoder.");
        }

        public bool TryGeneratePlc(byte[] pcmOutput) => false;
        public void Reset() { }
        public void Dispose() { }
    }

    private sealed class CapturingDecoder : IOpusDecoder
    {
        public int DecodeCalls { get; private set; }
        public byte[]? EncodedPayload { get; private set; }

        public bool TryDecode(byte[] opusData, int length, byte[] pcmOutput)
        {
            DecodeCalls++;
            EncodedPayload = opusData.AsSpan(0, length).ToArray();
            Array.Fill(pcmOutput, (byte)0x5A, 0, AudioPipeline.PacketPcmBytes);
            return true;
        }

        public bool TryGeneratePlc(byte[] pcmOutput) => false;
        public void Reset() { }
        public void Dispose() { }
    }

    private static byte[] V2HeaderWithPayloadLength(int payloadLength, byte flags = 3, int actualPayloadLength = -1)
    {
        var actualLength = actualPayloadLength < 0 ? Math.Max(payloadLength, 0) : actualPayloadLength;
        var packet = new byte[AudioPipeline.HeaderSizeV2 + actualLength + AudioPipeline.TagSize];
        "PMIC"u8.CopyTo(packet);
        packet[4] = AudioPipeline.ProtocolVersionV2;
        packet[5] = flags;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(6, 2), AudioPipeline.HeaderSizeV2);
        BinaryPrimitives.WriteUInt64BigEndian(packet.AsSpan(8, 8), 7);
        BinaryPrimitives.WriteUInt32BigEndian(packet.AsSpan(16, 4), 9);
        BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(20, 4), AudioPipeline.SampleRate);
        BinaryPrimitives.WriteInt32BigEndian(packet.AsSpan(24, 4), payloadLength);
        return packet;
    }

    private static byte[] ValidEncryptedV2Packet(byte[] opusPayload)
    {
        var packet = V2HeaderWithPayloadLength(opusPayload.Length);
        var nonce = new byte[12];
        BinaryPrimitives.WriteUInt64BigEndian(nonce.AsSpan(0, 8), 7);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(8, 4), 9);
        using var aes = new AesGcm(Key, AudioPipeline.TagSize);
        aes.Encrypt(
            nonce,
            opusPayload,
            packet.AsSpan(AudioPipeline.HeaderSizeV2, opusPayload.Length),
            packet.AsSpan(AudioPipeline.HeaderSizeV2 + opusPayload.Length, AudioPipeline.TagSize),
            packet.AsSpan(0, AudioPipeline.HeaderSizeV2));
        return packet;
    }

    private static bool TryDecrypt(byte[] packet, CountingDecoder decoder, byte[]? opusBuffer = null)
    {
        using var aes = new AesGcm(Key, AudioPipeline.TagSize);
        return AudioPipeline.TryDecrypt(
            aes,
            packet,
            new byte[12],
            new byte[AudioPipeline.PacketPcmBytes],
            out _, out _, out _,
            decoder,
            opusBuffer ?? new byte[AudioPipeline.MaxOpusPayloadBytes]);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(513, 513)]
    [InlineData(1024, 1024)]
    public void RejectsInvalidV2PayloadLengthsWithoutThrowingOrCallingDecoder(int declaredLength, int actualLength)
    {
        var decoder = new CountingDecoder();
        var packet = V2HeaderWithPayloadLength(declaredLength, actualPayloadLength: actualLength);

        Assert.False(TryDecrypt(packet, decoder));
        Assert.Equal(0, decoder.DecodeCalls);
    }

    [Fact]
    public void RejectsPayloadLengthThatDoesNotMatchDatagramLength()
    {
        var decoder = new CountingDecoder();
        var packet = V2HeaderWithPayloadLength(payloadLength: 10, actualPayloadLength: 9);

        Assert.False(TryDecrypt(packet, decoder));
        Assert.Equal(0, decoder.DecodeCalls);
    }

    [Fact]
    public void RejectsPayloadLargerThanCallerBufferBeforeSlicingOrAuthentication()
    {
        var decoder = new CountingDecoder();
        var packet = V2HeaderWithPayloadLength(payloadLength: 10);

        Assert.False(TryDecrypt(packet, decoder, opusBuffer: new byte[9]));
        Assert.Equal(0, decoder.DecodeCalls);
    }

    [Theory]
    [InlineData(1)] // encrypted PCM is not defined in v2
    [InlineData(7)] // unknown flag bit
    public void RejectsUnsupportedV2FlagsBeforeDecoder(int flags)
    {
        var decoder = new CountingDecoder();
        var packet = V2HeaderWithPayloadLength(payloadLength: 10, flags: (byte)flags);

        Assert.False(TryDecrypt(packet, decoder));
        Assert.Equal(0, decoder.DecodeCalls);
    }

    [Fact]
    public void TryAuthenticateReturnsOpusBytesAndMetadataWithoutDecoding()
    {
        var opusBytes = new byte[] { 0x48, 0x00 };
        var packetBytes = ValidEncryptedV2Packet(opusBytes);
        var payload = new byte[AudioPipeline.MaxOpusPayloadBytes];
        using var aes = new AesGcm(Key, AudioPipeline.TagSize);

        Assert.True(AudioPipeline.TryAuthenticate(aes, packetBytes, new byte[12], payload, out var packet));
        Assert.Equal((ulong)7, packet.SessionId);
        Assert.Equal((uint)9, packet.Sequence);
        Assert.Equal(AudioPipeline.SampleRate, packet.SampleRate);
        Assert.Equal(AudioPipeline.Codec.Opus, packet.Codec);
        Assert.Equal(opusBytes.Length, packet.PayloadLength);
        Assert.Equal(opusBytes, payload[..opusBytes.Length]);
    }

    [Fact]
    public void TryDecryptWrapperDecodesAnAuthenticatedV2Payload()
    {
        var opusBytes = new byte[] { 0x48, 0x00 };
        var packetBytes = ValidEncryptedV2Packet(opusBytes);
        var decoder = new CapturingDecoder();
        var pcm = new byte[AudioPipeline.PacketPcmBytes];
        using var aes = new AesGcm(Key, AudioPipeline.TagSize);

        Assert.True(AudioPipeline.TryDecrypt(
            aes, packetBytes, new byte[12], pcm, out _, out _, out _, decoder,
            new byte[AudioPipeline.MaxOpusPayloadBytes]));
        Assert.Equal(1, decoder.DecodeCalls);
        Assert.Equal(opusBytes, decoder.EncodedPayload);
        Assert.All(pcm, value => Assert.Equal((byte)0x5A, value));
    }
}
