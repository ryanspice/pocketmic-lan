import XCTest
@testable import PocketMicMac

final class PocketMicSequenceOrderTests: XCTestCase {
    func testAcceptsNextSequenceAndForwardGap() {
        XCTAssertTrue(PocketMicSequenceOrder.isForward(21, after: 20))
        XCTAssertTrue(PocketMicSequenceOrder.isForward(24, after: 20))
    }

    func testAcceptsSequenceRollover() {
        XCTAssertTrue(PocketMicSequenceOrder.isForward(0, after: UInt32.max))
        XCTAssertTrue(PocketMicSequenceOrder.isForward(1, after: UInt32.max))
    }

    func testRejectsDuplicateAndOlderPackets() {
        XCTAssertFalse(PocketMicSequenceOrder.isForward(20, after: 20))
        XCTAssertFalse(PocketMicSequenceOrder.isForward(19, after: 20))
        XCTAssertFalse(PocketMicSequenceOrder.isForward(UInt32.max, after: 0))
    }

    func testRejectsHalfRangeOrLargerForwardJump() {
        XCTAssertFalse(PocketMicSequenceOrder.isForward(0x8000_0001, after: 0))
    }
}
