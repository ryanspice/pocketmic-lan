#!/usr/bin/env python3
"""Validate the unsigned macOS preview artifacts before GitHub uploads them."""

from __future__ import annotations

import argparse
import plistlib
import shutil
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path, PurePosixPath


EXPECTED_APP_ID = "com.canopydigital.pocketmic.mac"
EXPECTED_DRIVER_ID = "com.canopydigital.pocketmic.virtual-mic"
EXPECTED_VERSION = "0.1.6"
EXPECTED_BUILD = "6"
EXPECTED_ARCHES = {"arm64", "x86_64"}
REQUIRED_SUPPORT_FILES = {
    "README.md",
    "TESTER-REPORT.md",
    "install-driver.sh",
    "uninstall-driver.sh",
    "THIRD-PARTY-NOTICES.md",
    "LICENSE.libASPL",
}


def fail(message: str) -> None:
    raise SystemExit(f"macOS artifact verification failed: {message}")


def extract_safely(archive: Path, destination: Path) -> None:
    try:
        with zipfile.ZipFile(archive) as bundle:
            for info in bundle.infolist():
                path = PurePosixPath(info.filename)
                if path.is_absolute() or ".." in path.parts:
                    fail(f"unsafe ZIP member in {archive.name}: {info.filename}")
            bundle.extractall(destination)
    except (OSError, zipfile.BadZipFile) as error:
        fail(f"cannot read {archive.name}: {error}")


def read_bundle_plist(bundle: Path) -> tuple[dict[str, object], Path]:
    plist_path = bundle / "Contents" / "Info.plist"
    try:
        with plist_path.open("rb") as stream:
            info = plistlib.load(stream)
    except (OSError, plistlib.InvalidFileException) as error:
        fail(f"invalid bundle plist at {plist_path}: {error}")
    if not isinstance(info, dict):
        fail(f"bundle plist is not a dictionary: {plist_path}")

    executable_name = info.get("CFBundleExecutable")
    if not isinstance(executable_name, str) or not executable_name:
        fail(f"CFBundleExecutable is missing in {plist_path}")
    executable = bundle / "Contents" / "MacOS" / executable_name
    if not executable.is_file():
        fail(f"bundle executable is missing: {executable}")
    return info, executable


def verify_bundle(
    bundle: Path,
    *,
    bundle_id: str,
    version: str,
    build: str,
    development_region: str | None = None,
) -> None:
    info, executable = read_bundle_plist(bundle)
    if info.get("CFBundleIdentifier") != bundle_id:
        fail(f"{bundle.name} has bundle ID {info.get('CFBundleIdentifier')!r}, expected {bundle_id!r}")
    if info.get("CFBundleShortVersionString") != version:
        fail(f"{bundle.name} has version {info.get('CFBundleShortVersionString')!r}, expected {version!r}")
    if info.get("CFBundleVersion") != build:
        fail(f"{bundle.name} has an unexpected build number: {info.get('CFBundleVersion')!r}")
    if development_region and info.get("CFBundleDevelopmentRegion") != development_region:
        fail(
            f"{bundle.name} has development region {info.get('CFBundleDevelopmentRegion')!r}, "
            f"expected {development_region!r}"
        )

    lipo = shutil.which("lipo")
    if not lipo:
        fail("lipo is unavailable; run this check on a macOS runner")
    result = subprocess.run([lipo, "-archs", str(executable)], capture_output=True, text=True)
    if result.returncode != 0:
        fail(f"cannot inspect architectures for {executable}: {result.stderr.strip()}")
    arches = set(result.stdout.split())
    if arches != EXPECTED_ARCHES:
        fail(f"{bundle.name} architectures are {sorted(arches)}, expected {sorted(EXPECTED_ARCHES)}")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("artifact_dir", type=Path)
    args = parser.parse_args()

    artifact_dir = args.artifact_dir.resolve()
    app_zip = artifact_dir / "PocketMic-macOS-unsigned.zip"
    driver_zip = artifact_dir / "PocketMicVirtualMic-driver-unsigned.zip"
    for path in (app_zip, driver_zip):
        if not path.is_file():
            fail(f"required archive is missing: {path}")

    missing_files = sorted(name for name in REQUIRED_SUPPORT_FILES if not (artifact_dir / name).is_file())
    if missing_files:
        fail(f"tester support files are missing: {', '.join(missing_files)}")

    for script_name in ("install-driver.sh", "uninstall-driver.sh"):
        result = subprocess.run(["bash", "-n", str(artifact_dir / script_name)], capture_output=True, text=True)
        if result.returncode != 0:
            fail(f"{script_name} has a shell syntax error: {result.stderr.strip()}")

    with tempfile.TemporaryDirectory(prefix="pocketmic-macos-artifact-") as temporary:
        root = Path(temporary)
        app_root = root / "app"
        driver_root = root / "driver"
        app_root.mkdir()
        driver_root.mkdir()
        extract_safely(app_zip, app_root)
        extract_safely(driver_zip, driver_root)

        app_bundle = app_root / "PocketMic.app"
        driver_bundle = driver_root / "PocketMicVirtualMic.driver"
        if not app_bundle.is_dir() or not driver_bundle.is_dir():
            fail("ZIP archive does not contain the expected PocketMic app and virtual-mic driver bundles")
        verify_bundle(
            app_bundle,
            bundle_id=EXPECTED_APP_ID,
            version=EXPECTED_VERSION,
            build=EXPECTED_BUILD,
            development_region="en-CA",
        )
        verify_bundle(
            driver_bundle,
            bundle_id=EXPECTED_DRIVER_ID,
            version=EXPECTED_VERSION,
            build=EXPECTED_VERSION,
        )

    print("macOS preview archives, bundle identity/version, en-CA fallback, universal binaries, and tester files verified.")


if __name__ == "__main__":
    try:
        main()
    except SystemExit:
        raise
    except Exception as error:  # Keep unexpected packaging errors actionable in CI.
        print(f"macOS artifact verification failed: {error}", file=sys.stderr)
        raise SystemExit(1) from error
