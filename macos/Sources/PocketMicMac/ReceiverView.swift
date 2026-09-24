import SwiftUI

struct ReceiverView: View {
    @StateObject private var receiver = PocketMicReceiver()
    @State private var pairingKey = ""
    @State private var port = "49500"

    var body: some View {
        VStack(alignment: .leading, spacing: 20) {
            Text("PocketMic")
                .font(.largeTitle.weight(.semibold))
            Text("macOS receiver preview")
                .font(.headline)

            GroupBox("Pairing") {
                VStack(alignment: .leading, spacing: 12) {
                    TextField("Audio port", text: $port)
                        .textFieldStyle(.roundedBorder)
                    SecureField("Pairing key", text: $pairingKey)
                        .textFieldStyle(.roundedBorder)
                    Text("Use the same pairing key in the Android or iOS sender. Enter this Mac’s local network address in the sender.")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                }
                .padding(.top, 6)
            }
            .disabled(receiver.isListening)

            GroupBox("Virtual microphone") {
                VStack(alignment: .leading, spacing: 8) {
                    Label(receiver.status, systemImage: receiver.isListening ? "waveform.circle.fill" : "waveform.circle")
                        .foregroundStyle(receiver.isListening ? .green : .secondary)
                    Text("PocketMic Virtual Mic appears in macOS sound input devices after the driver is installed. Choose it in the app that should receive audio.")
                        .font(.callout)
                        .foregroundStyle(.secondary)
                    Text("Authenticated packets: \(receiver.authenticatedPackets)")
                        .font(.system(.callout, design: .monospaced))
                    if let error = receiver.errorMessage {
                        Text(error)
                            .foregroundStyle(.red)
                            .textSelection(.enabled)
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.top, 6)
            }

            HStack {
                Button(receiver.isListening ? "Stop receiver" : "Start receiver") {
                    if receiver.isListening {
                        receiver.stop()
                    } else {
                        receiver.start(portText: port, pairingKey: pairingKey)
                    }
                }
                .keyboardShortcut(.defaultAction)
                .disabled(!receiver.isListening && pairingKey.isEmpty)

                Spacer()
                Text("PCM v1 · 48 kHz · mono")
                    .font(.caption)
                    .foregroundStyle(.secondary)
            }

            Text("Preview limitation: one receiving audio client is supported at a time. Keep PocketMic open while streaming. Opus, discovery, background service, and signed distribution are not included yet.")
                .font(.footnote)
                .foregroundStyle(.secondary)
        }
        .padding(24)
    }
}
