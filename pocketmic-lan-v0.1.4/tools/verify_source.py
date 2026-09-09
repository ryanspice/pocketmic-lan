#!/usr/bin/env python3
"""Static source/package consistency checks that do not require Android or .NET SDKs."""

from __future__ import annotations

import hashlib
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VERSION = "0.1.2"
GRADLE_VERSION = "8.14.3"


def require(path: str) -> Path:
    result = ROOT / path
    assert result.is_file(), f"missing required file: {path}"
    return result


def read(path: str) -> str:
    return require(path).read_text(encoding="utf-8")


def balanced_csharp(source: str, label: str) -> None:
    """Small lexer that catches truncated strings/comments and unbalanced delimiters."""
    stack: list[str] = []
    pairs = {')': '(', ']': '[', '}': '{'}
    i = 0
    state = "code"
    while i < len(source):
        ch = source[i]
        nxt = source[i + 1] if i + 1 < len(source) else ""
        if state == "code":
            if ch == '/' and nxt == '/':
                state = "line_comment"; i += 2; continue
            if ch == '/' and nxt == '*':
                state = "block_comment"; i += 2; continue
            if ch == '"':
                state = "string"; i += 1; continue
            if ch == "'":
                state = "char"; i += 1; continue
            if ch in "([{":
                stack.append(ch)
            elif ch in ")]}" :
                assert stack and stack.pop() == pairs[ch], f"unbalanced C# delimiter in {label} near offset {i}"
        elif state == "line_comment":
            if ch == '\n': state = "code"
        elif state == "block_comment":
            if ch == '*' and nxt == '/': state = "code"; i += 2; continue
        elif state in {"string", "char"}:
            if ch == '\\': i += 2; continue
            if (state == "string" and ch == '"') or (state == "char" and ch == "'"):
                state = "code"
        i += 1
    assert state in {"code", "line_comment"}, f"unterminated C# {state} in {label}"
    assert not stack, f"unclosed C# delimiters in {label}: {stack}"


def verify_source_manifest() -> None:
    """SOURCE_SHA256SUMS.txt must cover every source file in the tree, and nothing else.

    The manifest is the tamper-evidence baseline for a release, so it has to be complete: a
    checksum file that omits files cannot catch a change to the ones it omits. The exclusion
    set mirrors the release tree — build outputs, caches, scratch, and generated artifacts.
    """
    excluded = {".git", ".fugu-audit", ".tools", ".gradle", ".kotlin",
                "build", "bin", "obj", "release", "screenshots"}
    files = [
        path for path in ROOT.rglob("*")
        if path.is_file()
        and not any(part in excluded for part in path.relative_to(ROOT).parts)
        and path.name != "SOURCE_SHA256SUMS.txt"
    ]
    files.sort(key=lambda path: path.relative_to(ROOT).as_posix())

    expected = {
        "./" + path.relative_to(ROOT).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
        for path in files
    }
    actual = {}
    for line in (ROOT / "SOURCE_SHA256SUMS.txt").read_text(encoding="utf-8").splitlines():
        parts = line.split("  ", 1)
        if len(parts) == 2:
            actual[parts[1]] = parts[0]

    missing = sorted(set(expected) - set(actual))
    extra = sorted(set(actual) - set(expected))
    changed = sorted(name for name in expected if name in actual and actual[name] != expected[name])
    assert not missing, f"SOURCE_SHA256SUMS.txt missing entries for: {missing}"
    assert not extra, f"SOURCE_SHA256SUMS.txt has stale entries: {extra}"
    assert not changed, f"SOURCE_SHA256SUMS.txt has stale hashes: {changed}"


def main() -> int:
    gradle = read("android/app/build.gradle.kts")
    wrapper_properties = read("android/gradle/wrapper/gradle-wrapper.properties")
    receiver_project = read("windows-receiver/PocketMicReceiver.csproj")
    tests_project = read("windows-receiver-tests/PocketMicReceiver.Tests.csproj")
    winui_project = read("windows-receiver-winui/PocketMicReceiver.WinUI.csproj")
    launcher = read("windows-launcher/Program.cs")
    build_script = read("scripts/build-android.ps1")
    publish_script = read("scripts/publish-windows.ps1")
    firewall_script = read("scripts/allow-firewall.ps1")
    receiver = read("windows-receiver/Program.cs")
    core_sources = {
        source.name: source.read_text(encoding="utf-8")
        for source in sorted((ROOT / "windows-receiver-core").glob("*.cs"))
    }
    # The engine has been extracted out of the WinForms form and into Core so the WinUI 3 front
    # end can drive it. Lifecycle guards are therefore checked across the receiver as a whole:
    # moving a guard between host and engine is refactoring, deleting one is a regression.
    receiver_all = "\n".join([receiver, *core_sources.values()])
    pipeline = read("windows-receiver-core/AudioPipeline.cs")
    control_cs = read("windows-receiver-core/ControlProtocol.cs")
    control_kt = read("android/app/src/main/java/com/ryanspice/pocketmic/ControlProtocol.kt")
    control_channel = read("android/app/src/main/java/com/ryanspice/pocketmic/ControlChannel.kt")
    service = read("android/app/src/main/java/com/ryanspice/pocketmic/MicStreamingService.kt")
    mic_config = read("android/app/src/main/java/com/ryanspice/pocketmic/MicConfig.kt")
    main_activity = read("android/app/src/main/java/com/ryanspice/pocketmic/MainActivity.kt")
    diagnostics_screen = read("android/app/src/main/java/com/ryanspice/pocketmic/DiagnosticsScreen.kt")
    diagnostics_log = read("android/app/src/main/java/com/ryanspice/pocketmic/DiagnosticsLog.kt")
    crypto = read("android/app/src/main/java/com/ryanspice/pocketmic/PacketCrypto.kt")
    streaming_state = read("android/app/src/main/java/com/ryanspice/pocketmic/StreamingState.kt")
    reconnect_policy = read("android/app/src/main/java/com/ryanspice/pocketmic/ReconnectPolicy.kt")
    analytics_collector = read("windows-receiver-core/Analytics/AnalyticsCollector.cs")
    connection_metrics = read("windows-receiver-core/Analytics/ConnectionMetrics.cs")

    assert f'versionName = "{VERSION}"' in gradle
    assert f"<Version>{VERSION}</Version>" in receiver_project
    assert f"$AppVersion = '{VERSION}'" in build_script
    assert "versionCode = 3" in gradle

    # The wrapper is the build contract: without it the APK depends on whichever Gradle happens
    # to be on PATH, which is how a machine-specific build becomes an unreproducible one.
    require("android/gradlew")
    require("android/gradlew.bat")
    require("android/gradle/wrapper/gradle-wrapper.jar")
    assert f"gradle-{GRADLE_VERSION}-all.zip" in wrapper_properties

    # Packet mathematics lives in Core so both front ends share one copy of it.
    assert "public const int HeaderSize = 24" in pipeline
    assert "public const int PacketPcmBytes = 960" in pipeline
    assert "public const int SampleRate = 48_000" in pipeline
    assert "public const int DatagramBytes = HeaderSize + PacketPcmBytes + TagSize" in pipeline
    assert "data.Length != DatagramBytes" in pipeline
    assert "const val HEADER_SIZE = 24" in crypto
    assert "private const val SAMPLE_RATE = 48_000" in service

    # The rest of the receiver must consume those constants rather than keep a second copy that
    # can drift away from the phone's idea of the packet layout.
    assert "AudioPipeline.PacketPcmBytes" in receiver_all
    assert "AudioPipeline.TryDecrypt(" in receiver_all
    assert "AudioPipeline.HighWaterFor(" in receiver_all
    assert "AudioPipeline.BuildConcealmentFrame(" in receiver_all
    assert "AudioPipeline.ShouldTrim(" in receiver_all
    assert "DatagramBytes = HeaderSize + PacketPcmBytes + TagSize" not in receiver

    assert "public const int DefaultPrebufferMilliseconds = 100;" in pipeline
    # The stated default has to be the value HighWaterFor computes for the default prebuffer,
    # or the engine starts on one threshold and silently moves to another on the first apply.
    assert "public const int DefaultHighWaterMilliseconds = 220;" in pipeline
    assert "public const int MaxConcealedGapPackets = 20;" in pipeline

    assert "while (coroutineContext.isActive)" in service
    assert "while (isActive)" not in service
    assert "currentCoroutineContext().isActive" not in service
    assert "createStartedAudioRecord" in service
    assert "private var destroying = false" in service
    assert "PacketCrypto.Encryptor" in service
    assert "DatagramPacket(ByteArray(0), 0, address, config.port)" in service
    assert "PlaybackStopped += HandlePlaybackStopped" in receiver_all
    assert "PlaybackStopped -= HandlePlaybackStopped" in receiver_all
    assert "ReferenceEquals(sender, _waveOut)" in receiver_all
    assert "ReceiveLoopAsync(UdpClient udp, CancellationToken cancellationToken, long runId)" in receiver_all
    # Run-generation guards: a callback queued before a stop must not act on the run that
    # replaced it, so every asynchronous continuation re-checks the id it started with.
    assert receiver_all.count("runId != _runId") >= 4
    assert "PostUiThrottled" not in receiver_all

    # Control channel: two independent implementations of one wire format, so the framing
    # constants and the key derivation label have to agree literally.
    assert "const val FRAME_SIZE = 256" in control_kt
    assert "public const int FrameSize = 256;" in control_cs
    assert "const val HMAC_SIZE = 32" in control_kt
    assert "public const int HmacSize = 32;" in control_cs
    assert "const val HEADER_SIZE = 10" in control_kt
    assert "public const int HeaderSize = 10;" in control_cs
    assert "const val NONCE_SIZE = 16" in control_kt
    assert "public const int NonceSize = 16;" in control_cs
    assert "public const int MaxAudioPort = ushort.MaxValue - 1;" in control_cs
    assert "pocketmic-control-v1" in control_kt
    assert "pocketmic-control-v1" in control_cs
    assert "fun controlPort(audioPort: Int): Int = audioPort + 1" in control_kt
    assert "ControlPort(int audioPort) => audioPort + 1;" in control_cs

    # Parsing and authentication stay separate on both sides: a wrong pairing key has to be
    # reportable as a wrong pairing key rather than as an unanswered probe.
    assert "fun parse(frame: ByteArray, length: Int): ControlMessage?" in control_kt
    assert "fun verify(controlKey: ByteArray, message: ControlMessage): Boolean" in control_kt
    assert "public static bool TryParse(byte[] frame, out byte type, out byte[] payload)" in control_cs
    assert "public static bool Verify(byte[] controlKey, byte[] frame)" in control_cs

    assert "if (payload.size < 45) return null" in control_kt
    assert "var payload = new byte[45];" in control_cs
    assert "if (payload.Length < 7) return false;" in control_cs

    # Version negotiation. The announce trailer is a second wire format implemented twice, so its
    # caps have to agree literally too — a name cap that differed by one byte would truncate the
    # trailer off exactly the machines whose names are longest, and only on one side.
    assert "const val ANNOUNCE_MAX_NAME_BYTES = 96" in control_kt
    assert "public const int AnnounceMaxNameBytes = 96;" in control_cs
    assert "const val ANNOUNCE_MAX_BUILD_BYTES = 16" in control_kt
    assert "public const int AnnounceMaxBuildBytes = 16;" in control_cs
    # The character cap and the byte cap are separate limits on the same field; both sides
    # must agree on both, or names truncate differently per platform.
    assert "const val ANNOUNCE_MAX_NAME_CHARS = 63" in control_kt
    assert "public const int AnnounceMaxNameChars = 63;" in control_cs
    assert "const val FRONT_END_CLASSIC: Byte = 1" in control_kt
    assert "Classic = 1," in connection_metrics
    assert "const val FRONT_END_MODERN: Byte = 2" in control_kt
    assert "Modern = 2," in connection_metrics

    # A version this build cannot parse must stay reportable on both sides, or a newer peer is
    # indistinguishable from no peer at all. Same discipline as AudioPipeline.TryDecrypt.
    assert "fun peekVersion(frame: ByteArray, length: Int): Int" in control_kt
    assert "public static bool TryPeekVersion(byte[] frame, int length, out byte version)" in control_cs
    assert "public static bool TryPeekProtocolVersion(byte[] data, out byte version)" in pipeline

    # Discovery freshness: an announce must echo a probe this phone actually sent recently.
    # The HMAC proves the sender holds the key; the nonce proves the reply is this round's,
    # together closing the recorded-replay hole. Both the ring and the counter must exist.
    assert "ProbeNonceRing" in control_channel
    assert "staleAnnounceCount" in control_channel
    assert "isFresh(announce.nonce)" in control_channel

    # Liveness is measured on the monotonic clock everywhere a backward wall-clock jump would
    # otherwise make a dead link look alive: service policy, control channel, and all three
    # consumers of the timestamps.
    for source, label in (
        (service, "MicStreamingService.kt"),
        (control_channel, "ControlChannel.kt"),
        (main_activity, "MainActivity.kt"),
        (diagnostics_screen, "DiagnosticsScreen.kt"),
        (diagnostics_log, "DiagnosticsLog.kt"),
    ):
        assert "SystemClock.elapsedRealtime()" in source, f"monotonic clock missing from {label}"

    # The choppy-audio fix: capture and send live on separate coroutines, the queue is bounded
    # and drops the oldest rather than ever blocking capture, and the mutable encryptor buffer
    # is copied at the queue boundary so the asynchronous send can never see it overwritten.
    assert "BufferOverflow.DROP_OLDEST" in service
    assert "SenderTelemetry" in service
    assert "sendQueue" in service
    assert "packetsDropped" in service
    assert "packetBytes.copyOf()" in service

    # The staticy-audio fix: the default VOICE path is exactly one processing layer (the
    # device's VOICE_COMMUNICATION source). No software audiofx effects may be stacked.
    assert "audiofx" not in service
    assert "createStartedAudioRecord" in service

    # The Wi-Fi lock is a policy choice fed by the live network, not a constant: it re-evaluates
    # against band/RSSI and receiver loss, and can release and re-acquire on a mode change.
    assert "desiredWifiLockMode" in service
    assert "revalidateWifiLock" in service
    assert "WIFI_MODE_FULL_LOW_LATENCY" in service

    # One default port, one validation rule: the phone-side defaults and the connection rules
    # live in MicConfig so the service and the activity cannot drift.
    assert "const val DEFAULT_PORT = 49_500" in mic_config
    assert "NOTIFICATION_ID = DEFAULT_PORT" in service
    assert "fun validateConnection(): String?" in mic_config
    assert "config.validateConnection()" in service

    # Reconnect. The decision half is pure so it can be tested off-device, and the invariant it
    # protects is that a recovered link never restarts the packet sequence.
    assert "RECONNECTING," in streaming_state
    assert "LinkAction.ENTER_RECONNECTING" in service
    assert "LinkAction.RESUME_STREAMING" in service
    assert "LinkAction.GIVE_UP" in service
    assert "ControlChannel.setProbing(this@MicStreamingService, config.port, enabled = true)" in service
    assert "sequence = 0" not in reconnect_policy
    require("android/app/src/main/java/com/ryanspice/pocketmic/ReconnectPolicy.kt")
    require("android/app/src/test/java/com/ryanspice/pocketmic/ReconnectPolicyTest.kt")
    require("windows-receiver-tests/AnalyticsTests.cs")

    # Silence must be excluded from the level statistics rather than saturating them, and the
    # receiver must be able to notice that audio has stopped without a packet to tell it.
    assert "rms > 0f ? 20f * MathF.Log10(rms) : float.NaN" in core_sources["PocketMicEngine.cs"]
    assert "float.IsNaN(dbfs) ? PacketSample.NoLevel" in analytics_collector
    assert "CheckForSilence(runId)" in core_sources["PocketMicEngine.cs"]
    assert "EngineStatus.PhonePaused" in core_sources["PocketMicEngine.cs"]
    assert "EngineStatus.PhoneLost" in core_sources["PocketMicEngine.cs"]

    # Both front ends enforce single instance, which is what makes the shared settings file safe.
    assert "SingleInstance.FindOthers()" in receiver
    assert "SingleInstance.CloseOthersAsync(" in receiver
    assert "SilentTakeover" in receiver
    assert "public bool SilentTakeover { get; set; }" in core_sources["ReceiverSettings.cs"]

    # Release payload shape. Only one front end may carry a private copy of .NET: the WinForms
    # receiver, which is the fallback and therefore the one that has to run on a machine with
    # nothing installed. Flipping the WinUI app back to self-contained silently doubles the
    # download, so the pairing is asserted rather than left to whoever edits the file next.
    assert "<SelfContained>false</SelfContained>" in winui_project
    assert "<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>" in winui_project
    assert "--self-contained true" in publish_script

    # The notification-area icon is hand-rolled so the WinUI app does not acquire a desktop
    # framework through System.Drawing.Common, which is what H.NotifyIcon.WinUI reaches the tray
    # through. Taking the package back would undo that without anything else noticing.
    tray = read("windows-receiver-winui/TrayIcon.cs")
    assert 'Include="H.NotifyIcon' not in winui_project
    assert 'Include="System.Drawing' not in winui_project
    assert "Shell_NotifyIconW" in tray
    # An icon that is not re-added when Explorer restarts leaves a running app unreachable.
    assert '"TaskbarCreated"' in tray

    # Restore and build must stay separate invocations: the WinUI targets arrive through NuGet and
    # are not imported by a build that restores them in the same MSBuild run, so a clean clone
    # silently produces a WinForms-only release.
    assert "/t:Restore `" in publish_script
    assert "/t:Build `" in publish_script
    assert "/t:Restore`;Build" not in publish_script

    # Which makes the launcher's runtime check load-bearing: without it the default path starts a
    # framework-dependent app on a machine that cannot run it.
    assert "SharedFramework.Satisfies(requirements)" in launcher
    assert '"includedFrameworks"' in launcher
    assert 'Path.Combine(root, "shared", requirement.Name)' in launcher
    assert "version.Major == requirement.Version.Major" in launcher
    assert "--classic" in launcher

    assert "testDebugUnitTest" in build_script
    assert "lintDebug" in build_script
    assert "app-release-unsigned.apk" in build_script
    assert "$LASTEXITCODE" in build_script
    assert "$LASTEXITCODE" in publish_script
    assert "PocketMicReceiver.exe" in publish_script

    # The firewall script has to open the control port too, or discovery never answers.
    assert "ValidateRange(1, 65534)" in firewall_script
    assert "$ControlPort = $Port + 1" in firewall_script
    assert "-LocalPort $wanted" in firewall_script
    assert "-Profile Private" in firewall_script

    require("android/app/src/test/java/com/ryanspice/pocketmic/PacketCryptoTest.kt")
    require("android/app/src/test/java/com/ryanspice/pocketmic/ControlProtocolTest.kt")
    require("windows-receiver-tests/ControlProtocolTests.cs")
    require("windows-receiver-tests/AudioPipelineTests.cs")
    assert "<TargetFramework>net8.0-windows</TargetFramework>" in tests_project
    assert r"..\windows-receiver-core\PocketMicReceiver.Core.csproj" in tests_project

    xml_files = list((ROOT / "android/app/src/main").rglob("*.xml"))
    # The WinUI markup replaces the throwaway winui-probe these checks used to cover. It is the
    # only part of the product whose UI is declared rather than written, so a truncated or
    # unbalanced XAML file is a class of breakage nothing else here would catch.
    xml_files.extend(sorted((ROOT / "windows-receiver-winui").glob("*.xaml")))
    xml_files.append(require("windows-receiver/app.manifest"))
    xml_files.append(require("windows-receiver-winui/app.manifest"))
    for project in (
        "windows-receiver/PocketMicReceiver.csproj",
        "windows-receiver-core/PocketMicReceiver.Core.csproj",
        "windows-receiver-tests/PocketMicReceiver.Tests.csproj",
        "windows-receiver-winui/PocketMicReceiver.WinUI.csproj",
        "windows-launcher/PocketMic.Launcher.csproj",
    ):
        xml_files.append(require(project))
    assert xml_files, "no XML files found"
    for xml_file in xml_files:
        ET.parse(xml_file)

    csharp_sources = [("windows-receiver/Program.cs", receiver)]
    csharp_sources.extend(core_sources.items())
    # Analytics lives in a subdirectory of Core, so it is not picked up by the glob above and
    # would otherwise be the only part of the engine never checked for truncation.
    for source in sorted((ROOT / "windows-receiver-core/Analytics").glob("*.cs")):
        csharp_sources.append((f"Analytics/{source.name}", source.read_text(encoding="utf-8")))
    for source in sorted((ROOT / "windows-receiver-tests").glob("*.cs")):
        csharp_sources.append((source.name, source.read_text(encoding="utf-8")))
    for source in sorted((ROOT / "windows-receiver-winui").glob("*.cs")):
        csharp_sources.append((f"WinUI/{source.name}", source.read_text(encoding="utf-8")))
    csharp_sources.append(("Launcher/Program.cs", launcher))
    for label, source in csharp_sources:
        balanced_csharp(source, label)

    verify_source_manifest()

    forbidden = []
    for path in ROOT.rglob("*"):
        # .gradle holds daemon-owned lock files that raise PermissionError while a build runs.
        if not path.is_file() or any(
            part in {".tools", ".gradle", ".kotlin", "build", "bin", "obj", "release"}
            for part in path.parts
        ):
            continue
        if path.name in {"CHANGELOG.md", "SOURCE_SHA256SUMS.txt"}:
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except (UnicodeDecodeError, PermissionError):
            continue
        if re.search(r"v0\.1\.[01]", text):
            forbidden.append(str(path.relative_to(ROOT)))
    assert not forbidden, f"stale version references: {forbidden}"

    print(
        "PASS: source structure, Gradle wrapper, lifecycle guards, control-channel parity, "
        "build scripts, test projects, XML, C# delimiters, and version consistency"
    )
    return 0


if __name__ == "__main__":
    sys.exit(main())
