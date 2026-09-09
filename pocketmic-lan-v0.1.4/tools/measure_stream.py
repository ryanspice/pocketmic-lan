"""PocketMic Stage 0 baseline measurement.

Reports, per distinct sender socket and per crypto session:
  packet rate, interarrival jitter percentiles, loss, reordering,
  duplicate sequences, and audio level.

Usage: pocketmic_baseline.py <pairing_key> [port] [seconds]
"""
import hashlib
import math
import socket
import statistics
import struct
import sys
import time
from collections import defaultdict

from cryptography.hazmat.primitives.ciphers.aead import AESGCM

KEY_TEXT = sys.argv[1] if len(sys.argv) > 1 else "CWGTVD5TC8UJ"
PORT = int(sys.argv[2]) if len(sys.argv) > 2 else 49500
SECONDS = float(sys.argv[3]) if len(sys.argv) > 3 else 30.0

MAGIC, HEADER, TAG, PACKET = b"PMIC", 24, 16, 1000
aead = AESGCM(hashlib.sha256(KEY_TEXT.encode()).digest())

sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
sock.setsockopt(socket.SOL_SOCKET, socket.SO_RCVBUF, 1 << 20)
sock.bind(("0.0.0.0", PORT))
sock.settimeout(2.0)

print(f"listening 0.0.0.0:{PORT} for {SECONDS:.0f}s", flush=True)


class Stream:
    __slots__ = ("first_seq", "last_seq", "seen", "count", "arrivals",
                 "dups", "reorders", "peak", "sum_sq", "n_samples", "t_first", "t_last")

    def __init__(self):
        self.first_seq = None
        self.last_seq = None
        self.seen = set()
        self.count = 0
        self.arrivals = []
        self.dups = 0
        self.reorders = 0
        self.peak = 0.0
        self.sum_sq = 0.0
        self.n_samples = 0
        self.t_first = None
        self.t_last = None


streams = defaultdict(Stream)
total = bad = 0
start = time.monotonic()

while time.monotonic() - start < SECONDS:
    try:
        data, addr = sock.recvfrom(2048)
    except socket.timeout:
        continue
    now = time.monotonic()
    total += 1

    if len(data) != PACKET or data[:4] != MAGIC:
        bad += 1
        continue
    sid, seq, rate = struct.unpack(">QII", data[8:24])
    try:
        pcm = aead.decrypt(data[8:16] + data[16:20], data[HEADER:], data[:HEADER])
    except Exception:
        bad += 1
        continue

    st = streams[(addr[1], sid)]
    if st.t_first is None:
        st.t_first = now
        st.first_seq = seq
    else:
        st.arrivals.append((now - st.t_last) * 1000.0)
    st.t_last = now
    st.count += 1

    if seq in st.seen:
        st.dups += 1
    else:
        st.seen.add(seq)
    if st.last_seq is not None and ((seq - st.last_seq) & 0xFFFFFFFF) > 0x7FFFFFFF:
        st.reorders += 1
    st.last_seq = seq

    n = len(pcm) // 2
    samples = struct.unpack(f"<{n}h", pcm)
    ssq = sum(s * s for s in samples)
    st.sum_sq += ssq
    st.n_samples += n
    st.peak = max(st.peak, math.sqrt(ssq / n) / 32768.0)

sock.close()


def pct(values, p):
    if not values:
        return float("nan")
    ordered = sorted(values)
    k = min(len(ordered) - 1, max(0, int(round(p / 100.0 * (len(ordered) - 1)))))
    return ordered[k]


print("\n================ STAGE 0 BASELINE ================")
print(f"total datagrams: {total}   malformed/failed-auth: {bad}")
print(f"distinct sender-socket/session pairs: {len(streams)}")
if len(streams) > 1:
    print("*** WARNING: more than one concurrent stream is transmitting ***")

for (sport, sid), st in sorted(streams.items(), key=lambda kv: -kv[1].count):
    span = (st.t_last - st.t_first) if st.t_first and st.t_last else 0.0
    expected = ((st.last_seq - st.first_seq) & 0xFFFFFFFF) + 1
    lost = expected - len(st.seen)
    rms = math.sqrt(st.sum_sq / st.n_samples) / 32768.0 if st.n_samples else 0.0
    print(f"\n--- src port {sport}  session 0x{sid:016x} ---")
    print(f"  packets            {st.count}   over {span:.1f}s  = {st.count / span if span else 0:.1f}/s  (target 100/s)")
    print(f"  sequence span      {st.first_seq} -> {st.last_seq}  (expected {expected})")
    print(f"  unique seqs        {len(st.seen)}   lost {lost} ({100.0 * lost / expected if expected else 0:.2f}%)")
    print(f"  duplicates         {st.dups}")
    print(f"  reordered          {st.reorders}")
    if st.arrivals:
        print(f"  interarrival ms    p50={pct(st.arrivals,50):.2f}  p95={pct(st.arrivals,95):.2f}  "
              f"p99={pct(st.arrivals,99):.2f}  max={max(st.arrivals):.2f}  (ideal 10.00)")
        print(f"  jitter (stdev)     {statistics.pstdev(st.arrivals):.2f} ms")
    print(f"  level              rms {20 * math.log10(rms) if rms > 0 else -999:.1f} dBFS   "
          f"peak-frame {20 * math.log10(st.peak) if st.peak > 0 else -999:.1f} dBFS")
