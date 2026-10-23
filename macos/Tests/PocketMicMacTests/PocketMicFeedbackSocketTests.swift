import Darwin
import Foundation
import Network
import XCTest
@testable import PocketMic

/// Hosted-Mac transport checks; these do not exercise the virtual microphone or audible audio.
final class PocketMicFeedbackSocketTests: XCTestCase {
    func testConcreteBindCompanionPortsAndRapidFeedbackRestart() throws {
        let address = try nonLoopbackIPv4()
        let queue = DispatchQueue(label: "pocketmic.tests.feedback")
        // Model Android's unconnected DatagramSocket. Two Network.framework UDP peers in
        // one process can collide even with distinct IPs; the production peer is another
        // device. Keep the real feedback NWConnection and inspect its actual datagrams.
        let descriptor = socket(AF_INET, SOCK_DGRAM, 0)
        guard descriptor >= 0 else { throw socketError() }
        defer { close(descriptor) }
        var loopback = sockaddr_in()
        loopback.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        loopback.sin_family = sa_family_t(AF_INET)
        loopback.sin_addr = in_addr(s_addr: inet_addr("127.0.0.1"))
        let bound = withUnsafePointer(to: &loopback) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                Darwin.bind(descriptor, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }
        guard bound == 0 else { throw socketError() }
        var length = socklen_t(MemoryLayout<sockaddr_in>.size)
        let named = withUnsafeMutablePointer(to: &loopback) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                getsockname(descriptor, $0, &length)
            }
        }
        guard named == 0 else { throw socketError() }
        let portNumber = UInt16(bigEndian: loopback.sin_port)
        guard portNumber > 1, PocketMicFeedbackRoute.validAudioPort(portNumber - 1) else {
            throw XCTSkip("Ephemeral port is reserved for the driver bridge")
        }
        let port = try XCTUnwrap(NWEndpoint.Port(rawValue: portNumber))
        var timeout = timeval(tv_sec: 5, tv_usec: 0)
        guard setsockopt(descriptor, SOL_SOCKET, SO_RCVTIMEO, &timeout,
                         socklen_t(MemoryLayout<timeval>.size)) == 0 else { throw socketError() }

        let received = expectation(description: "Feedback received before and after rapid restart")
        received.expectedFulfillmentCount = 2
        let readerQueue = DispatchQueue(label: "pocketmic.tests.phone-socket")
        // Finish the bounded blocking reads before closing their descriptor.
        defer { readerQueue.sync {} }
        readerQueue.async {
            for _ in 0..<2 {
                var buffer = [UInt8](repeating: 0, count: 64)
                var source = sockaddr_in()
                var sourceLength = socklen_t(MemoryLayout<sockaddr_in>.size)
                let count = withUnsafeMutablePointer(to: &source) { pointer in
                    pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) { sourceAddress in
                        buffer.withUnsafeMutableBytes { bytes in
                            recvfrom(descriptor, bytes.baseAddress, bytes.count, 0, sourceAddress, &sourceLength)
                        }
                    }
                }
                if count >= 0 {
                    XCTAssertEqual(Array(buffer.prefix(count)), [0x50, 0x4d, 0x43, 0x54, 0x4c])
                    var receivedAddress = source.sin_addr
                    XCTAssertEqual(withUnsafeBytes(of: &receivedAddress) { Data($0) }, address.rawValue)
                    XCTAssertEqual(UInt16(bigEndian: source.sin_port), portNumber)
                } else { XCTFail("Feedback receive failed: \(String(cString: strerror(errno)))") }
                received.fulfill()
            }
        }
        let route = try XCTUnwrap(PocketMicFeedbackRoute(
            localEndpoint: .hostPort(host: .ipv4(address), port: .any),
            remoteEndpoint: .hostPort(host: .ipv4(.loopback), port: 43_210),
            audioPort: port.rawValue - 1))
        for _ in 0..<2 {
            let feedback = route.makeConnection()
            let ready = expectation(description: "Concrete source port available")
            feedback.stateUpdateHandler = { state in
                if case .ready = state { ready.fulfill() }
                if case .failed(let error) = state { XCTFail("Feedback bind: \(error)") }
            }
            feedback.start(queue: queue)
            wait(for: [ready], timeout: 5)
            let sent = expectation(description: "Feedback send processed")
            feedback.send(content: Data([0x50, 0x4d, 0x43, 0x54, 0x4c]), completion: .contentProcessed { error in
                XCTAssertNil(error)
                sent.fulfill()
            })
            wait(for: [sent], timeout: 5)
            feedback.cancel()
        }
        wait(for: [received], timeout: 5)
    }

    private func socketError() -> NSError {
        NSError(domain: NSPOSIXErrorDomain, code: Int(errno))
    }

    func testDefaultLANControlBindCoexistsWithDriverLoopbackPort() throws {
        let address = try nonLoopbackIPv4()
        // A real HAL driver may already own this address on a developer's Mac; skip that
        // environment rather than competing with it. Hosted tests normally have no driver.
        let descriptor = socket(AF_INET, SOCK_DGRAM, 0)
        guard descriptor >= 0 else { throw XCTSkip("Cannot open dummy loopback bridge") }
        defer { close(descriptor) }
        var loopback = sockaddr_in()
        loopback.sin_len = UInt8(MemoryLayout<sockaddr_in>.size)
        loopback.sin_family = sa_family_t(AF_INET)
        loopback.sin_port = UInt16(49_501).bigEndian
        loopback.sin_addr = in_addr(s_addr: inet_addr("127.0.0.1"))
        let result = withUnsafePointer(to: &loopback) { pointer in
            pointer.withMemoryRebound(to: sockaddr.self, capacity: 1) {
                Darwin.bind(descriptor, $0, socklen_t(MemoryLayout<sockaddr_in>.size))
            }
        }
        guard result == 0 else { throw XCTSkip("Loopback bridge port already occupied") }
        let route = try XCTUnwrap(PocketMicFeedbackRoute(
            localEndpoint: .hostPort(host: .ipv4(address), port: 49_500),
            remoteEndpoint: .hostPort(host: .ipv4(.loopback), port: 43_210), audioPort: 49_500))
        let feedback = route.makeConnection()
        defer { feedback.cancel() }
        let ready = expectation(description: "Default LAN feedback port coexists with loopback bridge")
        feedback.stateUpdateHandler = { state in
            if case .ready = state { ready.fulfill() }
            if case .failed(let error) = state { XCTFail("LAN/loopback collision: \(error)") }
        }
        feedback.start(queue: DispatchQueue(label: "pocketmic.tests.default-feedback"))
        wait(for: [ready], timeout: 5)
    }

    private func nonLoopbackIPv4() throws -> IPv4Address {
        var interfaces: UnsafeMutablePointer<ifaddrs>?
        guard getifaddrs(&interfaces) == 0 else { throw XCTSkip("No interface inventory") }
        defer { freeifaddrs(interfaces) }
        var cursor = interfaces
        while let interface = cursor {
            defer { cursor = interface.pointee.ifa_next }
            guard let address = interface.pointee.ifa_addr,
                  Int32(address.pointee.sa_family) == AF_INET,
                  interface.pointee.ifa_flags & UInt32(IFF_UP) != 0,
                  interface.pointee.ifa_flags & UInt32(IFF_LOOPBACK) == 0 else { continue }
            let ipv4 = UnsafeRawPointer(address).assumingMemoryBound(to: sockaddr_in.self)
            var raw = ipv4.pointee.sin_addr
            let data = withUnsafeBytes(of: &raw) { Data($0) }
            if let result = IPv4Address(data) { return result }
        }
        throw XCTSkip("Socket feedback test requires a configured non-loopback IPv4 address")
    }
}
