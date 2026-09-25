import CryptoKit
import Foundation
import XCTest
@testable import PocketMic

final class PocketMicPacketFixtureTests: XCTestCase {
    private struct Vectors: Decodable {
        struct V1: Decodable {
            let datagram_hex: String
            let pcm_payload_hex: String
        }

        let key_hex: String
        let v1: V1
    }

    private func loadVectors() throws -> Vectors {
        let url = try XCTUnwrap(Bundle(for: Self.self).url(forResource: "vectors", withExtension: "json"))
        return try JSONDecoder().decode(Vectors.self, from: Data(contentsOf: url))
    }

    func testDecryptsSharedProtocolV1Fixture() throws {
        let vectors = try loadVectors()
        let key = SymmetricKey(data: try XCTUnwrap(Data(hex: vectors.key_hex)))
        let datagram = try XCTUnwrap(Data(hex: vectors.v1.datagram_hex))

        let frame = try XCTUnwrap(PocketMicReceiver.decryptPCMv1(datagram, using: key))

        XCTAssertEqual(frame.sessionID, 0x0102030405060708)
        XCTAssertEqual(frame.sequence, 0)
        XCTAssertEqual(frame.pcm, try XCTUnwrap(Data(hex: vectors.v1.pcm_payload_hex)))
    }

    func testRejectsTamperedCiphertextInSharedFixture() throws {
        let vectors = try loadVectors()
        let key = SymmetricKey(data: try XCTUnwrap(Data(hex: vectors.key_hex)))
        var datagram = try XCTUnwrap(Data(hex: vectors.v1.datagram_hex))
        datagram[30] ^= 1

        XCTAssertNil(PocketMicReceiver.decryptPCMv1(datagram, using: key))
    }

    func testRejectsSharedFixtureWithWrongKey() throws {
        let vectors = try loadVectors()
        let wrongKey = SymmetricKey(data: Data(repeating: 0xff, count: 32))
        let datagram = try XCTUnwrap(Data(hex: vectors.v1.datagram_hex))

        XCTAssertNil(PocketMicReceiver.decryptPCMv1(datagram, using: wrongKey))
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
