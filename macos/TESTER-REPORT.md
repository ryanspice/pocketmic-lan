# PocketMic macOS Preview — Tester Report

Please complete this after testing the preview. Do not include the pairing key, private account details, or unrelated personal data. Attach screenshots or logs only when they help explain a result, and redact local usernames and network addresses.

## Test environment

- Test date and time zone:
- Mac model / chip:
- macOS version:
- Artifact workflow run or commit:
- App build/version (if shown):
- Sender device and OS:
- Sender app version/build:
- Receiving audio app and version:
- Network type (home Wi-Fi, hotspot, other):
- Codec selected on sender: PCM / Opus / other

## Results

Use `PASS`, `FAIL`, `NOT TESTED`, or `BLOCKED`; include a short note for failures or blocked steps.

| Check | Result | Notes / evidence reference |
|---|---|---|
| App archive unzipped and opened | | |
| Driver installed using the documented script | | |
| macOS approval completed; PocketMic Virtual Mic appears in Sound → Input | | |
| PocketMic Virtual Mic selected in the receiving app | | |
| Sender paired using the Mac's private-LAN address and matching key | | |
| Sender microphone audio is visible/audible in the receiving app | | |
| Wrong pairing key produces no audio | | |
| Android PCM v1 stream works | | |
| iOS PCM v1 stream works | | |
| Android Opus v2 produces the expected compatibility warning and no misleading success state | | |
| Stop and start works repeatedly | | |
| Wi-Fi interruption and reconnect behavior is understandable and recovers as documented | | |
| Driver uninstall script removes PocketMic Virtual Mic | | |
| Any unexpected audio-app disconnect or system recovery issue | | |

## Summary

- Overall: PASS / FAIL / PARTIAL
- Most important issue to fix:
- Would you use this preview again after that issue is fixed? Yes / No / Unsure
- Attachments or log references:

Do not test modified or replayed traffic unless the project owner provides a dedicated test procedure. The CI protocol fixtures cover ciphertext tampering and wrong-key rejection; physical app routing still requires this report.
