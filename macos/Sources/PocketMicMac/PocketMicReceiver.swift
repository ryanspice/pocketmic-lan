import Combine
import CryptoKit
import Foundation
import Network

struct PocketMicDecodedFrame: Sendable {
    let sessionID: UInt64
    let sequence: UInt32
    let pcm: Data
}

@MainActor
final class PocketMicReceiver: ObservableObject {
    @Published private(set) var isListening = false
    @Published private(set) var status = String(localized: "Ready")
    @Published private(set) var authenticatedPackets = 0
    @Published private(set) var codecWarning: String?
    @Published var errorMessage: String?

    private let queue = DispatchQueue(label: "com.canopydigital.pocketmic.mac.receiver")
    private var listener: NWListener?
    private var connections: [ObjectIdentifier: NWConnection] = [:]
    private var virtualMicConnection: NWConnection?
    private var key: SymmetricKey?
    private var controlKey: SymmetricKey?
    private var reception = PocketMicReceptionState()
    private var feedbackConnection: NWConnection?
    private var feedbackRoute: PocketMicFeedbackRoute?
    private var feedbackReady = false
    private var feedbackInFlight = false
    private var nextFeedbackAttemptAt: TimeInterval = 0
    private var runID: UUID?
    private var audioPort: UInt16 = 49_500
    private var didReportUnsupportedCodec = false

    func start(portText: String, pairingKey: String) {
        guard listener == nil else { return }
        guard let portNumber = UInt16(portText), PocketMicFeedbackRoute.validAudioPort(portNumber),
              let port = NWEndpoint.Port(rawValue: portNumber) else {
            errorMessage = String(localized: "Enter a UDP port between 1 and 65534, except 49501 (reserved for the virtual microphone).")
            return
        }
        guard !pairingKey.isEmpty else {
            errorMessage = String(localized: "Enter the pairing key used by the mobile sender.")
            return
        }

        key = SymmetricKey(data: Data(SHA256.hash(data: Data(pairingKey.utf8))))
        controlKey = PocketMicControlFeedback.deriveKey(pairingKey: pairingKey)
        audioPort = portNumber
        let run = UUID()
        runID = run
        authenticatedPackets = 0
        codecWarning = nil
        didReportUnsupportedCodec = false
        reception = PocketMicReceptionState()
        errorMessage = nil

        do {
            let udpListener = try NWListener(using: .udp, on: port)
            listener = udpListener
            udpListener.stateUpdateHandler = { [weak self] state in
                Task { @MainActor in
                    guard let self, self.runID == run else { return }
                    switch state {
                    case .ready:
                        self.isListening = true
                        self.status = String(localized: "Listening on UDP") + " \(portNumber)"
                    case .failed(let error):
                        self.errorMessage = String(localized: "Receiver failed:") + " \(error.localizedDescription)"
                        self.stop()
                    case .cancelled:
                        self.isListening = false
                        self.status = String(localized: "Ready")
                    default:
                        break
                    }
                }
            }
            udpListener.newConnectionHandler = { [weak self] connection in
                Task { @MainActor in
                    guard let self, self.runID == run else { connection.cancel(); return }
                    self.accept(connection, run: run)
                }
            }
            udpListener.start(queue: queue)

            let local = NWConnection(host: "127.0.0.1", port: 49_501, using: .udp)
            virtualMicConnection = local
            local.start(queue: queue)
        } catch {
            stop()
            errorMessage = String(localized: "Could not listen on UDP") + " \(portNumber): \(error.localizedDescription)"
        }
    }

    func stop() {
        // Invalidate queued callbacks before cancelling sockets; an old run cannot send replies
        // or update the packet count/status of a replacement receiver.
        runID = nil
        listener?.cancel()
        listener = nil
        for connection in connections.values {
            connection.cancel()
        }
        connections.removeAll()
        virtualMicConnection?.cancel()
        virtualMicConnection = nil
        feedbackConnection?.cancel()
        feedbackConnection = nil
        feedbackRoute = nil
        feedbackReady = false
        feedbackInFlight = false
        nextFeedbackAttemptAt = 0
        key = nil
        controlKey = nil
        reception = PocketMicReceptionState()
        isListening = false
        status = String(localized: "Ready")
        didReportUnsupportedCodec = false
    }

    private func accept(_ connection: NWConnection, run: UUID) {
        let id = ObjectIdentifier(connection)
        connections[id] = connection
        connection.stateUpdateHandler = { [weak self, weak connection] state in
            guard case .failed = state, let connection else { return }
            Task { @MainActor in
                guard let self, self.runID == run else { return }
                self.connections.removeValue(forKey: ObjectIdentifier(connection))
            }
        }
        connection.start(queue: queue)
        receiveNextMessage(from: connection, run: run)
    }

    private func receiveNextMessage(from connection: NWConnection, run: UUID) {
        guard runID == run else { return }
        let packetKey = key
        connection.receiveMessage { [weak self, weak connection] data, _, _, error in
            guard let self, let connection else { return }
            // Crypto work stays off the main actor; session/sequence and run ownership are
            // checked before either the loopback bridge or feedback sees the plaintext.
            let frame = data.flatMap { packet in packetKey.flatMap { Self.decryptPCMv1(packet, using: $0) } }
            let unsupportedOpus = data.map(Self.isOpusV2Packet) ?? false
            Task { @MainActor in
                guard self.runID == run, self.connections[ObjectIdentifier(connection)] != nil else { return }
                if let frame {
                    switch self.reception.accept(frame) {
                    case .accepted:
                        self.authenticatedPackets += 1
                        self.virtualMicConnection?.send(content: frame.pcm, completion: .contentProcessed { _ in })
                        self.sendFeedbackIfDue(from: connection, run: run)
                    case .late:
                        break
                    case .rejectedSession:
                        self.errorMessage = String(localized: "Rejected a replayed or excessive PocketMic stream session. Restart the receiver to begin a fresh validation session.")
                    }
                } else if data != nil {
                    self.reception.reject()
                    if unsupportedOpus { self.reportUnsupportedOpusOnce() }
                }
                if error == nil { self.receiveNextMessage(from: connection, run: run) }
            }
        }
    }

    private func sendFeedbackIfDue(from audioConnection: NWConnection, run: UUID) {
        guard let controlKey else { return }
        let now = ProcessInfo.processInfo.systemUptime
        guard let route = PocketMicFeedbackRoute(localEndpoint: audioConnection.currentPath?.localEndpoint,
                                                remoteEndpoint: audioConnection.endpoint, audioPort: audioPort) else {
            errorMessage = String(localized: "Delivery feedback requires a private-LAN IPv4 sender and receiver address.")
            return
        }
        if feedbackRoute != route || feedbackConnection == nil {
            // A persistent bind/route failure must not create a socket for every 10 ms frame.
            guard feedbackRoute != route || now >= nextFeedbackAttemptAt else { return }
            feedbackConnection?.cancel()
            let feedback = route.makeConnection()
            feedbackRoute = route
            feedbackConnection = feedback
            feedbackReady = false
            feedbackInFlight = false
            nextFeedbackAttemptAt = now + 0.5
            feedback.stateUpdateHandler = { [weak self, weak feedback] state in
                Task { @MainActor in
                    guard let self, let feedback, self.runID == run,
                          self.feedbackConnection === feedback else { return }
                    switch state {
                    case .ready:
                        self.feedbackReady = true
                    case .failed(let error):
                        self.feedbackReady = false
                        feedback.cancel()
                        self.feedbackConnection = nil
                        self.errorMessage = String(localized: "Delivery feedback unavailable:") + " \(error.localizedDescription)"
                    default:
                        self.feedbackReady = false
                    }
                }
            }
            feedback.start(queue: queue)
        }
        // Never queue STATS while the path is preparing/waiting: a delayed stale reply would
        // otherwise look like fresh delivery after the sender or audio path had stopped.
        guard reception.feedbackIsDue(at: now,
            transportReady: feedbackReady, sendInFlight: feedbackInFlight),
              let feedback = feedbackConnection else { return }
        let frame = PocketMicControlFeedback.statsFrame(using: controlKey, packets: reception.packets,
            lost: reception.lost, late: reception.late, rejected: reception.rejected)
        feedbackInFlight = true
        feedback.send(content: frame, completion: .contentProcessed { [weak self, weak feedback] error in
            Task { @MainActor in
                guard let self, let feedback, self.runID == run,
                      self.feedbackConnection === feedback else { return }
                self.feedbackInFlight = false
                if let error {
                    self.errorMessage = String(localized: "Delivery feedback unavailable:") + " \(error.localizedDescription)"
                }
            }
        })
    }

    private func reportUnsupportedOpusOnce() {
        guard !didReportUnsupportedCodec else { return }
        didReportUnsupportedCodec = true
        codecWarning = String(localized: "Opus v2 traffic was detected, but this Mac preview accepts PCM v1. Select PCM on the sender and restart the stream.")
    }

    /// Identifies a structurally valid Opus v2 datagram without attempting to decode it.
    /// This is only a compatibility hint; packets are still authenticated before PCM playback.
    nonisolated private static func isOpusV2Packet(_ packet: Data) -> Bool {
        guard packet.count >= 45,
              packet.count <= 28 + 512 + 16,
              packet[packet.startIndex] == 0x50,
              packet[packet.startIndex + 1] == 0x4d,
              packet[packet.startIndex + 2] == 0x49,
              packet[packet.startIndex + 3] == 0x43,
              packet[packet.startIndex + 4] == 2,
              (packet[packet.startIndex + 5] & 0x03) == 0x03,
              packet.uint16BE(at: 6) == 28,
              packet.uint32BE(at: 20) == 48_000 else { return false }

        let payloadLength = Int(packet.uint32BE(at: 24))
        return (1...512).contains(payloadLength) && packet.count == 28 + payloadLength + 16
    }

    /// Opens PocketMic protocol v1 and rejects malformed or unauthenticated datagrams.
    nonisolated static func decryptPCMv1(_ packet: Data, using key: SymmetricKey) -> PocketMicDecodedFrame? {
        guard packet.count == 1_000,
              packet[packet.startIndex] == 0x50,
              packet[packet.startIndex + 1] == 0x4d,
              packet[packet.startIndex + 2] == 0x49,
              packet[packet.startIndex + 3] == 0x43,
              packet[packet.startIndex + 4] == 1,
              packet[packet.startIndex + 5] == 1,
              packet.uint16BE(at: 6) == 24,
              packet.uint32BE(at: 20) == 48_000 else { return nil }

        let header = packet.prefix(24)
        let sessionID = packet.uint64BE(at: 8)
        let sequence = packet.uint32BE(at: 16)
        let nonceData = packet.subdata(in: 8..<20)
        let ciphertext = packet.subdata(in: 24..<984)
        let tag = packet.subdata(in: 984..<1_000)
        guard let nonce = try? AES.GCM.Nonce(data: nonceData),
              let box = try? AES.GCM.SealedBox(nonce: nonce, ciphertext: ciphertext, tag: tag),
              let plaintext = try? AES.GCM.open(box, using: key, authenticating: header),
              plaintext.count == 960 else { return nil }
        return PocketMicDecodedFrame(sessionID: sessionID, sequence: sequence, pcm: plaintext)
    }
}

enum PocketMicSequenceOrder {
    /// Matches the Windows receiver's signed modular delta policy, including UInt32 rollover.
    static func isForward(_ sequence: UInt32, after lastSequence: UInt32) -> Bool {
        let delta = sequence &- (lastSequence &+ 1)
        return Int32(bitPattern: delta) >= 0
    }
}

private extension Data {
    func uint16BE(at offset: Int) -> UInt16 {
        (UInt16(self[index(startIndex, offsetBy: offset)]) << 8)
            | UInt16(self[index(startIndex, offsetBy: offset + 1)])
    }

    func uint32BE(at offset: Int) -> UInt32 {
        (UInt32(self[index(startIndex, offsetBy: offset)]) << 24)
            | (UInt32(self[index(startIndex, offsetBy: offset + 1)]) << 16)
            | (UInt32(self[index(startIndex, offsetBy: offset + 2)]) << 8)
            | UInt32(self[index(startIndex, offsetBy: offset + 3)])
    }

    func uint64BE(at offset: Int) -> UInt64 {
        (UInt64(uint32BE(at: offset)) << 32) | UInt64(uint32BE(at: offset + 4))
    }
}
