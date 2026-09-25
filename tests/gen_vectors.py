#!/usr/bin/env python3
"""Generate cross-language test vectors for PocketMic protocol v1 and v2."""
import hashlib, struct, json, os

os.makedirs('tests/fixtures', exist_ok=True)

pairing_key = "test-pairing-key-1234"
session_id = 0x0102030405060708
sequence = 0
sample_rate = 48000

key = hashlib.sha256(pairing_key.encode('utf-8')).digest()
nonce = struct.pack('>Q', session_id) + struct.pack('>I', sequence)

# v1 header
h1 = (b'PMIC' + bytes([1, 1]) + struct.pack('>H', 24)
      + struct.pack('>Q', session_id) + struct.pack('>I', sequence)
      + struct.pack('>I', sample_rate))

# v2 header
h2 = (b'PMIC' + bytes([2, 3]) + struct.pack('>H', 28)
      + struct.pack('>Q', session_id) + struct.pack('>I', sequence)
      + struct.pack('>I', sample_rate) + struct.pack('>I', 10))

pcm = bytes([0, 1] * 480)  # 960 bytes
opus = bytes([0x48, 0x00] + [0x00] * 8)  # 10 bytes

from cryptography.hazmat.primitives.ciphers.aead import AESGCM
a = AESGCM(key)
ct1 = a.encrypt(nonce, pcm, h1)
ct2 = a.encrypt(nonce, opus, h2)

assert a.decrypt(nonce, ct1, h1) == pcm, "v1 round-trip failed"
assert a.decrypt(nonce, ct2, h2) == opus, "v2 round-trip failed"

vectors = {
    "key_hex": key.hex(),
    "nonce_hex": nonce.hex(),
    "session_id": session_id,
    "sequence": sequence,
    "sample_rate": sample_rate,
    "pairing_key": pairing_key,
    "v1": {
        "header_hex": h1.hex(),
        "pcm_payload_hex": pcm.hex(),
        "datagram_hex": (h1 + ct1).hex(),
        "datagram_length": len(h1 + ct1),
    },
    "v2": {
        "header_hex": h2.hex(),
        "opus_payload_hex": opus.hex(),
        "datagram_hex": (h2 + ct2).hex(),
        "datagram_length": len(h2 + ct2),
    }
}

with open('tests/fixtures/vectors.json', 'w') as f:
    json.dump(vectors, f, indent=2)

# Android's local JVM tests use java.util.Properties (available without adding a
# JSON library to the app). Keep this generated companion in lockstep with the
# JSON fixture consumed by the C# and Swift suites.
android_vectors = {
    "key_hex": key.hex(),
    "pairing_key": pairing_key,
    "session_id": str(session_id),
    "sequence": str(sequence),
    "sample_rate": str(sample_rate),
    "v1.pcm_payload_hex": pcm.hex(),
    "v1.datagram_hex": (h1 + ct1).hex(),
    "v2.opus_payload_hex": opus.hex(),
    "v2.datagram_hex": (h2 + ct2).hex(),
}

with open('tests/fixtures/protocol-vectors.properties', 'w', encoding='ascii', newline='\n') as f:
    for name, value in android_vectors.items():
        f.write(f"{name}={value}\n")

print(f"v1 datagram: {len(h1 + ct1)} bytes (expected 1000)")
print(f"v2 datagram: {len(h2 + ct2)} bytes")
print(f"v1 round-trip: OK")
print(f"v2 round-trip: OK")
print(f"Written to tests/fixtures/vectors.json")
print(f"Written to tests/fixtures/protocol-vectors.properties")
