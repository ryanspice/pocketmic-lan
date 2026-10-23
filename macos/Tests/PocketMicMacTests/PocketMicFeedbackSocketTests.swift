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
        let parameters = NWParameters.udp
        parameters.requiredLocalEndpoint = .hostPort(host: .ipv4(.loopback), port: .any)
        let listener = try NWListener(using: parameters)
        var accepted = [NWConnection]()
        defer {
            listener.cancel()
            queue.sync { accepted.forEach { $0.cancel() } }
        }
        let listening = expectation(description: "Loopback test receiver ready")
        listener.stateUpdateHandler = { state in
            if case .ready = state { listening.fulfill() }
            if case .failed(let error) = state { XCTFail("Test listener: \(error)") }
        }
        let received = expectation(description: "Feedback received before and after rapid restart")
        received.expectedFulfillmentCount = 2
        listener.newConnectionHandler = { connection in
            accepted.append(connection)
            connection.start(queue: queue)
            func receive() {
                connection.receiveMessage { data, _, _, error in
                    if let data {
                        XCTAssertEqual(data, Data([0x50, 0x4d, 0x43, 0x54, 0x4c]))
                        if case .hostPort(let sourceHost, let sourcePort) = connection.endpoint {
                            XCTAssertEqual(sourceHost, .ipv4(address))
                            XCTAssertEqual(sourcePort, listener.port)
                        } else { XCTFail("Expected numeric source endpoint") }
                        received.fulfill()
                    }
                    if error == nil { receive() }
                }
            }
            receive()
        }
        listener.start(queue: queue)
        wait(for: [listening], timeout: 5)
        let port = try XCTUnwrap(listener.port)
        guard port.rawValue > 1, PocketMicFeedbackRoute.validAudioPort(port.rawValue - 1) else {
            throw XCTSkip("Ephemeral port is reserved for the driver bridge")
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
