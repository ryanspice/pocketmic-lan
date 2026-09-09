#!/usr/bin/env python3
"""Dependency-free structural checks plus an optional AES-GCM round trip."""

from __future__ import annotations

import hashlib
import struct
import sys

MAGIC = b"PMIC"
VERSION = 1
FLAGS = 1
HEADER_SIZE = 24
SAMPLE_RATE = 48_000
SAMPLES_PER_PACKET = 480
PCM_BYTES = SAMPLES_PER_PACKET * 2
TAG_BYTES = 16
DATAGRAM_BYTES = HEADER_SIZE + PCM_BYTES + TAG_BYTES


def build_header(session_id: int, sequence: int) -> bytes:
    return struct.pack(">4sBBHQII", MAGIC, VERSION, FLAGS, HEADER_SIZE, session_id, sequence, SAMPLE_RATE)


def main() -> int:
    session_id = 0x0102030405060708
    sequence = 0x11223344
    pairing_key = "POCKETMIC-TEST"
    header = build_header(session_id, sequence)
    nonce = struct.pack(">QI", session_id, sequence)
    pcm = b"".join(struct.pack("<h", ((index * 257) % 65536) - 32768) for index in range(SAMPLES_PER_PACKET))

    assert len(header) == HEADER_SIZE
    assert len(nonce) == 12
    assert len(pcm) == PCM_BYTES
    assert DATAGRAM_BYTES == 1000
    assert hashlib.sha256(pairing_key.encode("utf-8")).digest().hex() == (
        "019106befdd02e322a4e878011c35ae22687bfb57b4aaba36572bdeee5a062aa"
    )

    try:
        from cryptography.hazmat.primitives.ciphers.aead import AESGCM
    except ImportError:
        print("PASS: protocol structure, endian layout, PCM size, key derivation, and 1000-byte datagram size")
        print("SKIP: AES-GCM round trip (install Python package 'cryptography' to enable it)")
        return 0

    key = hashlib.sha256(pairing_key.encode("utf-8")).digest()
    encrypted = AESGCM(key).encrypt(nonce, pcm, header)
    packet = header + encrypted
    assert len(packet) == DATAGRAM_BYTES
    decrypted = AESGCM(key).decrypt(nonce, packet[HEADER_SIZE:], packet[:HEADER_SIZE])
    assert decrypted == pcm

    print("PASS: protocol structure and AES-256-GCM round trip")
    print(f"Packet: {len(packet)} bytes; PCM: {len(pcm)} bytes; tag: {TAG_BYTES} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
