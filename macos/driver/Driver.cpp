#include <aspl/Driver.hpp>

#include <CoreAudio/AudioServerPlugIn.h>

#include <array>
#include <atomic>
#include <cerrno>
#include <cstdint>
#include <cstring>
#include <thread>

#include <arpa/inet.h>
#include <poll.h>
#include <sys/socket.h>
#include <unistd.h>

namespace {

constexpr UInt32 SampleRate = 48'000;
constexpr UInt32 ChannelCount = 1;
constexpr UInt16 BridgePort = 49'501;
constexpr std::size_t RingCapacity = 32'768;
static_assert(std::atomic<std::size_t>::is_always_lock_free,
    "CoreAudio callback requires lock-free size_t atomics");

// One network producer and one CoreAudio reader. All storage is preallocated;
// the realtime callback never allocates, locks, or touches the socket.
class PcmRing {
public:
    void Write(const SInt16* source, std::size_t count) noexcept {
        auto head = head_.load(std::memory_order_relaxed);
        const auto tail = tail_.load(std::memory_order_acquire);
        const auto available = RingCapacity - (head - tail);
        if (count > available) {
            dropped_.fetch_add(count, std::memory_order_relaxed);
            return;
        }
        for (std::size_t i = 0; i < count; ++i) {
            samples_[head % RingCapacity] = source[i];
            ++head;
        }
        head_.store(head, std::memory_order_release);
    }

    bool Read(SInt16& sample) noexcept {
        const auto tail = tail_.load(std::memory_order_relaxed);
        if (tail == head_.load(std::memory_order_acquire)) return false;
        sample = samples_[tail % RingCapacity];
        tail_.store(tail + 1, std::memory_order_release);
        return true;
    }

    void Clear() noexcept {
        tail_.store(head_.load(std::memory_order_acquire), std::memory_order_release);
    }

private:
    std::array<SInt16, RingCapacity> samples_{};
    alignas(64) std::atomic<std::size_t> head_{0};
    alignas(64) std::atomic<std::size_t> tail_{0};
    std::atomic<std::uint64_t> dropped_{0};
};

class PocketMicHandler final : public aspl::ControlRequestHandler, public aspl::IORequestHandler {
public:
    OSStatus OnStartIO() override {
        if (running_.exchange(true)) return kAudioHardwareNoError;
        ring_.Clear();
        socket_ = ::socket(AF_INET, SOCK_DGRAM, 0);
        if (socket_ < 0) {
            running_.store(false);
            return kAudioHardwareUnspecifiedError;
        }

        sockaddr_in address{};
        address.sin_len = sizeof(address);
        address.sin_family = AF_INET;
        address.sin_port = htons(BridgePort);
        address.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
        if (::bind(socket_, reinterpret_cast<sockaddr*>(&address), sizeof(address)) != 0) {
            ::close(socket_);
            socket_ = -1;
            running_.store(false);
            return kAudioHardwareUnspecifiedError;
        }
        worker_ = std::thread([this] { ReceiveLoop(); });
        return kAudioHardwareNoError;
    }

    void OnStopIO() override {
        if (!running_.exchange(false)) return;
        if (socket_ >= 0) {
            ::shutdown(socket_, SHUT_RDWR);
        }
        if (worker_.joinable()) worker_.join();
        if (socket_ >= 0) {
            ::close(socket_);
            socket_ = -1;
        }
        ring_.Clear();
    }

    void OnReadClientInput(const std::shared_ptr<aspl::Client>&,
        const std::shared_ptr<aspl::Stream>&,
        Float64,
        Float64,
        void* bytes,
        UInt32 bytesCount) override {
        auto* samples = static_cast<SInt16*>(bytes);
        const auto count = bytesCount / sizeof(SInt16);
        for (UInt32 i = 0; i < count; ++i) {
            if (!ring_.Read(samples[i])) samples[i] = 0;
        }
        const auto trailing = bytesCount % sizeof(SInt16);
        if (trailing != 0) std::memset(reinterpret_cast<std::uint8_t*>(bytes) + count * sizeof(SInt16), 0, trailing);
    }

private:
    void ReceiveLoop() noexcept {
        std::array<SInt16, 480> frame{};
        std::array<std::uint8_t, 2'048> datagram{};
        pollfd descriptor{socket_, POLLIN, 0};
        while (running_.load(std::memory_order_acquire)) {
            const auto result = ::poll(&descriptor, 1, 100);
            if (result <= 0) continue;
            const auto count = ::recv(socket_, datagram.data(), datagram.size(), 0);
            if (count == static_cast<ssize_t>(sizeof(frame))) {
                std::memcpy(frame.data(), datagram.data(), sizeof(frame));
                ring_.Write(frame.data(), frame.size());
            }
        }
    }

    PcmRing ring_;
    std::atomic<bool> running_{false};
    int socket_ = -1;
    std::thread worker_;
};

std::shared_ptr<aspl::Driver> CreateDriver() {
    auto context = std::make_shared<aspl::Context>();
    aspl::DeviceParameters parameters;
    parameters.Name = "PocketMic Virtual Mic";
    parameters.Manufacturer = "Canopy Digital";
    parameters.DeviceUID = "com.canopydigital.pocketmic.virtual-mic.device";
    parameters.ModelUID = "com.canopydigital.pocketmic.virtual-mic";
    parameters.SampleRate = SampleRate;
    parameters.ChannelCount = ChannelCount;
    parameters.Latency = 480;

    auto device = std::make_shared<aspl::Device>(context, parameters);
    aspl::StreamParameters streamParameters;
    streamParameters.Direction = aspl::Direction::Input;
    streamParameters.Format = {
        .mSampleRate = static_cast<Float64>(SampleRate),
        .mFormatID = kAudioFormatLinearPCM,
        .mFormatFlags = kAudioFormatFlagIsSignedInteger | kAudioFormatFlagsNativeEndian | kAudioFormatFlagIsPacked,
        .mBytesPerPacket = sizeof(SInt16),
        .mFramesPerPacket = 1,
        .mBytesPerFrame = sizeof(SInt16),
        .mChannelsPerFrame = ChannelCount,
        .mBitsPerChannel = 16,
    };
    device->AddStreamAsync(streamParameters);

    auto handler = std::make_shared<PocketMicHandler>();
    device->SetControlHandler(handler);
    device->SetIOHandler(handler);

    auto plugin = std::make_shared<aspl::Plugin>(context);
    plugin->AddDevice(device);
    return std::make_shared<aspl::Driver>(context, plugin);
}

} // namespace

extern "C" void* PocketMicVirtualMicEntryPoint(CFAllocatorRef, CFUUIDRef typeUUID) {
    if (!CFEqual(typeUUID, kAudioServerPlugInTypeUUID)) return nullptr;
    static auto driver = CreateDriver();
    return driver->GetReference();
}
