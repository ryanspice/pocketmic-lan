# PocketMic UDP protocol v1

PocketMic sends one authenticated encrypted datagram for each 10 ms microphone frame.

## Audio

- signed PCM16 little-endian;
- mono;
- 48,000 Hz;
- 480 samples / 960 plaintext bytes per packet;
- 100 packets per second;
- 768 kbit/s raw audio.

## Datagram

Header integers are big-endian. Audio samples inside the encrypted payload are little-endian.

| Offset | Bytes | Field |
|---:|---:|---|
| 0 | 4 | ASCII `PMIC` |
| 4 | 1 | protocol version (`1`) |
| 5 | 1 | flags; bit 0 means AES-GCM encrypted |
| 6 | 2 | header length (`24`) |
| 8 | 8 | random stream session ID |
| 16 | 4 | unsigned packet sequence |
| 20 | 4 | sample rate (`48000`) |
| 24 | 960 | AES-GCM ciphertext |
| 984 | 16 | AES-GCM authentication tag |

Total datagram length: exactly `1000` bytes.

## Encryption

- key: `SHA-256(UTF-8(pairing key))`;
- cipher: AES-256-GCM;
- nonce: 8-byte session ID followed by 4-byte sequence;
- authenticated data: complete 24-byte header;
- authentication tag: 128 bits.

The session ID changes when a stream starts. Every sequence value is used at most once within that session. The Android sender terminates the stream before sequence reuse.

Recovering a dropped link does **not** start a stream. The sender keeps the same session ID, the same cipher and the same sequence counter across a reconnect, and only the destination may change; a reconnect that reset the counter would repeat a (key, nonce) pair, which is a total loss of confidentiality and authenticity for every message under that key. A genuine restart draws a new random session ID, which makes the sequences already used unreachable rather than merely unlikely.

## Receiver sequencing policy

- the first valid packet establishes the current session and sequence;
- modular unsigned-32-bit arithmetic handles rollover;
- already-played or reordered older packets are dropped;
- gaps of 1–20 packets (up to 200 ms) are concealed by repeating the last good frame at a
  decaying level, ending in digital silence at the ceiling — not by inserting hard digital
  silence, which a run of would be heard as crackle;
- larger jumps clear playback and require a fresh prebuffer;
- a new authenticated session ID clears playback and sequence state;
- playback starts after a 100 ms prebuffer and, while the input is quiet, buffered audio is
  trimmed above the high-water mark (220 ms at the default prebuffer; the prebuffer is
  user-adjustable from 40 to 300 ms on the receiver, and the high-water mark is derived from
  it — at least 120 ms above it, and at least twice it when that is larger);
- malformed lengths, headers, tags, or sample rates are rejected.

These receiver rules do not alter the wire format.

## Control channel

Carried on UDP `audioPort + 1`, separate from the audio stream. Every datagram is exactly 256 bytes, zero padded, so a reply can never exceed the request that triggered it and the discovery responder cannot be used as a traffic amplifier.

| Offset | Bytes | Field |
|---:|---:|---|
| 0 | 5 | ASCII `PMCTL` |
| 5 | 1 | control protocol version (`1`) |
| 6 | 1 | message type: 1 PROBE, 2 ANNOUNCE, 3 STATS, 4 CONFIG |
| 7 | 1 | reserved (`0`) |
| 8 | 2 | payload length, big endian |
| 10 | 214 | payload area, zero padded |
| 224 | 32 | HMAC-SHA256 over bytes 0–223 |

The control key is `HMAC-SHA256(SHA-256(UTF-8(pairing key)), "pocketmic-control-v1")`, domain separated so one key is never used for both AES-GCM and HMAC. The control socket is bound to `audioPort + 1`, so the audio port range is `1..65534` and control port `65535` remains representable.

Parsing and authentication are deliberately separate steps on both sides. A receiver replies to a probe it cannot authenticate, and the phone parses a reply whose HMAC fails, so that "a receiver answered but the key is wrong" is reportable as itself rather than collapsing into silence.

A probe carries a fresh random 16-byte nonce, and the announce echoes it back. The phone only
accepts an announce that echoes a probe it sent in the current or immediately previous round:
an authenticated announce carrying an older nonce is a recorded replay of a genuine exchange
(the HMAC cannot distinguish one) or a reply so delayed it should not be believed, and either
way it must not refresh discovery state. The next 1 Hz probe round supersedes it.

### ANNOUNCE payload

| Offset | Bytes | Field |
|---:|---:|---|
| 0 | 16 | probe nonce, echoed back |
| 16 | 2 | audio port, big endian |
| 18 | 1 | host name length in bytes (UTF-8, capped at 63 characters and 96 bytes) |
| 19 | *n* | host name |
| 19+*n* | 1 | audio protocol version |
| 20+*n* | 1 | control protocol version |
| 21+*n* | 1 | receiver front end: 0 unknown, 1 classic, 2 modern |
| 22+*n* | 1 | build version length in bytes (UTF-8, capped at 16) |
| 23+*n* | *m* | build version |

The version trailer follows the variable-length name rather than sitting at a fixed offset, so a phone built before the trailer existed reads the nonce, port and name exactly as it always did and ignores what follows. A payload that ends before the trailer is valid and means "this receiver did not state its versions", which is not the same as, and must not be reported as, a mismatch.

A datagram whose framing version is not the reader's is rejected before anything else is parsed, on both sides — but the version itself stays readable, so a peer speaking a version we do not is reported as a version disagreement rather than as no peer at all.

### STATS payload (45 bytes)

The receiver pushes this back to the phone every 500 ms while audio is arriving. All integers
big endian.

| Offset | Bytes | Field |
|---:|---:|---|
| 0 | 8 | packets received and authenticated |
| 8 | 8 | lost (missing sequence gap) |
| 16 | 8 | late (older than already played) |
| 24 | 8 | rejected (malformed, unauthenticated, or wrong protocol version) |
| 32 | 8 | trimmed (discarded to claw back latency) |
| 40 | 4 | buffered playback depth in milliseconds |
| 44 | 1 | playing flag: 1 while output is running |

### CONFIG payload (7 bytes)

The phone pushes voice-processing settings to the receiver at any time; the PC applies them
live. Fire and forget over UDP — the sliders are continuous, so a dropped update is corrected
by the next one.

| Offset | Bytes | Field |
|---:|---:|---|
| 0 | 1 | processing enabled flag |
| 1 | 1 | high-pass frequency in Hz ÷ 4 |
| 2 | 1 | noise gate amount, 0–100 |
| 3 | 1 | compressor amount, 0–100 |
| 4 | 1 | presence EQ boost in dB × 10 |
| 5 | 1 | make-up gain, 0–100 |
| 6 | 1 | noise reduction amount, 0–100 |
