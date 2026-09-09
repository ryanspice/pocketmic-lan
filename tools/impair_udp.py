#!/usr/bin/env python3
"""PocketMic network impairment relay.

Stage 0 measured the real Wi-Fi path once (p50 15ms, p95 31ms, p99 32ms, max 234-282ms,
0.9-2.5% loss) and every jitter-buffer/reorder-window decision since has been tuned against
that one sample. That is luck, not a test harness: the real network will not reliably
reproduce a 250ms tail or a burst-loss run on demand, so the concealment and trim logic
never gets exercised against its own worst case except by chance.

This tool stands in for the network. It is a UDP relay: point the phone at the relay
instead of the PC, the relay forwards every datagram to the real receiver (and replies back
to the phone), and in between it can drop, delay, reorder, and duplicate packets on command.
Same impairment settings, run twice, hit the same code paths every time.

Usage as a man in the middle:

    phone  --> (this relay, listening on --listen-port) --> PC receiver (--target-host:--target-port)
    phone <-- (this relay)                               <-- PC receiver

Point the PocketMic Android app's "PC IP address" at the machine running this script instead
of the real receiver, using the same port the receiver is listening on. The relay forwards
audio there and impairs on the way; PocketMic's control channel needs --pair (see below) so
discovery and STATS keep working too.

Examples
--------

Baseline sanity check (should be transparent - all impairments off, verifies the relay
itself is not the source of any loss or reordering)::

    python impair_udp.py --listen-port 49500 --target-host 10.0.0.35 --target-port 49500 --pair

Reproduce the measured Stage 0 Wi-Fi tail deterministically (2% random loss, a 250ms jitter
spike roughly every few seconds via a wide jitter distribution, occasional reordering)::

    python impair_udp.py --listen-port 49500 --target-host 10.0.0.35 --target-port 49500 --pair \\
        --loss 2.0 --jitter-mean 20 --jitter-stdev 40 --reorder-prob 1.0 --seed 1

Burst loss - the harder case for concealment, several consecutive packets gone at once
instead of scattered single drops::

    python impair_udp.py --listen-port 49500 --target-host 10.0.0.35 --target-port 49500 \\
        --burst-loss-prob 0.5 --burst-loss-length 8

Duplicate-heavy USB-over-adb simulation, to exercise the dedup path::

    python impair_udp.py --listen-port 49500 --target-host 10.0.0.35 --target-port 49500 \\
        --duplicate-prob 3.0 --jitter-mean 2 --jitter-stdev 1

Impair only the PC-to-phone direction (STATS/ANNOUNCE), leaving audio untouched, to check
the phone's "Live" indicator degrades sensibly when delivery confirmation gets lossy::

    python impair_udp.py --listen-port 49500 --target-host 10.0.0.35 --target-port 49501 \\
        --direction reverse --loss 30
"""
from __future__ import annotations

import argparse
import heapq
import random
import socket
import sys
import threading
import time
from dataclasses import dataclass, field


@dataclass
class ImpairmentConfig:
    loss_percent: float = 0.0
    burst_loss_prob_percent: float = 0.0
    burst_loss_mean_length: float = 5.0
    jitter_mean_ms: float = 0.0
    jitter_stdev_ms: float = 0.0
    jitter_dist: str = "normal"
    reorder_prob_percent: float = 0.0
    reorder_delay_ms: float = 20.0
    duplicate_prob_percent: float = 0.0
    duplicate_delay_ms: float = 5.0
    direction: str = "forward"  # forward = client->target, reverse = target->client, both

    def applies_to(self, is_forward: bool) -> bool:
        if self.direction == "both":
            return True
        return self.direction == "forward" if is_forward else self.direction == "reverse"


class BurstState:
    """Two-state (Gilbert-Elliott-style) burst loss tracker, one instance per direction.

    Plain per-packet loss models miss the failure mode that actually breaks concealment: the
    jitter buffer's `MaxConcealedGapPackets` limit is about *consecutive* loss, and scattered
    independent drops at the same overall rate almost never produce a long run. This tracks
    "currently inside a burst" state explicitly so --burst-loss-length can target exactly that.
    """

    def __init__(self, rng: random.Random) -> None:
        self._rng = rng
        self._remaining = 0

    def should_drop(self, enter_prob_percent: float, mean_length: float) -> bool:
        if self._remaining > 0:
            self._remaining -= 1
            return True
        if enter_prob_percent <= 0:
            return False
        if self._rng.random() * 100.0 < enter_prob_percent:
            # Geometric-ish burst length: mean_length on average, at least 1.
            length = max(1, int(round(self._rng.expovariate(1.0 / max(mean_length, 0.001)))))
            self._remaining = length - 1
            return True
        return False


@dataclass(order=True)
class ScheduledSend:
    release_at: float
    sequence: int = field(compare=False)
    payload: bytes = field(compare=False)
    dest: tuple = field(compare=False)


class Stats:
    def __init__(self) -> None:
        self.lock = threading.Lock()
        self.received = 0
        self.forwarded = 0
        self.dropped_loss = 0
        self.dropped_burst = 0
        self.duplicated = 0
        self.reordered = 0

    def snapshot(self) -> dict:
        with self.lock:
            return dict(vars(self))

    def bump(self, field_name: str, by: int = 1) -> None:
        with self.lock:
            setattr(self, field_name, getattr(self, field_name) + by)


class Relay:
    """One impaired UDP pipe: `listen_port` on this machine <-> `target` elsewhere.

    A single unconnected socket handles both directions, exactly like PocketMic's own
    sockets: a datagram from the configured target address is a reply (reverse direction);
    anything else is treated as the client (forward direction) and its address is remembered
    so replies have somewhere to go. This mirrors PROTOCOL.md's own receiver, which likewise
    identifies a peer by arrival socket/address rather than a self-declared field.
    """

    def __init__(self, name: str, listen_port: int, target: tuple, config: ImpairmentConfig, seed: int, stats: Stats):
        self.name = name
        self.listen_port = listen_port
        self.target = target
        self.config = config
        self.stats = stats
        self._rng = random.Random(seed)
        self._forward_burst = BurstState(self._rng)
        self._reverse_burst = BurstState(self._rng)
        self._sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
        self._sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self._sock.bind(("0.0.0.0", listen_port))
        self._heap: list[ScheduledSend] = []
        self._heap_lock = threading.Lock()
        self._wake = threading.Event()
        self._sequence = 0
        self._last_client: tuple | None = None
        self._running = True

    def start(self) -> None:
        threading.Thread(target=self._receive_loop, name=f"{self.name}-recv", daemon=True).start()
        threading.Thread(target=self._scheduler_loop, name=f"{self.name}-sched", daemon=True).start()

    def stop(self) -> None:
        self._running = False
        self._wake.set()
        try:
            self._sock.close()
        except OSError:
            pass

    def _receive_loop(self) -> None:
        while self._running:
            try:
                data, addr = self._sock.recvfrom(65535)
            except OSError:
                return

            self.stats.bump("received")
            is_forward = addr != self.target
            if is_forward:
                self._last_client = addr
                dest = self.target
            else:
                if self._last_client is None:
                    continue  # A reply with no known client yet has nowhere to go.
                dest = self._last_client

            self._handle_packet(data, dest, is_forward)

    def _handle_packet(self, data: bytes, dest: tuple, is_forward: bool) -> None:
        cfg = self.config
        if not cfg.applies_to(is_forward):
            self._schedule(data, dest, 0.0)
            return

        burst = self._forward_burst if is_forward else self._reverse_burst
        if burst.should_drop(cfg.burst_loss_prob_percent, cfg.burst_loss_mean_length):
            self.stats.bump("dropped_burst")
            return
        if cfg.loss_percent > 0 and self._rng.random() * 100.0 < cfg.loss_percent:
            self.stats.bump("dropped_loss")
            return

        delay_ms = self._sample_jitter(cfg)
        reordered = cfg.reorder_prob_percent > 0 and self._rng.random() * 100.0 < cfg.reorder_prob_percent
        if reordered:
            delay_ms += cfg.reorder_delay_ms
            self.stats.bump("reordered")

        self._schedule(data, dest, delay_ms)

        if cfg.duplicate_prob_percent > 0 and self._rng.random() * 100.0 < cfg.duplicate_prob_percent:
            self.stats.bump("duplicated")
            self._schedule(data, dest, delay_ms + cfg.duplicate_delay_ms)

    def _sample_jitter(self, cfg: ImpairmentConfig) -> float:
        if cfg.jitter_mean_ms <= 0 and cfg.jitter_stdev_ms <= 0:
            return 0.0
        if cfg.jitter_dist == "uniform":
            spread = max(cfg.jitter_stdev_ms, 0.0)
            return max(0.0, self._rng.uniform(cfg.jitter_mean_ms - spread, cfg.jitter_mean_ms + spread))
        if cfg.jitter_dist == "exponential":
            mean = max(cfg.jitter_mean_ms, 0.001)
            return self._rng.expovariate(1.0 / mean)
        # normal, clamped at zero - a negative delay is meaningless here.
        return max(0.0, self._rng.gauss(cfg.jitter_mean_ms, max(cfg.jitter_stdev_ms, 0.0)))

    def _schedule(self, data: bytes, dest: tuple, delay_ms: float) -> None:
        self._sequence += 1
        item = ScheduledSend(time.monotonic() + (delay_ms / 1000.0), self._sequence, data, dest)
        with self._heap_lock:
            heapq.heappush(self._heap, item)
        self._wake.set()

    def _scheduler_loop(self) -> None:
        while self._running:
            with self._heap_lock:
                next_item = self._heap[0] if self._heap else None

            if next_item is None:
                self._wake.wait(timeout=1.0)
                self._wake.clear()
                continue

            remaining = next_item.release_at - time.monotonic()
            if remaining > 0:
                self._wake.wait(timeout=remaining)
                self._wake.clear()
                continue

            with self._heap_lock:
                if self._heap and self._heap[0] is next_item:
                    heapq.heappop(self._heap)
                else:
                    continue  # Something changed the heap while we waited; re-check.

            try:
                self._sock.sendto(next_item.payload, next_item.dest)
                self.stats.bump("forwarded")
            except OSError:
                pass


def build_config(args: argparse.Namespace) -> ImpairmentConfig:
    return ImpairmentConfig(
        loss_percent=args.loss,
        burst_loss_prob_percent=args.burst_loss_prob,
        burst_loss_mean_length=args.burst_loss_length,
        jitter_mean_ms=args.jitter_mean,
        jitter_stdev_ms=args.jitter_stdev,
        jitter_dist=args.jitter_dist,
        reorder_prob_percent=args.reorder_prob,
        reorder_delay_ms=args.reorder_delay,
        duplicate_prob_percent=args.duplicate_prob,
        duplicate_delay_ms=args.duplicate_delay,
        direction=args.direction,
    )


def print_stats_periodically(relays: list[Relay], stats: Stats, interval: float) -> None:
    while True:
        time.sleep(interval)
        snap = stats.snapshot()
        print(
            f"[{time.strftime('%H:%M:%S')}] received={snap['received']} forwarded={snap['forwarded']} "
            f"dropped(loss={snap['dropped_loss']} burst={snap['dropped_burst']}) "
            f"duplicated={snap['duplicated']} reordered={snap['reordered']}",
            flush=True,
        )


def parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description=__doc__,
        formatter_class=argparse.RawDescriptionHelpFormatter,
    )
    parser.add_argument("--listen-port", type=int, required=True, help="UDP port this relay listens on (point the phone here).")
    parser.add_argument("--target-host", required=True, help="Real PocketMic receiver address.")
    parser.add_argument("--target-port", type=int, required=True, help="Real PocketMic receiver audio port.")
    parser.add_argument(
        "--pair", action="store_true",
        help="Also relay listen-port+1 <-> target-port+1 (PocketMic's control channel, always one above audio). "
             "Needed for discovery/STATS to keep working through the relay.",
    )

    loss = parser.add_argument_group("loss")
    loss.add_argument("--loss", type=float, default=0.0, metavar="PERCENT", help="Independent per-packet loss chance. Default: 0.")
    loss.add_argument("--burst-loss-prob", type=float, default=0.0, metavar="PERCENT",
                       help="Chance per surviving packet of *starting* a loss burst. Default: 0.")
    loss.add_argument("--burst-loss-length", type=float, default=5.0, metavar="PACKETS",
                       help="Mean consecutive packets dropped once a burst starts. Default: 5.")

    jitter = parser.add_argument_group("jitter")
    jitter.add_argument("--jitter-mean", type=float, default=0.0, metavar="MS", help="Mean added delay in ms. Default: 0.")
    jitter.add_argument("--jitter-stdev", type=float, default=0.0, metavar="MS",
                         help="Spread of added delay in ms (stdev for normal, half-width for uniform, ignored for exponential). Default: 0.")
    jitter.add_argument("--jitter-dist", choices=("normal", "uniform", "exponential"), default="normal",
                         help="Delay distribution shape. Default: normal.")

    reorder = parser.add_argument_group("reorder")
    reorder.add_argument("--reorder-prob", type=float, default=0.0, metavar="PERCENT",
                          help="Chance a packet gets extra hold time so a later packet overtakes it. Default: 0.")
    reorder.add_argument("--reorder-delay", type=float, default=20.0, metavar="MS",
                          help="Extra delay added on top of jitter for a reordered packet. Default: 20.")

    duplicate = parser.add_argument_group("duplicate")
    duplicate.add_argument("--duplicate-prob", type=float, default=0.0, metavar="PERCENT",
                            help="Chance a packet is also sent again shortly after. Default: 0.")
    duplicate.add_argument("--duplicate-delay", type=float, default=5.0, metavar="MS",
                            help="Delay between the original and its duplicate. Default: 5.")

    parser.add_argument("--direction", choices=("forward", "reverse", "both"), default="forward",
                         help="Which direction impairments apply to: forward = client->target (audio), "
                              "reverse = target->client, both. Default: forward.")
    parser.add_argument("--seed", type=int, default=None, help="Random seed, for a reproducible impairment sequence.")
    parser.add_argument("--stats-interval", type=float, default=5.0, metavar="SECONDS",
                         help="How often to print running totals. Default: 5.")

    return parser.parse_args(argv)


def main(argv: list[str]) -> int:
    args = parse_args(argv)
    config = build_config(args)
    seed = args.seed if args.seed is not None else random.SystemRandom().randrange(1 << 30)

    stats = Stats()
    relays = [
        Relay("audio", args.listen_port, (args.target_host, args.target_port), config, seed, stats),
    ]
    if args.pair:
        relays.append(
            Relay("control", args.listen_port + 1, (args.target_host, args.target_port + 1), config, seed + 1, stats),
        )

    print(f"PocketMic UDP impairment relay (seed={seed})")
    for relay in relays:
        print(f"  {relay.name}: 0.0.0.0:{relay.listen_port} <-> {args.target_host}:{relay.target[1]}")
    print(
        f"  loss={config.loss_percent}%  burst(prob={config.burst_loss_prob_percent}% len={config.burst_loss_mean_length})  "
        f"jitter({config.jitter_dist} mean={config.jitter_mean_ms}ms stdev={config.jitter_stdev_ms}ms)  "
        f"reorder={config.reorder_prob_percent}%  duplicate={config.duplicate_prob_percent}%  direction={config.direction}",
        flush=True,
    )

    for relay in relays:
        relay.start()

    stats_thread = threading.Thread(
        target=print_stats_periodically, args=(relays, stats, args.stats_interval), daemon=True,
    )
    stats_thread.start()

    try:
        while True:
            time.sleep(1.0)
    except KeyboardInterrupt:
        print("\nStopping...")
        for relay in relays:
            relay.stop()
        return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
