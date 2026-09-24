#!/bin/bash
set -euo pipefail

DESTINATION="/Library/Audio/Plug-Ins/HAL/PocketMicVirtualMic.driver"
if [[ -d "$DESTINATION" ]]; then
  sudo /bin/rm -rf "$DESTINATION"
  sudo /usr/bin/killall coreaudiod || true
  echo "Removed PocketMic Virtual Mic driver. Restart the Mac if it remains in the device list."
else
  echo "PocketMic Virtual Mic driver is not installed."
fi
