import AVFoundation
import AudioToolbox
import CryptoKit
import Darwin
import Network
import SwiftUI

@MainActor
final class AudioStreamer: ObservableObject {
    @Published private(set) var isStreaming = false
    @Published private(set) var isStarting = false
    @Published private(set) var status = "Ready"
    @Published var errorMessage: String?

    private let engine = AVAudioEngine()
    private let processingQueue = DispatchQueue(label: "com.canopydigital.pocketmic.audio-processing")
    private let networkQueue = DispatchQueue(label: "com.canopydigital.pocketmic.network")
    private let processor = AudioPacketProcessor()
    private var connection: NWConnection?
    private var permissionRequestID: UUID?
    private var streamGeneration: UInt64 = 0

    func start(host: String, port portText: String, pairingKey: String) {
        guard !isStreaming, !isStarting else { return }
        guard let portValue = UInt16(portText), portValue > 0,
              let nwPort = NWEndpoint.Port(rawValue: portValue) else {
            errorMessage = "Enter a valid UDP port between 1 and 65535."
            return
        }
        let receiver = host.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !receiver.isEmpty, !pairingKey.isEmpty else {
            errorMessage = "Enter the receiver address and pairing key."
            return
        }

        isStarting = true
        status = "Waiting for microphone permission"
        let requestID = UUID()
        permissionRequestID = requestID
        AVAudioApplication.requestRecordPermission { [weak self] granted in
            Task { @MainActor in
                guard let self, self.permissionRequestID == requestID else { return }
                self.permissionRequestID = nil
                self.isStarting = false
                guard granted else {
                    self.status = "Ready"
                    self.errorMessage = "Allow microphone access in iOS Settings to stream audio."
                    return
                }
                self.beginCapture(host: receiver, port: nwPort, pairingKey: pairingKey)
            }
        }
    }

    func stop() {
        permissionRequestID = nil
        isStarting = false
        streamGeneration &+= 1

        engine.inputNode.removeTap(onBus: 0)
        engine.stop()
        let stoppedConnection = connection
        connection = nil

        // The tap only copies buffers and queues them. Drain those queued buffers before
        // clearing processor state so a late callback cannot race shutdown or a new stream.
        processingQueue.sync {
            processor.reset()
        }
        stoppedConnection?.cancel()
        try? AVAudioSession.sharedInstance().setActive(false, options: .notifyOthersOnDeactivation)

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
            guard let outputFormat = AVAudioFormat(
                commonFormat: .pcmFormatFloat32,
                sampleRate: 48_000,
                channels: 1,
                interleaved: false
            ), let audioConverter = AVAudioConverter(from: inputFormat, to: outputFormat) else {
                throw StreamError.audioSetup("Could not configure 48 kHz microphone conversion.")
            }

            streamGeneration &+= 1
            let generation = streamGeneration
            let key = SymmetricKey(data: Data(SHA256.hash(data: Data(pairingKey.utf8))))
            let sessionID = UInt64.random(in: 1...UInt64.max)
            processingQueue.sync {
                processor.configure(
                    converter: audioConverter,
                    key: key,
                    sessionID: sessionID,
                    generation: generation
                )
            }

            let udp = NWConnection(host: NWEndpoint.Host(host), port: port, using: .udp)
            connection = udp
            udp.stateUpdateHandler = { [weak self, weak udp] state in
                guard case .failed(let error) = state else { return }
                Task { @MainActor in
                    guard let self, let udp, self.connection === udp else { return }
                    self.errorMessage = "Receiver connection failed: \(error.localizedDescription)"
                    self.stop()
                }
            }
            udp.start(queue: networkQueue)

            let captureQueue = processingQueue
            let packetProcessor = processor
            input.installTap(onBus: 0, bufferSize: 2_048, format: inputFormat) { [weak self, weak udp] buffer, _ in
                guard let udp, let ownedBuffer = copyAudioBuffer(buffer) else { return }
                captureQueue.async { [weak self, weak udp] in
                    guard let udp else { return }
                    let exhaustedSequence = packetProcessor.process(
                        ownedBuffer,
                        generation: generation
                    ) { packet in
                        udp.send(content: packet, completion: .contentProcessed { _ in })
                    }
                    if exhaustedSequence {
                        Task { @MainActor [weak self, weak udp] in
                            guard let self, let udp, self.connection === udp else { return }
                            self.errorMessage = "This audio session reached the packet limit. Start a new session to keep encryption nonces unique."
                            self.stop()
                        }
                    }
                }
            }

            try engine.start()
            isStreaming = true
            status = "Streaming to \(host):\(port.rawValue)"
        } catch {
            stop()
            errorMessage = error.localizedDescription
        }
    }
}

/// Owns all conversion, framing, and sequence state on AudioStreamer.processingQueue.
private final class AudioPacketProcessor {
    private var converter: AVAudioConverter?
    private var streamKey: SymmetricKey?
    private var sessionID: UInt64 = 0
    private var sequence: UInt32 = 0
    private var generation: UInt64?
    private var pendingSamples: [Float] = []
    private var sequenceLimitReported = false

    func configure(converter: AVAudioConverter, key: SymmetricKey, sessionID: UInt64, generation: UInt64) {
        self.converter = converter
        streamKey = key
        self.sessionID = sessionID
        sequence = 0
        self.generation = generation
        pendingSamples.removeAll(keepingCapacity: true)
        sequenceLimitReported = false
    }

    func reset() {
        converter = nil
        streamKey = nil
        sessionID = 0
        sequence = 0
        generation = nil
        pendingSamples.removeAll(keepingCapacity: false)
        sequenceLimitReported = false
    }

    /// Returns true once when the final unique sequence value has been sent.
    func process(_ inputBuffer: AVAudioPCMBuffer, generation: UInt64, send: (Data) -> Void) -> Bool {
        guard self.generation == generation, !sequenceLimitReported,
              let converter, let streamKey else { return false }

        let ratio = 48_000 / inputBuffer.format.sampleRate
        let capacity = AVAudioFrameCount(Double(inputBuffer.frameLength) * ratio + 512)
        guard let outputFormat = AVAudioFormat(
            commonFormat: .pcmFormatFloat32,
            sampleRate: 48_000,
            channels: 1,
            interleaved: false
        ), let converted = AVAudioPCMBuffer(pcmFormat: outputFormat, frameCapacity: capacity) else { return false }

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
        guard result != .error, let samples = converted.floatChannelData?[0] else { return false }

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
            send(packet)
            guard sequence < UInt32.max else {
                sequenceLimitReported = true
                return true
            }
            sequence += 1
        }
        return false
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

private func copyAudioBuffer(_ source: AVAudioPCMBuffer) -> AVAudioPCMBuffer? {
    guard let copy = AVAudioPCMBuffer(pcmFormat: source.format, frameCapacity: source.frameLength) else { return nil }
    copy.frameLength = source.frameLength

    let sourceBuffers = UnsafeMutableAudioBufferListPointer(UnsafeMutablePointer(mutating: source.audioBufferList))
    let destinationBuffers = UnsafeMutableAudioBufferListPointer(copy.mutableAudioBufferList)
    guard sourceBuffers.count == destinationBuffers.count else { return nil }
    for index in sourceBuffers.indices {
        let byteCount = Int(sourceBuffers[index].mDataByteSize)
        guard byteCount <= Int(destinationBuffers[index].mDataByteSize),
              let sourceData = sourceBuffers[index].mData,
              let destinationData = destinationBuffers[index].mData else { return nil }
        memcpy(destinationData, sourceData, byteCount)
        destinationBuffers[index].mDataByteSize = sourceBuffers[index].mDataByteSize
    }
    return copy
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
