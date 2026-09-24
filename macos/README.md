# macOS receiver preview

The macOS preview receives the existing PocketMic encrypted PCM v1 stream over UDP, authenticates and decrypts it, then sends 10 ms PCM frames over loopback to the PocketMic Virtual Mic Audio Server Plug-in. The plug-in exposes a 48 kHz mono CoreAudio input device.

## Build

The repository workflow `.github/workflows/macos.yml` builds universal arm64/x86_64 unsigned ZIP artifacts for the app and virtual-microphone driver on GitHub's `macos-26` runner. Development and packaging can happen from Windows; CI does not verify installation or live audio on a physical Mac.

## External tester setup

1. Unzip `PocketMic-macOS-unsigned.zip` and open `PocketMic.app`.
2. Unzip `PocketMicVirtualMic-driver-unsigned.zip` beside `install-driver.sh`, so `PocketMicVirtualMic.driver` is in the same folder.
3. In Terminal, run `bash ./install-driver.sh`. macOS asks for administrator approval; the script installs the unsigned preview driver and restarts CoreAudio. Audio apps may briefly disconnect. Restart the Mac if macOS does not register the device.
4. In **System Settings → Sound → Input**, confirm **PocketMic Virtual Mic** is listed. Select it in the conferencing/recording app that should consume audio.
5. Open PocketMic, enter a pairing key, and start the receiver. On the Android/iOS sender use the Mac's private-LAN IPv4 address, UDP port `49500`, and the same pairing key.
6. Speak into the phone and confirm the receiving app meters/hears audio. Check that a wrong key and a modified/replayed datagram produce no audio. Repeat stop/start and a Wi-Fi reconnect.
7. When finished, run `bash ./uninstall-driver.sh` from Terminal. CoreAudio restarts again.

Use one receiving audio app at a time and keep PocketMic open in the foreground. The app currently supports PCM v1/manual pairing; Opus v2, receiver discovery, QR pairing, multiple simultaneous consumers, background service, signing, notarization, and store delivery are not included.

## Preview safety and acceptance

This is an unsigned development driver, not a release installer. The tester explicitly runs the install script with `sudo`; do not install it on a production/managed Mac. The driver binds its audio bridge only to `127.0.0.1:49501`. Mobile clients use the encrypted LAN receiver port. The Audio Server Plug-in read callback uses a preallocated single-producer/single-consumer ring buffer and returns silence on underrun.

The physical-Mac tester owns the acceptance evidence for installation, device visibility, selected-app routing, audio quality/latency, reconnect, and uninstall. CI only establishes that Xcode/CMake can compile the unsigned artifacts.
