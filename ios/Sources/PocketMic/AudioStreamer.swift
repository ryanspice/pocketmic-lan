import AVFoundation
import CryptoKit
import Network
import SwiftUI

@MainActor
final class AudioStreamer: ObservableObject {
    @Published private(set) var isStreaming = false
    @Published private(set) var status = "Ready"
    @Published var errorMessage: String?

    private let engine = AVAudioEngine()
    private let processingQueue = DispatchQueue(label: "com.ryanspice.pocketmic.audio")
    private var connection: NWConnection?
    private var converter: AVAudioConverter?
    private var pendingSamples: [Float] = []
    private var streamKey: SymmetricKey?
    private var sessionID: UInt64 = 0
    private var sequence: UInt32 = 0

    func start(host: String, port portText: String, pairingKey: String) {
        guard let portValue = UInt16(portText), portValue > 0,
              let nwPort = NWEndpoint.Port(rawValue: portValue) else {
            errorMessage = "Enter a valid UDP port between 1 and 65535."
            return
        }
        guard !host.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty,
              !pairingKey.isEmpty else {
            errorMessage = "Enter the receiver address and pairing key."
            return
        }

        AVAudioApplication.requestRecordPermission { [weak self] granted in
            Task { @MainActor in
                guard let self else { return }
                guard granted else {
                    self.errorMessage = "Allow microphone access in iOS Settings to stream audio."
                    return
                }
                self.beginCapture(host: host, port: nwPort, pairingKey: pairingKey)
            }
        }
    }

    func stop() {
        engine.inputNode.removeTap(onBus: 0)
        engine.stop()
        connection?.cancel()
        connection = nil
        converter = nil
        streamKey = nil
        pendingSamples.removeAll(keepingCapacity: false)
        isStreaming = false
        status = "Ready"
    }

    private func beginCapture(host: String, port: NWEndpoint.Port, pairingKey: String) {
        do {
            let session = AVAudioSession.sharedInstance()
            try session.setCategory(.playAndRecord, mode: .measurement, options: [.defaultToSpeaker])
            try session.setPreferredSampleRate(48_000)
            try session.setActive(true)

            let input = engine.inputNode
            let inputFormat = input.outputFormat(forBus: 0)
            let outputFormat = AVAudioFormat(commonFormat: .pcmFormatFloat32, sampleRate: 48_000, channels: 1, interleaved: false)!
            guard let audioConverter = AVAudioConverter(from: inputFormat, to: outputFormat) else {
                throw StreamError.audioSetup("Could not configure 48 kHz microphone conversion.")
            }
            converter = audioConverter
            streamKey = SymmetricKey(data: Data(SHA256.hash(data: Data(pairingKey.utf8))))
            sessionID = UInt64.random(in: 1...UInt64.max)
            sequence = 0
            pendingSamples.removeAll(keepingCapacity: true)

            let udp = NWConnection(host: NWEndpoint.Host(host), port: port, using: .udp)
            connection = udp
            udp.stateUpdateHandler = { [weak self] state in
                guard case .failed(let error) = state else { return }
                Task { @MainActor in
                    self?.errorMessage = "Receiver connection failed: \(error.localizedDescription)"
                    self?.stop()
                }
            }
            udp.start(queue: processingQueue)

            input.installTap(onBus: 0, bufferSize: 2_048, format: inputFormat) { [weak self] buffer, _ in
                self?.convertAndSend(buffer, outputFormat: outputFormat)
            }
            try engine.start()
            isStreaming = true
            status = "Streaming to \(host):\(port.rawValue)"
        } catch {
            stop()
            errorMessage = error.localizedDescription
        }
    }

    private func convertAndSend(_ inputBuffer: AVAudioPCMBuffer, outputFormat: AVAudioFormat) {
        guard let converter, let streamKey else { return }
        let ratio = outputFormat.sampleRate / inputBuffer.format.sampleRate
        let capacity = AVAudioFrameCount(Double(inputBuffer.frameLength) * ratio + 512)
        guard let converted = AVAudioPCMBuffer(pcmFormat: outputFormat, frameCapacity: capacity) else { return }
        var suppliedInput = false
        var conversionError: NSError?
        let result = converter.convert(to: converted, error: &conversionError) { _, inputStatus in
            if suppliedInput {
                inputStatus.pointee = .noDataNow
                return nil
            }
            suppliedInput = true
            inputStatus.pointee = .haveData
            return inputBuffer
        }
        guard result != .error, let samples = converted.floatChannelData?[0] else { return }

        pendingSamples.append(contentsOf: UnsafeBufferPointer(start: samples, count: Int(converted.frameLength)))
        while pendingSamples.count >= 480 {
            var pcm = Data(count: 960)
            pcm.withUnsafeMutableBytes { raw in
                guard let bytes = raw.bindMemory(to: UInt8.self).baseAddress else { return }
                for index in 0..<480 {
                    let scaled = Int16(max(-1, min(1, pendingSamples[index])) * Float(Int16.max))
                    let value = UInt16(bitPattern: scaled)
                    bytes[index * 2] = UInt8(value & 0xff)
                    bytes[index * 2 + 1] = UInt8(value >> 8)
                }
            }
            pendingSamples.removeFirst(480)
            guard let packet = Self.makePacket(pcm: pcm, key: streamKey, sessionID: sessionID, sequence: sequence) else { continue }
            sequence &+= 1
            connection?.send(content: packet, completion: .contentProcessed { _ in })
        }
    }

    private static func makePacket(pcm: Data, key: SymmetricKey, sessionID: UInt64, sequence: UInt32) -> Data? {
        var header = Data([0x50, 0x4d, 0x49, 0x43, 1, 1])
        header.appendUInt16BE(24)
        header.appendUInt64BE(sessionID)
        header.appendUInt32BE(sequence)
        header.appendUInt32BE(48_000)

        var nonceData = Data()
        nonceData.appendUInt64BE(sessionID)
        nonceData.appendUInt32BE(sequence)
        guard let nonce = try? AES.GCM.Nonce(data: nonceData),
              let sealed = try? AES.GCM.seal(pcm, using: key, nonce: nonce, authenticating: header) else { return nil }
        var packet = header
        packet.append(sealed.ciphertext)
        packet.append(sealed.tag)
        return packet
    }
}

private enum StreamError: LocalizedError {
    case audioSetup(String)

    var errorDescription: String? {
        if case .audioSetup(let message) = self { return message }
        return nil
    }
}

private extension Data {
    mutating func appendUInt16BE(_ value: UInt16) {
        append(UInt8(value >> 8))
        append(UInt8(value & 0xff))
    }

    mutating func appendUInt32BE(_ value: UInt32) {
        append(UInt8((value >> 24) & 0xff))
        append(UInt8((value >> 16) & 0xff))
        append(UInt8((value >> 8) & 0xff))
        append(UInt8(value & 0xff))
    }

    mutating func appendUInt64BE(_ value: UInt64) {
        appendUInt32BE(UInt32(value >> 32))
        appendUInt32BE(UInt32(value & 0xffff_ffff))
    }
}
