import CryptoKit
import Foundation
import XCTest
@testable import PocketMic

final class PocketMicProtocolVectorTests: XCTestCase {
    private struct Vectors: Decodable {
        let key_hex: String
        let pairing_key: String
        let session_id: UInt64
        let sequence: UInt32
        let v1: V1

        struct V1: Decodable {
            let pcm_payload_hex: String
            let datagram_hex: String
        }
    }

    func testEmitsSharedPcmV1ProtocolFixture() throws {
        let fixtureURL = try XCTUnwrap(Bundle(for: Self.self).url(forResource: "vectors", withExtension: "json"))
        let vectors = try JSONDecoder().decode(Vectors.self, from: Data(contentsOf: fixtureURL))
        let derivedKey = Data(SHA256.hash(data: Data(vectors.pairing_key.utf8)))
        XCTAssertEqual(derivedKey, try XCTUnwrap(Data(hex: vectors.key_hex)))

        let key = SymmetricKey(data: derivedKey)
        let pcm = try XCTUnwrap(Data(hex: vectors.v1.pcm_payload_hex))
        let packet = try XCTUnwrap(AudioPacketProcessor.makePacket(
            pcm: pcm,
            key: key,
            sessionID: vectors.session_id,
            sequence: vectors.sequence
        ))

        XCTAssertEqual(packet, try XCTUnwrap(Data(hex: vectors.v1.datagram_hex)))
    }
}

private extension Data {
    init?(hex: String) {
        guard hex.count.isMultiple(of: 2) else { return nil }
        var bytes = [UInt8]()
        bytes.reserveCapacity(hex.count / 2)

        var index = hex.startIndex
        while index < hex.endIndex {
            let next = hex.index(index, offsetBy: 2)
            guard let byte = UInt8(hex[index..<next], radix: 16) else { return nil }
            bytes.append(byte)
            index = next
        }

        self.init(bytes)
    }
}
