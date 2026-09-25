# macOS receiver preview

The macOS preview receives the existing PocketMic encrypted PCM v1 stream over UDP, authenticates and decrypts it, then sends 10 ms PCM frames over loopback to the PocketMic Virtual Mic Audio Server Plug-in. The plug-in exposes a 48 kHz mono CoreAudio input device.

## Build

The repository workflow `.github/workflows/macos.yml` builds universal arm64/x86_64 unsigned ZIP artifacts for the app and virtual-microphone driver on GitHub's `macos-26` runner. Development and packaging can happen from Windows; CI does not verify installation or live audio on a physical Mac.

## External tester setup

1. Download the complete preview artifact from the workflow run named by the project owner. Before opening either archive, verify both inner ZIPs against the bundled `SHA256SUMS.txt`:

   ```sh
   shasum -a 256 -c SHA256SUMS.txt
   ```

   Continue only if both checks report `OK`. The app and driver are unsigned and not notarized. If Gatekeeper blocks the app, only use **System Settings → Privacy & Security → Open Anyway** if you intentionally trust the source and the verified artifact. If macOS reports malware, damage, or a revoked authorization, stop and report the exact alert; do not bypass that alert or disable Gatekeeper/System Integrity Protection. A managed Mac may prohibit overrides.
2. Unzip `PocketMic-macOS-unsigned.zip` and open `PocketMic.app` after the check above.
3. Unzip `PocketMicVirtualMic-driver-unsigned.zip` beside `install-driver.sh`, so `PocketMicVirtualMic.driver` is in the same folder.
4. In Terminal, run `bash ./install-driver.sh`. Terminal prompts for administrator credentials because the script installs into `/Library/Audio/Plug-Ins/HAL`; it then restarts CoreAudio. This legacy Audio Server Plug-in does not use the DriverKit system-extension activation flow, so do not expect a system-extension approval prompt. Audio apps may briefly disconnect. Restart the Mac if CoreAudio does not register the device. If macOS refuses to load the unsigned plug-in, record the alert/log and stop; do not weaken system security to continue.
5. In **System Settings → Sound → Input**, confirm **PocketMic Virtual Mic** is listed. Select it in the conferencing/recording app that should consume audio.
6. On Android, select **PCM** as the audio codec before starting the stream. The Mac preview accepts PCM v1 only; if an Opus v2 stream arrives, PocketMic displays a compatibility warning and tells you to switch the sender to PCM. The current iOS client sends PCM v1.
7. Open PocketMic, enter a pairing key, and start the receiver. On the Android/iOS sender use the Mac's private-LAN IPv4 address, UDP port `49500`, and the same pairing key.
8. Speak into the phone and confirm the receiving app meters/hears audio. Confirm that a wrong pairing key produces no audio. Do not modify or replay packets; CI protocol fixtures cover ciphertext tampering and wrong-key rejection. Repeat stop/start and a Wi-Fi reconnect.
9. When finished, run `bash ./uninstall-driver.sh` from Terminal. Confirm removal by typing `REMOVE` when prompted; CoreAudio then restarts again.

Use one receiving audio app at a time and keep PocketMic open in the foreground. The app currently supports PCM v1/manual pairing; Opus v2, receiver discovery, QR pairing, multiple simultaneous consumers, background service, signing, notarization, and store delivery are not included.

## Preview safety and acceptance

This is an unsigned development driver, not a release installer. The install script validates the bundle identifier before copying; the uninstall script checks the installed identifier and requires explicit confirmation before removing it. Do not install it on a production/managed Mac. The driver binds its audio bridge only to `127.0.0.1:49501`. Mobile clients use the encrypted LAN receiver port. The Audio Server Plug-in read callback uses a preallocated single-producer/single-consumer ring buffer and returns silence on underrun.

The physical-Mac tester owns the acceptance evidence for installation, device visibility, selected-app routing, audio quality/latency, reconnect, and uninstall. CI only establishes that Xcode/CMake can compile the unsigned artifacts.

Use the bundled [`TESTER-REPORT.md`](TESTER-REPORT.md) template to record the Mac and OS versions, artifact commit, exact apps/codecs tested, outcomes, and any logs or screenshots. Do not include the pairing key in the report.

Apple's instructions for opening an unidentified app are in [Open apps safely on your Mac](https://support.apple.com/en-ca/102445). Apple's [Audio Server Driver Plug-in guide](https://developer.apple.com/documentation/coreaudio/creating-an-audio-server-driver-plug-in) documents the HAL plug-in installation location and restart behavior. PocketMic's preview remains unsigned; these references do not imply Apple approval or successful operation on a particular Mac.
