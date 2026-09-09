"""Send a PocketMic control PROBE and validate the receiver's ANNOUNCE reply."""
import hashlib
import hmac
import os
import socket
import struct
import sys

KEY_TEXT = sys.argv[1] if len(sys.argv) > 1 else "CWGTVD5TC8UJ"
TARGET = sys.argv[2] if len(sys.argv) > 2 else "10.0.0.35"
AUDIO_PORT = int(sys.argv[3]) if len(sys.argv) > 3 else 49500

FRAME, HMAC_SIZE, HEADER, NONCE = 256, 32, 10, 16
SIGNED = FRAME - HMAC_SIZE
CONTROL_PORT = AUDIO_PORT + 1

master = hashlib.sha256(KEY_TEXT.encode()).digest()
control_key = hmac.new(master, b"pocketmic-control-v1", hashlib.sha256).digest()


def build(msg_type, payload):
    frame = bytearray(FRAME)
    frame[0:5] = b"PMCTL"
    frame[5] = 1
    frame[6] = msg_type
    frame[7] = 0
    struct.pack_into(">H", frame, 8, len(payload))
    frame[HEADER:HEADER + len(payload)] = payload
    tag = hmac.new(control_key, bytes(frame[:SIGNED]), hashlib.sha256).digest()
    frame[SIGNED:] = tag
    return bytes(frame)


nonce = os.urandom(NONCE)
probe = build(1, nonce)

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.settimeout(4.0)
sock.sendto(probe, (TARGET, CONTROL_PORT))
print(f"PROBE  -> {TARGET}:{CONTROL_PORT}  ({len(probe)} bytes)")

try:
    data, addr = sock.recvfrom(2048)
except socket.timeout:
    print("FAIL: no ANNOUNCE received (timeout)")
    sys.exit(1)

print(f"REPLY  <- {addr[0]}:{addr[1]}  ({len(data)} bytes)")

if len(data) != FRAME:
    print(f"FAIL: reply is {len(data)} bytes, expected {FRAME}")
    sys.exit(1)
if len(data) > len(probe):
    print("FAIL: reply larger than probe (amplification risk)")
    sys.exit(1)
if data[0:5] != b"PMCTL":
    print("FAIL: bad magic")
    sys.exit(1)

msg_type = data[6]
payload_len = struct.unpack_from(">H", data, 8)[0]
payload = data[HEADER:HEADER + payload_len]

expected = hmac.new(control_key, data[:SIGNED], hashlib.sha256).digest()
authentic = hmac.compare_digest(expected, data[SIGNED:])

echoed = payload[:NONCE]
port = struct.unpack_from(">H", payload, NONCE)[0]
name_len = payload[NONCE + 2]
host = payload[NONCE + 3:NONCE + 3 + name_len].decode("utf-8", "replace")

print(f"  type          {msg_type} (2=ANNOUNCE)")
print(f"  HMAC valid    {authentic}")
print(f"  nonce echoed  {echoed == nonce}")
print(f"  audio port    {port}")
print(f"  host name     {host}")

# Version trailer, appended after the variable-length name so an older phone that stops reading
# at the name is unaffected. Absent on receivers built before it existed, which is reported as
# unknown rather than guessed at.
FRONT_ENDS = {0: "unknown", 1: "classic", 2: "modern"}
trailer = NONCE + 3 + name_len
versions_ok = True
if len(payload) >= trailer + 4:
    audio_version = payload[trailer]
    control_version = payload[trailer + 1]
    front_end = payload[trailer + 2]
    build_len = payload[trailer + 3]
    build_version = payload[trailer + 4:trailer + 4 + build_len].decode("utf-8", "replace")

    print(f"  audio proto   v{audio_version}")
    print(f"  control proto v{control_version}")
    print(f"  front end     {FRONT_ENDS.get(front_end, front_end)}")
    print(f"  build         {build_version}")

    versions_ok = audio_version == 1 and control_version == 1 and front_end in FRONT_ENDS
else:
    print("  versions      not announced (receiver predates version negotiation)")

ok = msg_type == 2 and authentic and echoed == nonce and port == AUDIO_PORT and host and versions_ok
print("\nPASS: discovery responder authenticated and correct" if ok else "\nFAIL")
sys.exit(0 if ok else 1)
