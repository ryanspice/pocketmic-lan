import Combine
import CryptoKit
import Foundation
import Network

private struct PocketMicDecodedFrame: Sendable {
    let sessionID: UInt64
    let sequence: UInt32
    let pcm: Data
}

@MainActor
final class PocketMicReceiver: ObservableObject {
    @Published private(set) var isListening = false
    @Published private(set) var status = "Ready"
    @Published private(set) var authenticatedPackets = 0
    @Published var errorMessage: String?

    private let queue = DispatchQueue(label: "com.canopydigital.pocketmic.mac.receiver")
    private var listener: NWListener?
    private var connections: [ObjectIdentifier: NWConnection] = [:]
    private var virtualMicConnection: NWConnection?
    private var key: SymmetricKey?
    private var activeSessionID: UInt64?
    private var lastSequence: UInt32?
    private var seenSessionIDs = Set<UInt64>()

    func start(portText: String, pairingKey: String) {
        guard !isListening else { return }
        guard let portNumber = UInt16(portText), portNumber > 0,
              let port = NWEndpoint.Port(rawValue: portNumber) else {
            errorMessage = "Enter a valid UDP port between 1 and 65535."
            return
        }
        guard !pairingKey.isEmpty else {
            errorMessage = "Enter the pairing key used by the mobile sender."
            return
        }

        key = SymmetricKey(data: Data(SHA256.hash(data: Data(pairingKey.utf8))))
        authenticatedPackets = 0
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
                        self.status = "Listening on UDP \(portNumber)"
                    case .failed(let error):
                        self.errorMessage = "Receiver failed: \(error.localizedDescription)"
                        self.stop()
                    case .cancelled:
                        self.isListening = false
                        self.status = "Ready"
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
            errorMessage = "Could not listen on UDP \(portNumber): \(error.localizedDescription)"
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
        status = "Ready"
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
            if let data, let packetKey, let frame = Self.decryptPCMv1(data, using: packetKey) {
                Task { @MainActor in
                    guard self.accept(frame) else { return }
                    bridge?.send(content: frame.pcm, completion: .contentProcessed { [weak self] error in
                        guard error == nil else { return }
                        Task { @MainActor in self?.authenticatedPackets += 1 }
                    })
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
                errorMessage = "Rejected a replayed or excessive PocketMic stream session. Restart the receiver to begin a fresh validation session."
                return false
            }
            seenSessionIDs.insert(frame.sessionID)
            activeSessionID = frame.sessionID
            lastSequence = nil
        }
        if let lastSequence, frame.sequence <= lastSequence { return false }
        lastSequence = frame.sequence
        return true
    }

    /// Opens PocketMic protocol v1 and rejects malformed or unauthenticated datagrams.
    nonisolated private static func decryptPCMv1(_ packet: Data, using key: SymmetricKey) -> PocketMicDecodedFrame? {
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
