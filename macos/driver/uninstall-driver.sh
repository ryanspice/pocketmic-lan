#!/bin/bash
set -euo pipefail

DESTINATION="/Library/Audio/Plug-Ins/HAL/PocketMicVirtualMic.driver"
if [[ -d "$DESTINATION" ]]; then
  PLIST="$DESTINATION/Contents/Info.plist"
  if [[ ! -f "$PLIST" ]]; then
    echo "Refusing to remove a bundle without Contents/Info.plist: $DESTINATION" >&2
    exit 1
  fi

  BUNDLE_ID="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$PLIST" 2>/dev/null || true)"
  if [[ "$BUNDLE_ID" != "com.canopydigital.pocketmic.virtual-mic" ]]; then
    echo "Refusing to remove unexpected bundle identifier: ${BUNDLE_ID:-<missing>}" >&2
    exit 1
  fi

  read -r -p "Remove PocketMic Virtual Mic from $DESTINATION? Type REMOVE to continue: " confirmation
  if [[ "$confirmation" != "REMOVE" ]]; then
    echo "Cancelled. No files were removed."
    exit 0
  fi

  sudo /bin/rm -rf "$DESTINATION"
  sudo /usr/bin/killall coreaudiod || true
  echo "Removed PocketMic Virtual Mic driver. Restart the Mac if it remains in the device list."
else
  echo "PocketMic Virtual Mic driver is not installed."
fi
