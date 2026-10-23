import CryptoKit
import Foundation
import Network
import XCTest
@testable import PocketMic

final class PocketMicControlFeedbackTests: XCTestCase {
    private struct Vectors: Decodable {
        struct Audio: Decodable { let datagram_hex: String }
        struct Control: Decodable {
            struct Stats: Decodable { let frame_hex: String }
            let key_hex: String
            let stats: Stats
        }
        let pairing_key: String
        let key_hex: String
        let v1: Audio
        let v2: Audio
        let control: Control
    }

    private func vectors() throws -> Vectors {
        let url = try XCTUnwrap(Bundle(for: Self.self).url(forResource: "vectors", withExtension: "json"))
        return try JSONDecoder().decode(Vectors.self, from: Data(contentsOf: url))
    }

    func testStatsExactlyMatchIndependentSharedFixture() throws {
        let fixture = try vectors()
        let key = PocketMicControlFeedback.deriveKey(pairingKey: fixture.pairing_key)
        XCTAssertEqual(key.withUnsafeBytes { Data($0) }, Data(hexString: fixture.control.key_hex))
        let frame = PocketMicControlFeedback.statsFrame(using: key, packets: 7000, lost: 2, late: 3, rejected: 4)
        XCTAssertEqual(frame, Data(hexString: fixture.control.stats.frame_hex))
        XCTAssertEqual(frame.count, 256)
        XCTAssertEqual(Array(frame[50..<55]), [0, 0, 0, 0, 0]) // Buffer=0, playing=false.
        XCTAssertTrue(HMAC<SHA256>.isValidAuthenticationCode(frame.suffix(32),
            authenticating: frame.prefix(224), using: key))
        let wrongKey = PocketMicControlFeedback.deriveKey(pairingKey: "wrong-pairing-key")
        XCTAssertFalse(HMAC<SHA256>.isValidAuthenticationCode(frame.suffix(32),
            authenticating: frame.prefix(224), using: wrongKey))
        var tampered = frame
        tampered[10] ^= 1
        XCTAssertFalse(HMAC<SHA256>.isValidAuthenticationCode(tampered.suffix(32),
            authenticating: tampered.prefix(224), using: key))
    }

    func testOnlyAuthenticatedSupportedForwardAudioCanProduceFeedback() throws {
        let fixture = try vectors()
        let key = SymmetricKey(data: try XCTUnwrap(Data(hexString: fixture.key_hex)))
        let packet = try XCTUnwrap(Data(hexString: fixture.v1.datagram_hex))
        let frame = try XCTUnwrap(PocketMicReceiver.decryptPCMv1(packet, using: key))
        var state = PocketMicReceptionState()
        XCTAssertEqual(state.accept(frame), .accepted)
        XCTAssertTrue(state.feedbackIsDue(at: 0))
        XCTAssertEqual(state.accept(frame), .late) // Replayed sequence cannot refresh liveness.
        XCTAssertEqual(state.packets, 1)
        XCTAssertEqual(state.late, 1)

        var tampered = packet
        tampered[30] ^= 1
        XCTAssertNil(PocketMicReceiver.decryptPCMv1(tampered, using: key))
        XCTAssertNil(PocketMicReceiver.decryptPCMv1(packet,
            using: SymmetricKey(data: Data(repeating: 0xff, count: 32))))
        XCTAssertNil(PocketMicReceiver.decryptPCMv1(try XCTUnwrap(Data(hexString: fixture.v2.datagram_hex)), using: key))
        XCTAssertNil(PocketMicReceiver.decryptPCMv1(packet.dropLast(), using: key))
        // Each failed decrypt is rejected before accept()/feedbackIsDue() in the receive path.
        XCTAssertEqual(state.packets, 1)
    }

    func testReceptionCadenceBeyondAndroidGiveUpDeadlineAndSessionLifecycle() {
        var state = PocketMicReceptionState()
        var feedbackTimes = [TimeInterval]()
        for tick in 0...7000 {
            XCTAssertEqual(state.accept(frame(sequence: UInt32(tick))), .accepted)
            let now = TimeInterval(tick) / 100
            if state.feedbackIsDue(at: now) { feedbackTimes.append(now) }
        }
        XCTAssertEqual(state.packets, 7001)
        XCTAssertEqual(feedbackTimes.count, 141)
        XCTAssertEqual(feedbackTimes.last, 70)
        for pair in zip(feedbackTimes, feedbackTimes.dropFirst()) {
            XCTAssertLessThan(pair.1 - pair.0, 2) // Android's existing stats timeout.
        }
        XCTAssertEqual(state.accept(frame(sequence: 7003)), .accepted)
        XCTAssertEqual(state.lost, 2)
        XCTAssertEqual(state.accept(frame(sequence: 7002)), .late)
        XCTAssertEqual(state.accept(frame(session: 2, sequence: 0)), .accepted)
        XCTAssertTrue(state.feedbackIsDue(at: 70.1)) // Genuine new session gets prompt feedback.
        XCTAssertEqual(state.accept(frame(sequence: 7004)), .rejectedSession)
        XCTAssertEqual(state.rejected, 1)

        // stop()/start() replaces this entire state; old-run callbacks are separately guarded.
        state = PocketMicReceptionState()
        XCTAssertEqual(state.packets, 0)
        XCTAssertEqual(state.accept(frame(sequence: 0)), .accepted)
        XCTAssertTrue(state.feedbackIsDue(at: 71))
    }

    func testSequenceWrapAndSessionLimitRemainBounded() {
        var state = PocketMicReceptionState()
        XCTAssertEqual(state.accept(frame(sequence: UInt32.max)), .accepted)
        XCTAssertEqual(state.accept(frame(sequence: 0)), .accepted)
        XCTAssertEqual(state.lost, 0)
        for session in 2...64 {
            XCTAssertEqual(state.accept(frame(session: UInt64(session), sequence: 0)), .accepted)
        }
        XCTAssertEqual(state.accept(frame(session: 65, sequence: 0)), .rejectedSession)
        XCTAssertEqual(state.packets, 65)
    }

    func testUnreadyOrBusyTransportDoesNotQueueOrConsumeFeedbackCadence() {
        var state = PocketMicReceptionState()
        XCTAssertEqual(state.accept(frame(sequence: 0)), .accepted)
        XCTAssertFalse(state.feedbackIsDue(at: 0, transportReady: false))
        XCTAssertFalse(state.feedbackIsDue(at: 10, transportReady: false))
        // Becoming ready itself does not send anything; the next accepted PCM invokes the gate.
        XCTAssertEqual(state.accept(frame(sequence: 1)), .accepted)
        XCTAssertTrue(state.feedbackIsDue(at: 10.01, transportReady: true))
        XCTAssertEqual(state.accept(frame(sequence: 2)), .accepted)
        XCTAssertFalse(state.feedbackIsDue(at: 11, sendInFlight: true))
        XCTAssertEqual(state.accept(frame(sequence: 3)), .accepted)
        XCTAssertTrue(state.feedbackIsDue(at: 11.01))
        XCTAssertFalse(state.feedbackIsDue(at: 11.02))
    }

    func testFeedbackUsesActualLANHostAndConfiguredCompanionPort() throws {
        for audioPort in [UInt16(49_500), 51_000, 65_534] {
            let route = try XCTUnwrap(PocketMicFeedbackRoute(
                localEndpoint: endpoint("192.168.1.20", audioPort),
                remoteEndpoint: endpoint("192.168.1.30", 43_210), audioPort: audioPort))
            XCTAssertEqual(route.local, endpoint("192.168.1.20", audioPort + 1))
            // Android listens on audioPort+1, not its ephemeral audio-source port.
            XCTAssertEqual(route.remote, endpoint("192.168.1.30", audioPort + 1))
        }
        XCTAssertNil(PocketMicFeedbackRoute(localEndpoint: endpoint("0.0.0.0", 49_500),
            remoteEndpoint: endpoint("192.168.1.30", 43_210), audioPort: 49_500))
        XCTAssertNil(PocketMicFeedbackRoute(localEndpoint: endpoint("127.0.0.1", 49_500),
            remoteEndpoint: endpoint("127.0.0.1", 43_210), audioPort: 49_500))
        XCTAssertNil(PocketMicFeedbackRoute(localEndpoint: nil,
            remoteEndpoint: endpoint("192.168.1.30", 43_210), audioPort: 49_500))
        XCTAssertFalse(PocketMicFeedbackRoute.validAudioPort(0))
        XCTAssertFalse(PocketMicFeedbackRoute.validAudioPort(49_501)) // Driver bridge reserved.
        XCTAssertFalse(PocketMicFeedbackRoute.validAudioPort(65_535)) // No companion port.
    }

    private func frame(session: UInt64 = 1, sequence: UInt32) -> PocketMicDecodedFrame {
        PocketMicDecodedFrame(sessionID: session, sequence: sequence, pcm: Data(repeating: 0, count: 960))
    }

    private func endpoint(_ address: String, _ port: UInt16) -> NWEndpoint {
        .hostPort(host: .ipv4(IPv4Address(address)!), port: NWEndpoint.Port(rawValue: port)!)
    }
}

private extension Data {
    init?(hexString: String) {
        guard hexString.count.isMultiple(of: 2) else { return nil }
        var data = Data()
        var index = hexString.startIndex
        while index < hexString.endIndex {
            let next = hexString.index(index, offsetBy: 2)
            guard let byte = UInt8(hexString[index..<next], radix: 16) else { return nil }
            data.append(byte)
            index = next
        }
        self = data
    }
}
