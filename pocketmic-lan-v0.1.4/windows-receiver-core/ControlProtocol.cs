using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace PocketMicReceiver;

/// <summary>
/// A decoded discovery announce.
///
/// <c>HasVersions</c> is false for an announce from a receiver built before the version trailer
/// existed, in which case the version fields carry no information and must not be compared
/// against anything — reporting a mismatch for a receiver that never stated its version would be
/// worse than saying nothing, because it would be believed.
/// </summary>
public readonly record struct AnnounceInfo(
    int AudioPort,
    string HostName,
    bool HasVersions,
    byte AudioProtocolVersion,
    byte ControlProtocolVersion,
    ReceiverFrontEnd FrontEnd,
    string BuildVersion);

/// <summary>Voice-processing settings the phone can push to the receiver at any time.</summary>
public readonly record struct DspConfig(
    bool Enabled,
    int HighPassHz,
    float Gate,
    float Compressor,
    float PresenceDb,
    float Makeup,
    float NoiseReduction);

/// <summary>
/// PocketMic control channel, v1. Mirrors ControlProtocol.kt byte for byte.
///
/// Carried on UDP <c>audioPort + 1</c>, separate from the audio stream. Every datagram is a
/// fixed <see cref="FrameSize"/> bytes, zero padded, so a reply can never exceed the request
/// that triggered it and the responder cannot be used as a traffic amplifier.
///
/// <code>
/// 0   5   magic "PMCTL"
/// 5   1   version
/// 6   1   message type
/// 7   1   reserved (0)
/// 8   2   payload length, big endian
/// 10  214 payload area, zero padded
/// 224 32  HMAC-SHA256 over bytes 0..224
/// </code>
/// </summary>
public static class ControlProtocol
{
    public const int FrameSize = 256;
    public const int HmacSize = 32;
    public const int HeaderSize = 10;
    public const int SignedSize = FrameSize - HmacSize;
    public const int MaxPayload = SignedSize - HeaderSize;
    public const int NonceSize = 16;
    public const byte Version = 1;

    // The control channel is audioPort + 1, so the highest usable audio port is one below the
    // end of the UDP port range. Both front ends validate against these constants before opening
    // sockets; keeping the boundary here prevents the protocol rule from drifting from the UI.
    public const int MinAudioPort = 1;
    public const int MaxAudioPort = ushort.MaxValue - 1;

    /// <summary>
    /// Hard cap on the UTF-8 length of the host name inside an announce. The name is variable
    /// length and the version trailer follows it, so without a ceiling here a long multi-byte
    /// machine name could push the trailer out of the frame — which would silently turn version
    /// negotiation off for exactly the machines whose names are unusual.
    /// </summary>
    public const int AnnounceMaxNameBytes = 96;

    /// <summary>The name is also capped by characters, not only bytes; see <see cref="EncodeCapped"/>.</summary>
    public const int AnnounceMaxNameChars = 63;

    /// <summary>Cap on the build-version string. "0.1.2-preview" and its like fit comfortably.</summary>
    public const int AnnounceMaxBuildBytes = 16;

    public const byte TypeProbe = 1;
    public const byte TypeAnnounce = 2;
    public const byte TypeStats = 3;
    public const byte TypeConfig = 4;

    private static readonly byte[] Magic = "PMCTL"u8.ToArray();

    public static int ControlPort(int audioPort) => audioPort + 1;

    /// <summary>
    /// Domain separated from the audio key so one key is never used for both AES-GCM and HMAC.
    /// Must match ControlProtocol.deriveControlKey on Android exactly.
    /// </summary>
    public static byte[] DeriveControlKey(string pairingKey)
    {
        var master = SHA256.HashData(Encoding.UTF8.GetBytes(pairingKey));
        return HMACSHA256.HashData(master, "pocketmic-control-v1"u8.ToArray());
    }

    public static byte[] Build(byte[] controlKey, byte type, ReadOnlySpan<byte> payload)
    {
        if (payload.Length > MaxPayload)
        {
            throw new ArgumentException($"Control payload of {payload.Length} exceeds {MaxPayload} bytes.");
        }

        var frame = new byte[FrameSize];
        Magic.CopyTo(frame.AsSpan(0, Magic.Length));
        frame[5] = Version;
        frame[6] = type;
        frame[7] = 0;
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(8, 2), (ushort)payload.Length);
        payload.CopyTo(frame.AsSpan(HeaderSize));

        var tag = HMACSHA256.HashData(controlKey, frame.AsSpan(0, SignedSize));
        tag.CopyTo(frame.AsSpan(SignedSize));
        return frame;
    }

    /// <summary>
    /// Structural parse that deliberately does not verify the HMAC, so a key mismatch can be
    /// reported as a key mismatch rather than as silence.
    /// </summary>
    public static bool TryParse(byte[] frame, out byte type, out byte[] payload)
    {
        type = 0;
        payload = Array.Empty<byte>();
        if (frame.Length != FrameSize) return false;
        if (!frame.AsSpan(0, Magic.Length).SequenceEqual(Magic)) return false;
        if (frame[5] != Version) return false;

        var payloadLength = BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(8, 2));
        if (payloadLength > MaxPayload) return false;

        type = frame[6];
        payload = frame.AsSpan(HeaderSize, payloadLength).ToArray();
        return true;
    }

    public static bool Verify(byte[] controlKey, byte[] frame)
    {
        if (frame.Length != FrameSize) return false;
        var expected = HMACSHA256.HashData(controlKey, frame.AsSpan(0, SignedSize));
        return CryptographicOperations.FixedTimeEquals(expected, frame.AsSpan(SignedSize, HmacSize));
    }

    /// <summary>
    /// Reads the framing version out of anything carrying the control magic, without parsing or
    /// authenticating the rest.
    ///
    /// <see cref="TryParse"/> refuses a frame whose version is not ours, which is correct — a
    /// responder must not act on a message it cannot fully understand. But refusing it silently
    /// makes a future v2 peer indistinguishable from no peer at all, which is the exact failure
    /// this channel exists to eliminate. This is the same discipline
    /// <c>AudioPipeline.TryDecrypt</c> applies to the audio path: reject before doing any work,
    /// but keep enough to say why.
    /// </summary>
    public static bool TryPeekVersion(byte[] frame, int length, out byte version)
    {
        version = 0;
        if (length != FrameSize || frame.Length < FrameSize) return false;
        if (!frame.AsSpan(0, Magic.Length).SequenceEqual(Magic)) return false;

        version = frame[5];
        return true;
    }

    /// <summary>
    /// The discovery reply.
    ///
    /// <code>
    /// 0   16  probe nonce, echoed back
    /// 16  2   audio port, big endian
    /// 18  1   host name length in bytes
    /// 19  n   host name, UTF-8
    /// 19+n 1  audio protocol version
    /// 20+n 1  control protocol version
    /// 21+n 1  receiver front end
    /// 22+n 1  build version length in bytes
    /// 23+n m  build version, UTF-8
    /// </code>
    ///
    /// The version trailer is appended after the variable-length name rather than placed at a
    /// fixed offset so that a phone built before it existed still reads the nonce, port and name
    /// exactly as it always did, and simply ignores the bytes past the name. Discovery keeps
    /// working across the upgrade; only the version reporting is missing on the old build.
    /// </summary>
    public static byte[] AnnouncePayload(
        ReadOnlySpan<byte> nonce,
        int audioPort,
        string hostName,
        byte audioProtocolVersion = AudioPipeline.ProtocolVersion,
        ReceiverFrontEnd frontEnd = ReceiverFrontEnd.Unknown,
        string buildVersion = "")
    {
        var name = EncodeCapped(hostName, AnnounceMaxNameChars, AnnounceMaxNameBytes);
        var build = EncodeCapped(buildVersion, AnnounceMaxBuildBytes, AnnounceMaxBuildBytes);

        var payload = new byte[NonceSize + 2 + 1 + name.Length + 3 + 1 + build.Length];
        nonce[..NonceSize].CopyTo(payload.AsSpan(0, NonceSize));
        BinaryPrimitives.WriteUInt16BigEndian(payload.AsSpan(NonceSize, 2), (ushort)audioPort);
        payload[NonceSize + 2] = (byte)name.Length;
        name.CopyTo(payload.AsSpan(NonceSize + 3));

        var trailer = NonceSize + 3 + name.Length;
        payload[trailer] = audioProtocolVersion;
        payload[trailer + 1] = Version;
        payload[trailer + 2] = (byte)frontEnd;
        payload[trailer + 3] = (byte)build.Length;
        build.CopyTo(payload.AsSpan(trailer + 4));
        return payload;
    }

    /// <summary>
    /// UTF-8 bytes for a string clipped to <paramref name="maxChars"/> characters and then to
    /// <paramref name="maxBytes"/> bytes, dropping whole characters rather than splitting one.
    /// A half-written multi-byte sequence decodes to a replacement character on the phone, which
    /// looks like corruption rather than like truncation.
    /// </summary>
    private static byte[] EncodeCapped(string value, int maxChars, int maxBytes)
    {
        var clipped = value.Length > maxChars ? value[..maxChars] : value;
        while (true)
        {
            var bytes = Encoding.UTF8.GetBytes(clipped);
            if (bytes.Length <= maxBytes || clipped.Length == 0) return bytes;
            clipped = clipped[..^1];
        }
    }

    /// <summary>
    /// Reads an announce built by <see cref="AnnouncePayload"/>, on either side of the version
    /// trailer's introduction. A payload without the trailer yields
    /// <see cref="AnnounceInfo.HasVersions"/> false rather than an invented version number: not
    /// knowing and claiming to match are different answers, and only one of them is honest.
    /// </summary>
    public static bool TryReadAnnounce(byte[] payload, out AnnounceInfo announce)
    {
        announce = default;
        if (payload.Length < NonceSize + 3) return false;

        var audioPort = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(NonceSize, 2));
        var nameLength = payload[NonceSize + 2];
        if (payload.Length < NonceSize + 3 + nameLength) return false;

        var hostName = Encoding.UTF8.GetString(payload, NonceSize + 3, nameLength);
        var trailer = NonceSize + 3 + nameLength;

        if (payload.Length < trailer + 4)
        {
            announce = new AnnounceInfo(audioPort, hostName, false, 0, 0, ReceiverFrontEnd.Unknown, string.Empty);
            return true;
        }

        var buildLength = payload[trailer + 3];
        var build = payload.Length >= trailer + 4 + buildLength
            ? Encoding.UTF8.GetString(payload, trailer + 4, buildLength)
            : string.Empty;

        announce = new AnnounceInfo(
            audioPort,
            hostName,
            true,
            payload[trailer],
            payload[trailer + 1],
            (ReceiverFrontEnd)payload[trailer + 2],
            build);
        return true;
    }

    /// <summary>Mirrors ControlProtocol.configPayload on Android. Returns false if malformed.</summary>
    public static bool TryReadConfig(byte[] payload, out DspConfig config)
    {
        config = default;
        if (payload.Length < 7) return false;
        config = new DspConfig(
            Enabled: payload[0] != 0,
            HighPassHz: payload[1] * 4,
            Gate: payload[2] / 100f,
            Compressor: payload[3] / 100f,
            PresenceDb: payload[4] / 10f,
            Makeup: payload[5] / 100f,
            NoiseReduction: payload[6] / 100f);
        return true;
    }

    public static byte[] StatsPayload(
        long packets,
        long lost,
        long late,
        long rejected,
        long trimmed,
        int bufferMilliseconds,
        bool playing)
    {
        var payload = new byte[45];
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(0, 8), packets);
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(8, 8), lost);
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(16, 8), late);
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(24, 8), rejected);
        BinaryPrimitives.WriteInt64BigEndian(payload.AsSpan(32, 8), trimmed);
        BinaryPrimitives.WriteInt32BigEndian(payload.AsSpan(40, 4), bufferMilliseconds);
        payload[44] = playing ? (byte)1 : (byte)0;
        return payload;
    }
}
