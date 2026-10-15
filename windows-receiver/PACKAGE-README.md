# PocketMic LAN — Windows receiver

This x64 Windows preview receives encrypted audio from PocketMic over your private local network. The ZIP includes .NET and the Windows App SDK; you do not need to install either runtime separately. The Windows preview is not code-signed.

## Try audio through your speakers

1. Extract every file from the ZIP into one folder.
2. Run `Install-PocketMic.cmd` to install for your Windows account and create Desktop and Start Menu shortcuts. Settings are preserved, and an existing install is retained in a timestamped backup folder. You can also run `PocketMicReceiver.exe --show` directly for portable use.
3. On Android, scan the QR code shown by the receiver. On iOS, enter the receiver's IP address, audio port, and pairing key in the app; iOS QR pairing is not included in this preview.
4. Select your speakers in PocketMic Receiver, then start the mobile app's microphone stream.

## Route audio into another Windows app

1. Install VB-Audio VB-CABLE separately from [vb-audio.com/Cable](https://vb-audio.com/Cable/).
2. In PocketMic Receiver, choose `CABLE Input` as the playback device.
3. In the target app (for example, Discord, Teams, OBS, or a game), choose `CABLE Output` as its microphone.

VB-CABLE is third-party software and is not included in this ZIP. The receiver's **Use PocketMic as Windows microphone** control changes Windows recording defaults; check the selected cable before using it.

## Network and troubleshooting

- Keep the phone and PC on the same private network. Do not port-forward the receiver.
- The default UDP audio port is `49500`. If Windows Firewall prompts, allow PocketMic only on a trusted **Private network**.
- Pairing keys must match. A wrong key prevents audio from being accepted.
- If the phone cannot discover the PC, start the receiver first, verify its displayed IP and port, and use manual entry.
- Select the PC speakers first to separate receiver playback issues from VB-CABLE or target-app routing issues.
- `opus.dll` enables Opus v2 reception. PCM v1 remains available if the native decoder is unavailable.
- Windows 11 22H2 and newer open the WinUI 3 interface with Mica as the window canvas and native Fluent controls. Windows manages its light/dark, accessibility and material fallback policy. Older Windows opens the classic interface; `PocketMicReceiver.exe --classic --show` explicitly selects it for troubleshooting.
- Keep the `modern` directory with the main executable. It contains the modern interface and its runtime.
- Opening PocketMic again brings the existing receiver forward. Closing the window leaves it running in the notification area; choose **Exit** from the PocketMic tray menu to stop it. The tray icon remains present while the window is visible too.

For current project information and source, visit [PocketMic LAN on GitHub](https://github.com/ryanspice/pocketmic-lan). License notices are in the accompanying `PocketMic-LICENSE.txt` and `THIRD-PARTY-NOTICES.txt`; `OPUS-COPYING.txt` is also included when `opus.dll` is present.
