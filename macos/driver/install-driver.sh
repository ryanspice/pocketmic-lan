#!/bin/bash
set -euo pipefail

SCRIPT_DIRECTORY="$(cd "$(dirname "$0")" && pwd)"
SOURCE="${1:-$SCRIPT_DIRECTORY/PocketMicVirtualMic.driver}"
DESTINATION="/Library/Audio/Plug-Ins/HAL/PocketMicVirtualMic.driver"

if [[ ! -d "$SOURCE" ]]; then
  echo "Driver bundle not found: $SOURCE" >&2
  echo "Build it first with the macOS CI workflow or CMake." >&2
  exit 1
fi

PLIST="$SOURCE/Contents/Info.plist"
if [[ ! -f "$PLIST" ]]; then
  echo "Driver bundle is missing Contents/Info.plist: $SOURCE" >&2
  exit 1
fi

BUNDLE_ID="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$PLIST" 2>/dev/null || true)"
if [[ "$BUNDLE_ID" != "com.canopydigital.pocketmic.virtual-mic" ]]; then
  echo "Unexpected driver bundle identifier: ${BUNDLE_ID:-<missing>}" >&2
  exit 1
fi

if [[ -e "$DESTINATION" ]]; then
  echo "A driver already exists at $DESTINATION." >&2
  echo "Uninstall the existing PocketMic preview driver first, then rerun this installer." >&2
  exit 1
fi

sudo /usr/bin/install -d "/Library/Audio/Plug-Ins/HAL"
sudo /usr/bin/ditto "$SOURCE" "$DESTINATION"
echo "Installed unsigned preview driver. Restarting CoreAudio; audio apps may briefly disconnect."
sudo /usr/bin/killall coreaudiod || true
echo "If the device does not appear, restart the Mac and check Console for PocketMicVirtualMic errors."
