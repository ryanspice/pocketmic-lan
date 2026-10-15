#!/usr/bin/env python3
"""Check that desktop, phone and Apple development artifacts identify the same release."""
from pathlib import Path
import os
import re
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]


def read(name: str) -> str:
    return (ROOT / name).read_text(encoding="utf-8")


def value(pattern: str, source: str) -> str:
    match = re.search(pattern, source)
    if not match:
        raise ValueError(f"Missing version field: {pattern}")
    return match.group(1)


def main() -> None:
    android = read("android/app/build.gradle.kts")
    version = value(r'versionName = "(\d+\.\d+\.\d+)"', android)
    major, minor, patch = map(int, version.split("."))
    # Build numbers remain independent of marketing versions: 0.1.7 corrects
    # the mislabeled 1.7.0 release without downgrading its published build 10700.
    build = value(r'versionCode = (\d+)', android)
    minimum_build = max(major * 10000 + minor * 100 + patch, 10700)
    assert int(build) >= minimum_build, "Android build number would downgrade a published build"
    for project in ("windows-receiver/PocketMicReceiver.csproj",
                    "windows-receiver-core/PocketMicReceiver.Core.csproj",
                    "windows-receiver-winui/PocketMicReceiver.WinUI.csproj"):
        assert ET.fromstring(read(project)).findtext(".//Version") == version, project
    for project in ("ios/project.yml", "macos/project.yml"):
        source = read(project)
        assert value(r'MARKETING_VERSION: "([^"]+)"', source) == version, project
        assert value(r'CURRENT_PROJECT_VERSION: "([^"]+)"', source) == build, project
    assert value(r'set\(DRIVER_VERSION ([^)]+)\)', read("macos/driver/CMakeLists.txt")) == version
    verifier = read("scripts/verify_macos_artifacts.py")
    assert value(r'EXPECTED_VERSION = "([^"]+)"', verifier) == version
    assert value(r'EXPECTED_BUILD = "([^"]+)"', verifier) == build
    if os.environ.get("GITHUB_REF_TYPE") == "tag":
        assert os.environ.get("GITHUB_REF_NAME") == f"v{version}", "Tag and embedded version differ"
    print(f"PASS: all platform versions {version}; Android and Apple build {build}")


if __name__ == "__main__":
    main()
