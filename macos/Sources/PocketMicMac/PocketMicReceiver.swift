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
    private var activeSessionID: UInt64?
    private var lastSequence: UInt32?
    private var seenSessionIDs = Set<UInt64>()
    private var didReportUnsupportedCodec = false

    func start(portText: String, pairingKey: String) {
        guard !isListening else { return }
        guard let portNumber = UInt16(portText), portNumber > 0,
              let port = NWEndpoint.Port(rawValue: portNumber) else {
            errorMessage = String(localized: "Enter a valid UDP port between 1 and 65535.")
            return
        }
        guard !pairingKey.isEmpty else {
            errorMessage = String(localized: "Enter the pairing key used by the mobile sender.")
            return
        }

        key = SymmetricKey(data: Data(SHA256.hash(data: Data(pairingKey.utf8))))
        authenticatedPackets = 0
        codecWarning = nil
        didReportUnsupportedCodec = false
        activeSessionID = nil
        lastSequence = nil
        seenSessionIDs.removeAll()
        errorMessage = nil

        do {
            let udpListener = try NWListener(using: .udp, on: port)
            listener = udpListener
            udpListener.stateUpdateHandler = { [weak self] state in
                Task { @MainActor in
                    guard let self else { return }
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
                Task { @MainActor in self?.accept(connection) }
            }
            udpListener.start(queue: queue)

            let local = NWConnection(host: "127.0.0.1", port: 49_501, using: .udp)
            virtualMicConnection = local
            local.start(queue: queue)
        } catch {
            key = nil
            errorMessage = String(localized: "Could not listen on UDP") + " \(portNumber): \(error.localizedDescription)"
        }
    }

    func stop() {
        listener?.cancel()
        listener = nil
        for connection in connections.values {
            connection.cancel()
        }
        connections.removeAll()
        virtualMicConnection?.cancel()
        virtualMicConnection = nil
        key = nil
        activeSessionID = nil
        lastSequence = nil
        seenSessionIDs.removeAll()
        isListening = false
        status = String(localized: "Ready")
        didReportUnsupportedCodec = false
    }

    private func accept(_ connection: NWConnection) {
        let id = ObjectIdentifier(connection)
        connections[id] = connection
        connection.stateUpdateHandler = { [weak self, weak connection] state in
            guard case .failed = state, let connection else { return }
            Task { @MainActor in
                self?.connections.removeValue(forKey: ObjectIdentifier(connection))
            }
        }
        connection.start(queue: queue)
        receiveNextMessage(from: connection)
    }

    private func receiveNextMessage(from connection: NWConnection) {
        let packetKey = key
        let bridge = virtualMicConnection
        connection.receiveMessage { [weak self, weak connection] data, _, _, error in
            guard let self, let connection else { return }
            if let data {
                if Self.isOpusV2Packet(data) {
                    Task { @MainActor in self.reportUnsupportedOpusOnce() }
                } else if let packetKey, let frame = Self.decryptPCMv1(data, using: packetKey) {
                    Task { @MainActor in
                        guard self.accept(frame) else { return }
                        bridge?.send(content: frame.pcm, completion: .contentProcessed { [weak self] error in
                            guard error == nil else { return }
                            Task { @MainActor in self?.authenticatedPackets += 1 }
                        })
                    }
                }
            }
            if error == nil {
                Task { @MainActor in self.receiveNextMessage(from: connection) }
            }
        }
    }

    private func accept(_ frame: PocketMicDecodedFrame) -> Bool {
        if activeSessionID != frame.sessionID {
            guard seenSessionIDs.count < 64, !seenSessionIDs.contains(frame.sessionID) else {
                errorMessage = String(localized: "Rejected a replayed or excessive PocketMic stream session. Restart the receiver to begin a fresh validation session.")
                return false
            }
            seenSessionIDs.insert(frame.sessionID)
            activeSessionID = frame.sessionID
            lastSequence = nil
        }
        if let lastSequence, !PocketMicSequenceOrder.isForward(frame.sequence, after: lastSequence) { return false }
        lastSequence = frame.sequence
        return true
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
