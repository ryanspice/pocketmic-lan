using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace PocketMicReceiver.Tests;

/// <summary>
/// Verifies that Android-built frames decode correctly on the Windows receiver.
/// These tests construct wire-format payloads using the same byte layout the Kotlin
/// side uses, then feed them through the C# parser — the cross-platform contract.
/// </summary>
public class CrossPlatformInteropTest
{
    private static readonly byte[] Key = ControlProtocol.DeriveControlKey("POCKETMIC-TEST");
    private const byte AndroidControlProtocolVersion = 1;
    private const byte AndroidPacketCryptoVersion = 1;

    /// <summary>
    /// Build an Android-format control frame: 5-byte "PMCTL" magic + version + type +
    /// reserved + length (big endian) + payload + HMAC-SHA256, exactly as
    /// ControlProtocol.build() does on the Kotlin side.
    /// </summary>
    private static byte[] BuildAndroidControlFrame(
        byte type,
        byte[] payload,
        byte[]? key = null)
    {
        key ??= Key;
        var frame = new byte[ControlProtocol.FrameSize];

        // Magic: "PMCTL"
        Encoding.UTF8.GetBytes("PMCTL").CopyTo(frame, 0);

        // Version
        frame[5] = AndroidControlProtocolVersion;

        // Type
        frame[6] = type;

        // Reserved (0)
        frame[7] = 0;

        // Payload length, big endian
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(8, 2), (ushort)payload.Length);

        // Payload
        payload.CopyTo(frame, ControlProtocol.HeaderSize);

        // HMAC-SHA256 over bytes 0..SignedSize
        var mac = HMACSHA256.HashData(key, frame.AsSpan(0, ControlProtocol.SignedSize).ToArray());
        mac.CopyTo(frame, ControlProtocol.SignedSize);

        return frame;
    }

    [Fact]
    public void AndroidControlFrameDecodesOnWindows()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        var frame = BuildAndroidControlFrame(ControlProtocol.TypeProbe, payload);

        Assert.True(ControlProtocol.TryParse(frame, out var type, out var parsed));
        Assert.Equal(ControlProtocol.TypeProbe, type);
        Assert.Equal(payload, parsed);
    }

    [Fact]
    public void AndroidControlFrameVerifiesWithDerivedKey()
    {
        var payload = new byte[] { 10, 20, 30 };
        var frame = BuildAndroidControlFrame(ControlProtocol.TypeAnnounce, payload);

        Assert.True(ControlProtocol.Verify(Key, frame));
    }

    [Fact]
    public void AndroidAnnouncePayloadDecodesOnWindows()
    {
        // Build an announce payload matching the Kotlin layout:
        //   16 bytes nonce + 2 bytes port (big endian) + 1 byte name length + name + version trailer
        var nonce = new byte[ControlProtocol.NonceSize];
        for (var i = 0; i < nonce.Length; i++) nonce[i] = (byte)((i * 7) + 1);

        var hostName = "ANDROID-TEST";
        var nameBytes = Encoding.UTF8.GetBytes(hostName);
        var trailer = new byte[4 + 5]; // audioProtocolVersion, controlProtocolVersion, frontEnd, buildLength, "0.1.0"

        var payload = new byte[ControlProtocol.NonceSize + 3 + nameBytes.Length + trailer.Length];
        nonce.CopyTo(payload, 0);
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(ControlProtocol.NonceSize, 2), 49_500);
        payload[ControlProtocol.NonceSize + 2] = (byte)nameBytes.Length;
        nameBytes.CopyTo(payload, ControlProtocol.NonceSize + 3);

        var trailerOffset = ControlProtocol.NonceSize + 3 + nameBytes.Length;
        payload[trailerOffset] = AndroidPacketCryptoVersion;        // audioProtocolVersion
        payload[trailerOffset + 1] = AndroidControlProtocolVersion; // controlProtocolVersion
        payload[trailerOffset + 2] = 0;                            // frontEnd: Unknown
        payload[trailerOffset + 3] = 5;                            // buildVersion length
        Encoding.UTF8.GetBytes("0.1.0").CopyTo(payload, trailerOffset + 4);

        Assert.True(ControlProtocol.TryReadAnnounce(payload, out var announce));
        Assert.Equal(49_500, announce.AudioPort);
        Assert.Equal("ANDROID-TEST", announce.HostName);
        Assert.True(announce.HasVersions);
        Assert.Equal(AndroidPacketCryptoVersion, announce.AudioProtocolVersion);
        Assert.Equal(AndroidControlProtocolVersion, announce.ControlProtocolVersion);
        Assert.Equal("0.1.0", announce.BuildVersion);
    }

    [Fact]
    public void AndroidAnnounceFrameByteLayoutMatchesExpected()
    {
        // Verify the literal byte layout: magic "PMCTL" at 0..4, version at 5,
        // type at 6, reserved at 7, payload length at 8..9.
        var frame = BuildAndroidControlFrame(
            ControlProtocol.TypeAnnounce,
            new byte[ControlProtocol.NonceSize + 3 + 4]); // minimal announce payload

        Assert.Equal((byte)'P', frame[0]);
        Assert.Equal((byte)'M', frame[1]);
        Assert.Equal((byte)'C', frame[2]);
        Assert.Equal((byte)'T', frame[3]);
        Assert.Equal((byte)'L', frame[4]);
        Assert.Equal(AndroidControlProtocolVersion, frame[5]);
        Assert.Equal(ControlProtocol.TypeAnnounce, frame[6]);
        Assert.Equal(0, frame[7]);

        var payloadLength = BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(8, 2));
        Assert.Equal(ControlProtocol.NonceSize + 3 + 4, payloadLength);
    }
}
