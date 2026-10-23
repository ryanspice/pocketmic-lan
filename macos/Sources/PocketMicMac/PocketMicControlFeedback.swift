import CryptoKit
import Foundation
import Network

/// The existing Android/Windows PMCTL v1 STATS frame. No playback state is inferred from UDP.
enum PocketMicControlFeedback {
    static func deriveKey(pairingKey: String) -> SymmetricKey {
        let master = SymmetricKey(data: SHA256.hash(data: Data(pairingKey.utf8)))
        return SymmetricKey(data: HMAC<SHA256>.authenticationCode(
            for: Data("pocketmic-control-v1".utf8), using: master))
    }

    static func statsFrame(using key: SymmetricKey, packets: UInt64, lost: UInt64,
                           late: UInt64, rejected: UInt64) -> Data {
        var frame = Data(repeating: 0, count: 224)
        frame.replaceSubrange(0..<10, with: [0x50, 0x4d, 0x43, 0x54, 0x4c, 1, 3, 0, 0, 45])
        for (index, value) in [packets, lost, late, rejected, UInt64(0)].enumerated() {
            let offset = 10 + index * 8
            for byte in 0..<8 {
                frame[offset + byte] = UInt8(truncatingIfNeeded: value >> (56 - byte * 8))
            }
        }
        // No bridge/CoreAudio acknowledgement exists: trimmed, buffer depth and playing stay 0.
        frame.append(contentsOf: HMAC<SHA256>.authenticationCode(for: frame, using: key))
        return frame
    }
}

/// Only authenticated, forward PCM can advance reception state or schedule delivery feedback.
struct PocketMicReceptionState {
    enum Acceptance: Equatable { case accepted, late, rejectedSession }
    private(set) var packets: UInt64 = 0
    private(set) var lost: UInt64 = 0
    private(set) var late: UInt64 = 0
    private(set) var rejected: UInt64 = 0
    private var activeSessionID: UInt64?
    private var lastSequence: UInt32?
    private var seenSessionIDs = Set<UInt64>()
    private var lastFeedbackAt: TimeInterval?

    mutating func reject() { rejected += 1 }

    mutating func accept(_ frame: PocketMicDecodedFrame) -> Acceptance {
        if activeSessionID != frame.sessionID {
            guard seenSessionIDs.count < 64, !seenSessionIDs.contains(frame.sessionID) else {
                rejected += 1
                return .rejectedSession
            }
            seenSessionIDs.insert(frame.sessionID)
            activeSessionID = frame.sessionID
            lastSequence = nil
            lastFeedbackAt = nil
        }
        if let lastSequence {
            guard PocketMicSequenceOrder.isForward(frame.sequence, after: lastSequence) else {
                late += 1
                return .late
            }
            lost += UInt64(frame.sequence &- (lastSequence &+ 1))
        }
        lastSequence = frame.sequence
        packets += 1
        return .accepted
    }

    /// Called only in the accepted-frame path, never from a free-running heartbeat timer.
    mutating func feedbackIsDue(at now: TimeInterval, transportReady: Bool = true,
                                sendInFlight: Bool = false) -> Bool {
        guard transportReady, !sendInFlight else { return false }
        if let lastFeedbackAt, now - lastFeedbackAt < 0.5 { return false }
        lastFeedbackAt = now
        return true
    }
}

/// Bind the return channel to the actual receiving LAN address, never a wildcard listener.
/// The virtual microphone owns 127.0.0.1:49501, which is also the default LAN control port.
struct PocketMicFeedbackRoute: Equatable {
    let local: NWEndpoint
    let remote: NWEndpoint

    static func validAudioPort(_ number: UInt16) -> Bool {
        (1...65_534).contains(number) && number != 49_501
    }

    init?(localEndpoint: NWEndpoint?, remoteEndpoint: NWEndpoint, audioPort: UInt16) {
        guard Self.validAudioPort(audioPort),
              let localEndpoint,
              case .hostPort(let localHost, _) = localEndpoint,
              case .ipv4(let localAddress) = localHost,
              localAddress != .any,
              case .hostPort(let remoteHost, _) = remoteEndpoint,
              case .ipv4 = remoteHost,
              let controlPort = NWEndpoint.Port(rawValue: audioPort + 1) else { return nil }
        // A same-machine sender on the default port cannot share the driver's loopback bind.
        guard controlPort.rawValue != 49_501 || localAddress.rawValue.first != 127 else { return nil }
        local = .hostPort(host: localHost, port: controlPort)
        remote = .hostPort(host: remoteHost, port: controlPort)
    }

    func makeConnection() -> NWConnection {
        let parameters = NWParameters.udp
        parameters.requiredLocalEndpoint = local
        // A rapid authenticated peer change/restart may overlap the preceding cancellation.
        parameters.allowLocalEndpointReuse = true
        return NWConnection(to: remote, using: parameters)
    }
}
