import SwiftUI

struct ContentView: View {
    @StateObject private var streamer = AudioStreamer()
    @State private var host = ""
    @State private var port = "49500"
    @State private var pairingKey = ""

    var body: some View {
        NavigationStack {
            Form {
                Section("PocketMic receiver") {
                    TextField("IP address or host name", text: $host)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                        .keyboardType(.URL)
                    TextField("Audio port", text: $port)
                        .keyboardType(.numberPad)
                    SecureField("Pairing key", text: $pairingKey)
                        .textInputAutocapitalization(.never)
                        .autocorrectionDisabled()
                }
                .disabled(streamer.isStreaming || streamer.isStarting)

                Section {
                    HStack(spacing: 12) {
                        Circle()
                            .fill(streamer.isStreaming ? .green : .gray)
                            .frame(width: 10, height: 10)
                        Text(streamer.status)
                            .font(.subheadline)
                    }
                    Button(buttonTitle) {
                        if streamer.isStreaming || streamer.isStarting {
                            streamer.stop()
                        } else {
                            streamer.start(host: host, port: port, pairingKey: pairingKey)
                        }
                    }
                    .frame(maxWidth: .infinity)
                    .fontWeight(.semibold)
                    .tint(streamer.isStreaming || streamer.isStarting ? .red : .accentColor)
                    .disabled(!streamer.isStreaming && !streamer.isStarting && (host.isEmpty || pairingKey.isEmpty))
                }

                Section("About this iOS preview") {
                    Text("Streams 48 kHz mono PCM audio to a PocketMic receiver on the same private Wi-Fi network. Enter the receiver IP address and pairing key shown in its app.")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                    Text("Keep PocketMic in the foreground while streaming. QR pairing, receiver discovery, and Opus are not included in this initial iOS client.")
                        .font(.footnote)
                        .foregroundStyle(.secondary)
                }
            }
            .navigationTitle("PocketMic")
            .alert("PocketMic issue", isPresented: Binding(
                get: { streamer.errorMessage != nil },
                set: { if !$0 { streamer.errorMessage = nil } }
            )) {
                Button("OK", role: .cancel) { streamer.errorMessage = nil }
            } message: {
                Text(streamer.errorMessage ?? "")
            }
        }
    }

    private var buttonTitle: String {
        if streamer.isStarting { return "Cancel microphone request" }
        return streamer.isStreaming ? "Stop microphone" : "Start microphone"
    }
}
