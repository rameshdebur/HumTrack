#include <windows.h>
#include <mfapi.h>
#include <mferror.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <shlwapi.h>
#include <wrl/client.h>

#include <algorithm>
#include <cctype>
#include <cstdint>
#include <cwctype>
#include <filesystem>
#include <fstream>
#include <iomanip>
#include <iostream>
#include <limits>
#include <optional>
#include <sstream>
#include <stdexcept>
#include <string>
#include <vector>

using Microsoft::WRL::ComPtr;

namespace
{
constexpr LONGLONG TicksPerSecond = 10'000'000;
constexpr DWORD VideoStream = static_cast<DWORD>(MF_SOURCE_READER_FIRST_VIDEO_STREAM);

struct Options
{
    std::wstring deviceId;
    UINT32 width{};
    UINT32 height{};
    UINT32 fpsNumerator{};
    UINT32 fpsDenominator{1};
    std::wstring subtype;
    UINT32 durationSeconds{};
    std::filesystem::path outputDirectory;
    std::optional<DWORD> nativeTypeIndex;
    bool listProfiles{};
    bool finalizeOnReadError{};
    bool selfTest{};
};

struct Device
{
    ComPtr<IMFActivate> activation;
    std::wstring name;
    std::wstring symbolicLink;
    std::wstring normalizedId;
};

struct Profile
{
    DWORD index{};
    UINT32 width{};
    UINT32 height{};
    UINT32 fpsNumerator{};
    UINT32 fpsDenominator{};
    GUID subtype{};
    UINT32 averageBitrate{};
    ComPtr<IMFMediaType> mediaType;
};

class ProbeError final : public std::runtime_error
{
public:
    ProbeError(std::string code, std::string message, int exitCode = 1)
        : std::runtime_error(std::move(message)), code_(std::move(code)), exitCode_(exitCode)
    {
    }

    [[nodiscard]] const std::string& Code() const noexcept { return code_; }
    [[nodiscard]] int ExitCode() const noexcept { return exitCode_; }

private:
    std::string code_;
    int exitCode_;
};

std::string Utf8(const std::wstring& value)
{
    if (value.empty()) return {};
    const int size = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, value.data(),
        static_cast<int>(value.size()), nullptr, 0, nullptr, nullptr);
    if (size <= 0) throw ProbeError("UTF8_CONVERSION_FAILED", "Could not encode a Windows string as UTF-8.");
    std::string result(static_cast<size_t>(size), '\0');
    if (WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, value.data(),
        static_cast<int>(value.size()), result.data(), size, nullptr, nullptr) <= 0)
    {
        throw ProbeError("UTF8_CONVERSION_FAILED", "Could not encode a Windows string as UTF-8.");
    }
    return result;
}

std::wstring Wide(const std::string& value)
{
    if (value.empty()) return {};
    const int size = MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, value.data(),
        static_cast<int>(value.size()), nullptr, 0);
    if (size <= 0) throw ProbeError("UTF8_CONVERSION_FAILED", "Could not decode a UTF-8 argument.");
    std::wstring result(static_cast<size_t>(size), L'\0');
    if (MultiByteToWideChar(CP_UTF8, MB_ERR_INVALID_CHARS, value.data(),
        static_cast<int>(value.size()), result.data(), size) <= 0)
    {
        throw ProbeError("UTF8_CONVERSION_FAILED", "Could not decode a UTF-8 argument.");
    }
    return result;
}

std::string EscapeJson(const std::string& value)
{
    std::ostringstream output;
    for (const unsigned char character : value)
    {
        switch (character)
        {
        case '"': output << "\\\""; break;
        case '\\': output << "\\\\"; break;
        case '\b': output << "\\b"; break;
        case '\f': output << "\\f"; break;
        case '\n': output << "\\n"; break;
        case '\r': output << "\\r"; break;
        case '\t': output << "\\t"; break;
        default:
            if (character < 0x20)
            {
                output << "\\u" << std::hex << std::setw(4) << std::setfill('0')
                    << static_cast<unsigned int>(character) << std::dec;
            }
            else output << character;
        }
    }
    return output.str();
}

std::wstring Upper(std::wstring value)
{
    std::transform(value.begin(), value.end(), value.begin(), [](const wchar_t character)
    {
        return static_cast<wchar_t>(std::towupper(character));
    });
    return value;
}

std::wstring NormalizeDeviceId(const std::wstring& value)
{
    const std::wstring upper = Upper(value);
    const size_t usb = upper.find(L"USB#VID_");
    if (usb == std::wstring::npos)
    {
        return upper;
    }
    const size_t hardwareEnd = upper.find(L'#', usb + 4);
    if (hardwareEnd == std::wstring::npos) return upper;
    const size_t instanceEnd = upper.find(L'#', hardwareEnd + 1);
    if (instanceEnd == std::wstring::npos) return upper;
    std::wstring normalized = upper.substr(usb, hardwareEnd - usb);
    std::replace(normalized.begin(), normalized.end(), L'#', L'\\');
    normalized += L'\\';
    normalized += upper.substr(hardwareEnd + 1, instanceEnd - hardwareEnd - 1);
    return normalized;
}

UINT32 ParseUInt(const std::wstring& text, const std::string& name)
{
    if (text.empty()) throw ProbeError("INVALID_ARGUMENT", name + " requires an unsigned integer.", 2);
    size_t used = 0;
    unsigned long value = 0;
    try { value = std::stoul(text, &used, 10); }
    catch (const std::exception&) { throw ProbeError("INVALID_ARGUMENT", name + " requires an unsigned integer.", 2); }
    if (used != text.size() || value > std::numeric_limits<UINT32>::max())
        throw ProbeError("INVALID_ARGUMENT", name + " is outside the supported unsigned range.", 2);
    return static_cast<UINT32>(value);
}

std::wstring RequiredValue(int& index, const int argc, wchar_t* argv[], const std::string& name)
{
    if (index + 1 >= argc) throw ProbeError("MISSING_OPTION_VALUE", name + " requires a value.", 2);
    return argv[++index];
}

Options ParseOptions(const int argc, wchar_t* argv[])
{
    Options options{};
    for (int index = 1; index < argc; ++index)
    {
        const std::wstring argument = argv[index];
        if (argument == L"--self-test") options.selfTest = true;
        else if (argument == L"--list-profiles") options.listProfiles = true;
        else if (argument == L"--finalize-on-read-error") options.finalizeOnReadError = true;
        else if (argument == L"--device-id") options.deviceId = RequiredValue(index, argc, argv, "--device-id");
        else if (argument == L"--width") options.width = ParseUInt(RequiredValue(index, argc, argv, "--width"), "--width");
        else if (argument == L"--height") options.height = ParseUInt(RequiredValue(index, argc, argv, "--height"), "--height");
        else if (argument == L"--fps-numerator") options.fpsNumerator = ParseUInt(RequiredValue(index, argc, argv, "--fps-numerator"), "--fps-numerator");
        else if (argument == L"--fps-denominator") options.fpsDenominator = ParseUInt(RequiredValue(index, argc, argv, "--fps-denominator"), "--fps-denominator");
        else if (argument == L"--subtype") options.subtype = Upper(RequiredValue(index, argc, argv, "--subtype"));
        else if (argument == L"--duration-seconds") options.durationSeconds = ParseUInt(RequiredValue(index, argc, argv, "--duration-seconds"), "--duration-seconds");
        else if (argument == L"--output-dir") options.outputDirectory = RequiredValue(index, argc, argv, "--output-dir");
        else if (argument == L"--native-type-index") options.nativeTypeIndex = ParseUInt(RequiredValue(index, argc, argv, "--native-type-index"), "--native-type-index");
        else throw ProbeError("UNKNOWN_OPTION", "Unknown command-line option: " + Utf8(argument), 2);
    }
    if (options.selfTest) return options;
    if (options.deviceId.empty()) throw ProbeError("MISSING_REQUIRED_OPTION", "--device-id is required.", 2);
    if (options.listProfiles) return options;
    if (options.width == 0 || options.height == 0 || options.fpsNumerator == 0 || options.fpsDenominator == 0
        || options.subtype.empty() || options.durationSeconds == 0 || options.outputDirectory.empty())
    {
        throw ProbeError("MISSING_REQUIRED_OPTION", "Capture requires width, height, frame-rate numerator/denominator, subtype, duration, and output directory.", 2);
    }
    return options;
}

std::wstring AllocatedString(IMFAttributes* attributes, const GUID& key)
{
    wchar_t* value = nullptr;
    UINT32 length = 0;
    if (FAILED(attributes->GetAllocatedString(key, &value, &length))) return {};
    std::wstring result(value, length);
    CoTaskMemFree(value);
    return result;
}

std::vector<Device> EnumerateDevices()
{
    ComPtr<IMFAttributes> attributes;
    HRESULT result = MFCreateAttributes(&attributes, 1);
    if (SUCCEEDED(result)) result = attributes->SetGUID(MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE,
        MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID);
    IMFActivate** activations = nullptr;
    UINT32 count = 0;
    if (SUCCEEDED(result)) result = MFEnumDeviceSources(attributes.Get(), &activations, &count);
    if (FAILED(result)) throw ProbeError("MF_ENUMERATION_FAILED", "Media Foundation camera enumeration failed.");

    std::vector<Device> devices;
    devices.reserve(count);
    for (UINT32 index = 0; index < count; ++index)
    {
        Device device{};
        device.activation.Attach(activations[index]);
        device.name = AllocatedString(device.activation.Get(), MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME);
        device.symbolicLink = AllocatedString(device.activation.Get(), MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_SYMBOLIC_LINK);
        device.normalizedId = NormalizeDeviceId(device.symbolicLink);
        devices.push_back(std::move(device));
    }
    CoTaskMemFree(activations);
    return devices;
}

Device& SelectDevice(std::vector<Device>& devices, const std::wstring& requested)
{
    const std::wstring normalized = NormalizeDeviceId(requested);
    std::vector<Device*> matches;
    for (Device& device : devices)
    {
        if (Upper(device.symbolicLink) == Upper(requested) || device.normalizedId == normalized)
            matches.push_back(&device);
    }
    if (matches.empty()) throw ProbeError("DEVICE_NOT_FOUND", "The exact requested device identity was not enumerated.", 3);
    if (matches.size() != 1) throw ProbeError("DEVICE_IDENTITY_AMBIGUOUS", "The requested identity matched multiple devices.", 3);
    return *matches.front();
}

std::vector<Profile> EnumerateProfiles(IMFSourceReader* reader)
{
    std::vector<Profile> profiles;
    for (DWORD index = 0;; ++index)
    {
        ComPtr<IMFMediaType> mediaType;
        HRESULT result = reader->GetNativeMediaType(VideoStream, index, &mediaType);
        if (result == MF_E_NO_MORE_TYPES) break;
        if (FAILED(result)) throw ProbeError("PROFILE_ENUMERATION_FAILED", "Native media-type enumeration failed.");
        GUID major{};
        if (FAILED(mediaType->GetGUID(MF_MT_MAJOR_TYPE, &major)) || major != MFMediaType_Video) continue;
        Profile profile{};
        profile.index = index;
        profile.mediaType = mediaType;
        if (FAILED(MFGetAttributeSize(mediaType.Get(), MF_MT_FRAME_SIZE, &profile.width, &profile.height))
            || FAILED(MFGetAttributeRatio(mediaType.Get(), MF_MT_FRAME_RATE, &profile.fpsNumerator, &profile.fpsDenominator))
            || FAILED(mediaType->GetGUID(MF_MT_SUBTYPE, &profile.subtype))) continue;
        mediaType->GetUINT32(MF_MT_AVG_BITRATE, &profile.averageBitrate);
        profiles.push_back(std::move(profile));
    }
    return profiles;
}

GUID SubtypeGuid(const std::wstring& subtype)
{
    if (subtype == L"H264") return MFVideoFormat_H264;
    if (subtype == L"MJPG") return MFVideoFormat_MJPG;
    if (subtype == L"NV12") return MFVideoFormat_NV12;
    if (subtype == L"YUY2") return MFVideoFormat_YUY2;
    throw ProbeError("UNSUPPORTED_SUBTYPE_ARGUMENT", "Supported subtype arguments are H264, MJPG, NV12, and YUY2.", 2);
}

std::vector<Profile*> MatchingProfiles(std::vector<Profile>& profiles, const Options& options)
{
    const GUID subtype = SubtypeGuid(options.subtype);
    std::vector<Profile*> matches;
    for (Profile& profile : profiles)
    {
        if (profile.width == options.width && profile.height == options.height
            && profile.fpsNumerator == options.fpsNumerator && profile.fpsDenominator == options.fpsDenominator
            && profile.subtype == subtype)
        {
            if (!options.nativeTypeIndex || profile.index == *options.nativeTypeIndex) matches.push_back(&profile);
        }
    }
    return matches;
}

std::string GuidText(const GUID& value)
{
    wchar_t buffer[40]{};
    StringFromGUID2(value, buffer, static_cast<int>(std::size(buffer)));
    return Utf8(buffer);
}

void WriteErrorJson(const ProbeError& error)
{
    std::cerr << "{\"error\":{\"code\":\"" << EscapeJson(error.Code())
              << "\",\"message\":\"" << EscapeJson(error.what()) << "\"}}\n";
}

void WriteProfilesJson(const Device& device, const std::vector<Profile>& profiles)
{
    std::cout << "{\"format_name\":\"humcapture-mf-native-profiles\","
              << "\"format_version\":\"0.1.0\",\"work_tags\":[\"P0.2B\",\"UVC\",\"CORE\",\"CAPTURE\",\"TIMING\",\"HARDWARE\"],"
              << "\"device_name\":\"" << EscapeJson(Utf8(device.name)) << "\","
              << "\"device_id\":\"" << EscapeJson(Utf8(device.normalizedId)) << "\",\"profiles\":[";
    for (size_t index = 0; index < profiles.size(); ++index)
    {
        const Profile& profile = profiles[index];
        std::cout << "{\"native_type_index\":" << profile.index << ",\"width_px\":" << profile.width
                  << ",\"height_px\":" << profile.height << ",\"fps_numerator\":" << profile.fpsNumerator
                  << ",\"fps_denominator\":" << profile.fpsDenominator << ",\"subtype_guid\":\""
                  << GuidText(profile.subtype) << "\",\"average_bitrate_bps\":" << profile.averageBitrate << "}"
                  << (index + 1 == profiles.size() ? "" : ",");
    }
    std::cout << "]}\n";
}

std::string NewRunId()
{
    GUID value{};
    if (FAILED(CoCreateGuid(&value))) throw ProbeError("RUN_ID_GENERATION_FAILED", "Could not generate a run identifier.");
    wchar_t buffer[40]{};
    StringFromGUID2(value, buffer, static_cast<int>(std::size(buffer)));
    std::wstring text(buffer);
    if (text.size() >= 2) text = text.substr(1, text.size() - 2);
    return Utf8(text);
}

std::uint64_t QpcNanoseconds()
{
    LARGE_INTEGER counter{};
    LARGE_INTEGER frequency{};
    QueryPerformanceCounter(&counter);
    QueryPerformanceFrequency(&frequency);
    const long double nanoseconds = static_cast<long double>(counter.QuadPart) * 1'000'000'000.0L
        / static_cast<long double>(frequency.QuadPart);
    return static_cast<std::uint64_t>(nanoseconds);
}

void Ensure(HRESULT result, const char* code, const char* message)
{
    if (FAILED(result))
    {
        std::ostringstream detail;
        detail << message << " HRESULT=0x" << std::uppercase << std::hex << static_cast<unsigned long>(result);
        throw ProbeError(code, detail.str());
    }
}

int RunCapture(Device& device, Profile& profile, const Options& options, IMFSourceReader* reader)
{
    Ensure(reader->SetCurrentMediaType(VideoStream, nullptr, profile.mediaType.Get()),
        "PROFILE_NEGOTIATION_FAILED", "Source Reader rejected the exact native media type.");
    ComPtr<IMFMediaType> negotiated;
    Ensure(reader->GetCurrentMediaType(VideoStream, &negotiated),
        "NEGOTIATED_PROFILE_UNAVAILABLE", "Could not read the negotiated media type.");
    UINT32 width = 0, height = 0, fpsNumerator = 0, fpsDenominator = 0;
    GUID subtype{};
    Ensure(MFGetAttributeSize(negotiated.Get(), MF_MT_FRAME_SIZE, &width, &height),
        "NEGOTIATED_PROFILE_INVALID", "Negotiated frame size is unavailable.");
    Ensure(MFGetAttributeRatio(negotiated.Get(), MF_MT_FRAME_RATE, &fpsNumerator, &fpsDenominator),
        "NEGOTIATED_PROFILE_INVALID", "Negotiated frame rate is unavailable.");
    Ensure(negotiated->GetGUID(MF_MT_SUBTYPE, &subtype),
        "NEGOTIATED_PROFILE_INVALID", "Negotiated subtype is unavailable.");
    if (width != profile.width || height != profile.height || fpsNumerator != profile.fpsNumerator
        || fpsDenominator != profile.fpsDenominator || subtype != profile.subtype)
    {
        throw ProbeError("SILENT_PROFILE_SUBSTITUTION", "Negotiated media type differs from the exact selected native media type.");
    }

    std::filesystem::create_directories(options.outputDirectory);
    const std::filesystem::path mediaPath = options.outputDirectory / L"capture.mp4";
    const std::filesystem::path framesPath = options.outputDirectory / L"frames.csv";
    const std::filesystem::path resultPath = options.outputDirectory / L"capture-result.json";

    ComPtr<IMFSinkWriter> writer;
    Ensure(MFCreateSinkWriterFromURL(mediaPath.c_str(), nullptr, nullptr, &writer),
        "SINK_CREATION_FAILED", "Could not create the MP4 sink writer.");
    DWORD writerStream = 0;
    Ensure(writer->AddStream(negotiated.Get(), &writerStream),
        "SINK_STREAM_CREATION_FAILED", "Could not add the exact H.264 output stream.");
    Ensure(writer->SetInputMediaType(writerStream, negotiated.Get(), nullptr),
        "SINK_INPUT_TYPE_FAILED", "Could not configure compressed H.264 pass-through input.");
    Ensure(writer->BeginWriting(), "SINK_BEGIN_FAILED", "Could not begin MP4 writing.");

    std::ofstream frames(framesPath, std::ios::binary | std::ios::trunc);
    if (!frames) throw ProbeError("FRAME_EVIDENCE_OPEN_FAILED", "Could not create frames.csv.");
    frames << "run_id,stream_id,sequence,source_timestamp,source_timestamp_domain,source_timestamp_unit,"
              "presentation_timestamp,presentation_timestamp_unit,host_arrival_timestamp,host_arrival_timestamp_domain,"
              "host_arrival_timestamp_unit,discontinuity,dropped_before,width_px,height_px,encoded_size_bytes,queue_depth,notes\r\n";

    const std::string runId = NewRunId();
    std::uint64_t sequence = 0;
    LONGLONG firstSourceTimestamp = 0;
    LONGLONG previousSourceTimestamp = 0;
    bool haveFirstTimestamp = false;
    bool finalized = false;
    std::uint64_t streamTicks = 0;
    std::uint64_t typeChanges = 0;
    std::uint64_t timestampRegressions = 0;
    std::uint64_t totalBytes = 0;
    std::string terminalReason = "duration_reached";
    std::optional<HRESULT> terminalHresult;
    const LONGLONG expectedDuration = static_cast<LONGLONG>(options.fpsDenominator) * TicksPerSecond
        / static_cast<LONGLONG>(options.fpsNumerator);

    while (true)
    {
        DWORD actualStream = 0;
        DWORD flags = 0;
        LONGLONG sourceTimestamp = 0;
        ComPtr<IMFSample> sample;
        const HRESULT readResult = reader->ReadSample(VideoStream, 0, &actualStream, &flags, &sourceTimestamp, &sample);
        if (FAILED(readResult))
        {
            if (!options.finalizeOnReadError || sequence == 0)
                Ensure(readResult, "SAMPLE_READ_FAILED", "Source Reader failed while reading a sample.");
            terminalReason = "read_error";
            terminalHresult = readResult;
            break;
        }
        const std::uint64_t hostArrivalNs = QpcNanoseconds();

        if ((flags & MF_SOURCE_READERF_ERROR) != 0)
        {
            if (!options.finalizeOnReadError || sequence == 0)
                throw ProbeError("SOURCE_READER_FLAGGED_ERROR", "Source Reader returned its error flag.");
            terminalReason = "source_reader_error_flag";
            break;
        }
        if ((flags & MF_SOURCE_READERF_STREAMTICK) != 0) ++streamTicks;
        if ((flags & (MF_SOURCE_READERF_NATIVEMEDIATYPECHANGED | MF_SOURCE_READERF_CURRENTMEDIATYPECHANGED)) != 0)
        {
            ++typeChanges;
            throw ProbeError("MEDIA_TYPE_CHANGED_DURING_CAPTURE", "The source media type changed during exact-profile capture.");
        }
        if ((flags & MF_SOURCE_READERF_ENDOFSTREAM) != 0)
        {
            terminalReason = "end_of_stream";
            break;
        }
        if (!sample) continue;

        if (!haveFirstTimestamp)
        {
            firstSourceTimestamp = sourceTimestamp;
            previousSourceTimestamp = sourceTimestamp;
            haveFirstTimestamp = true;
        }

        std::string discontinuity = "none";
        std::uint64_t droppedBefore = 0;
        if (sourceTimestamp < previousSourceTimestamp)
        {
            discontinuity = "regression";
            ++timestampRegressions;
        }
        else if (sequence > 0 && sourceTimestamp - previousSourceTimestamp > expectedDuration * 3 / 2)
        {
            discontinuity = "gap";
            const LONGLONG elapsedFrames = expectedDuration > 0 ? (sourceTimestamp - previousSourceTimestamp) / expectedDuration : 1;
            droppedBefore = elapsedFrames > 1 ? static_cast<std::uint64_t>(elapsedFrames - 1) : 0;
        }
        if ((flags & MF_SOURCE_READERF_STREAMTICK) != 0) discontinuity = "gap";
        if ((flags & (MF_SOURCE_READERF_NATIVEMEDIATYPECHANGED | MF_SOURCE_READERF_CURRENTMEDIATYPECHANGED)) != 0) discontinuity = "source_change";

        DWORD bytes = 0;
        Ensure(sample->GetTotalLength(&bytes), "SAMPLE_LENGTH_FAILED", "Could not read encoded sample length.");
        totalBytes += bytes;
        frames << runId << ",master," << sequence << ',' << sourceTimestamp
               << ",media_presentation,ticks_100ns," << sourceTimestamp << ",ticks_100ns,"
               << hostArrivalNs << ",host_qpc,ns," << discontinuity << ',' << droppedBefore << ','
               << width << ',' << height << ',' << bytes << ",0,flags_0x" << std::hex << flags << std::dec << "\r\n";

        const LONGLONG normalizedTimestamp = sourceTimestamp - firstSourceTimestamp;
        Ensure(sample->SetSampleTime(normalizedTimestamp), "SAMPLE_TIME_SET_FAILED", "Could not normalize sink sample time.");
        LONGLONG sampleDuration = 0;
        if (FAILED(sample->GetSampleDuration(&sampleDuration)) || sampleDuration <= 0)
            Ensure(sample->SetSampleDuration(expectedDuration), "SAMPLE_DURATION_SET_FAILED", "Could not set sample duration.");
        Ensure(writer->WriteSample(writerStream, sample.Get()), "SINK_WRITE_FAILED", "MP4 sink rejected a sample.");

        previousSourceTimestamp = sourceTimestamp;
        ++sequence;
        if (normalizedTimestamp >= static_cast<LONGLONG>(options.durationSeconds) * TicksPerSecond) break;
    }

    frames.flush();
    if (!frames) throw ProbeError("FRAME_EVIDENCE_WRITE_FAILED", "Writing frames.csv failed.");
    frames.close();
    if (sequence == 0) throw ProbeError("NO_SAMPLES_CAPTURED", "The camera produced no samples.");
    Ensure(writer->Finalize(), "SINK_FINALIZE_FAILED", "MP4 sink finalization failed.");
    finalized = true;
    std::ofstream result(resultPath, std::ios::binary | std::ios::trunc);
    if (!result) throw ProbeError("RESULT_WRITE_FAILED", "Could not create capture-result.json.");
    result << "{\n  \"format_name\": \"humcapture-mf-short-capture-result\",\n"
           << "  \"format_version\": \"0.1.0\",\n"
           << "  \"work_tags\": [\"P0.2B\", \"UVC\", \"CORE\", \"CAPTURE\", \"TIMING\", \"HARDWARE\"],\n"
           << "  \"run_id\": \"" << runId << "\",\n"
           << "  \"device_name\": \"" << EscapeJson(Utf8(device.name)) << "\",\n"
           << "  \"device_id\": \"" << EscapeJson(Utf8(device.normalizedId)) << "\",\n"
           << "  \"native_type_index\": " << profile.index << ",\n"
           << "  \"requested\": {\"width_px\": " << options.width << ", \"height_px\": " << options.height
           << ", \"fps_numerator\": " << options.fpsNumerator << ", \"fps_denominator\": " << options.fpsDenominator
           << ", \"subtype\": \"" << EscapeJson(Utf8(options.subtype)) << "\"},\n"
           << "  \"negotiated_exact\": true,\n"
           << "  \"source_timestamp_domain\": \"media_presentation\",\n"
           << "  \"source_timestamp_unit\": \"ticks_100ns\",\n"
           << "  \"host_arrival_timestamp_domain\": \"host_qpc\",\n"
           << "  \"host_arrival_timestamp_unit\": \"ns\",\n"
           << "  \"sample_count\": " << sequence << ",\n"
           << "  \"encoded_bytes\": " << totalBytes << ",\n"
           << "  \"stream_tick_count\": " << streamTicks << ",\n"
           << "  \"media_type_change_count\": " << typeChanges << ",\n"
           << "  \"timestamp_regression_count\": " << timestampRegressions << ",\n"
           << "  \"sink_finalized\": " << (finalized ? "true" : "false") << ",\n"
           << "  \"recovery_read_error_mode\": " << (options.finalizeOnReadError ? "true" : "false") << ",\n"
           << "  \"requested_duration_reached\": " << (terminalReason == "duration_reached" ? "true" : "false") << ",\n"
           << "  \"terminal_reason\": \"" << terminalReason << "\",\n"
           << "  \"terminal_hresult\": ";
    if (terminalHresult)
    {
        result << "\"0x" << std::uppercase << std::hex << static_cast<unsigned long>(*terminalHresult) << std::dec << "\"\n}\n";
    }
    else
    {
        result << "null\n}\n";
    }
    result.close();

    const bool finalizedAfterReadFailure = terminalReason == "read_error" || terminalReason == "source_reader_error_flag";
    std::cout << "{\"status\":\"" << (finalizedAfterReadFailure ? "CAPTURE_FINALIZED_AFTER_READ_ERROR" : "CAPTURE_FINALIZED")
              << "\",\"run_id\":\"" << runId
              << "\",\"sample_count\":" << sequence << ",\"output_directory\":\""
              << EscapeJson(Utf8(options.outputDirectory.wstring())) << "\"}\n";
    return finalizedAfterReadFailure ? 4 : 0;
}

void RunSelfTests()
{
    const std::wstring symbolic = LR"(\\?\usb#vid_046d&pid_082d&mi_00#6&dba5b52&2&0000#{e5323777-f976-4f5b-9b55-b94699c46e44}\global)";
    const std::wstring normalized = LR"(USB\VID_046D&PID_082D&MI_00\6&DBA5B52&2&0000)";
    if (NormalizeDeviceId(symbolic) != normalized) throw ProbeError("SELF_TEST_FAILED", "Device normalization failed.");
    if (NormalizeDeviceId(normalized) != normalized) throw ProbeError("SELF_TEST_FAILED", "Normalized identity was not stable.");
    std::vector<Device> devices(1);
    devices[0].name = L"synthetic-camera";
    devices[0].symbolicLink = symbolic;
    devices[0].normalizedId = normalized;
    if (&SelectDevice(devices, normalized) != &devices[0]) throw ProbeError("SELF_TEST_FAILED", "Exact device selection failed.");
    try
    {
        SelectDevice(devices, L"USB\\MISSING");
        throw ProbeError("SELF_TEST_FAILED", "Missing device selection did not fail.");
    }
    catch (const ProbeError& error)
    {
        if (error.Code() != "DEVICE_NOT_FOUND") throw;
    }
    devices.push_back(Device{});
    devices[1].name = L"synthetic-duplicate";
    devices[1].symbolicLink = symbolic;
    devices[1].normalizedId = normalized;
    try
    {
        SelectDevice(devices, normalized);
        throw ProbeError("SELF_TEST_FAILED", "Ambiguous device selection did not fail.");
    }
    catch (const ProbeError& error)
    {
        if (error.Code() != "DEVICE_IDENTITY_AMBIGUOUS") throw;
    }
    std::vector<Profile> profiles(2);
    profiles[0].index = 10; profiles[0].width = 1920; profiles[0].height = 1080;
    profiles[0].fpsNumerator = 30; profiles[0].fpsDenominator = 1; profiles[0].subtype = MFVideoFormat_H264;
    profiles[1] = profiles[0]; profiles[1].index = 11; profiles[1].fpsNumerator = 60;
    Options request{}; request.width = 1920; request.height = 1080; request.fpsNumerator = 60;
    request.fpsDenominator = 1; request.subtype = L"H264";
    if (MatchingProfiles(profiles, request).size() != 1) throw ProbeError("SELF_TEST_FAILED", "Exact profile matching failed.");
    request.fpsNumerator = 120;
    if (!MatchingProfiles(profiles, request).empty()) throw ProbeError("SELF_TEST_FAILED", "Unsupported profile matching substituted a result.");
    request.fpsNumerator = 30; request.nativeTypeIndex = 11;
    if (!MatchingProfiles(profiles, request).empty()) throw ProbeError("SELF_TEST_FAILED", "Native type index constraint was ignored.");
    std::cout << "SELF_TEST_PASS tests=8\n";
}
}

int wmain(const int argc, wchar_t* argv[])
{
    SetConsoleOutputCP(CP_UTF8);
    try
    {
        const Options options = ParseOptions(argc, argv);
        if (options.selfTest)
        {
            RunSelfTests();
            return 0;
        }
        Ensure(CoInitializeEx(nullptr, COINIT_MULTITHREADED), "COM_INITIALIZATION_FAILED", "COM initialization failed.");
        Ensure(MFStartup(MF_VERSION, MFSTARTUP_FULL), "MF_STARTUP_FAILED", "Media Foundation startup failed.");
        int exitCode = 0;
        try
        {
            std::vector<Device> devices = EnumerateDevices();
            Device& device = SelectDevice(devices, options.deviceId);
            ComPtr<IMFMediaSource> source;
            Ensure(device.activation->ActivateObject(IID_PPV_ARGS(&source)), "DEVICE_ACTIVATION_FAILED", "Camera activation failed.");
            ComPtr<IMFAttributes> readerAttributes;
            Ensure(MFCreateAttributes(&readerAttributes, 2), "READER_ATTRIBUTE_FAILED", "Could not create Source Reader attributes.");
            Ensure(readerAttributes->SetUINT32(MF_READWRITE_DISABLE_CONVERTERS, TRUE), "READER_ATTRIBUTE_FAILED", "Could not disable media-type converters.");
            Ensure(readerAttributes->SetUINT32(MF_SOURCE_READER_DISCONNECT_MEDIASOURCE_ON_SHUTDOWN, TRUE), "READER_ATTRIBUTE_FAILED", "Could not bind source shutdown.");
            ComPtr<IMFSourceReader> reader;
            Ensure(MFCreateSourceReaderFromMediaSource(source.Get(), readerAttributes.Get(), &reader), "READER_CREATION_FAILED", "Could not create Source Reader.");
            try
            {
                std::vector<Profile> profiles = EnumerateProfiles(reader.Get());
                if (options.listProfiles)
                {
                    WriteProfilesJson(device, profiles);
                }
                else
                {
                    std::vector<Profile*> matches = MatchingProfiles(profiles, options);
                    if (matches.empty()) throw ProbeError("REQUESTED_PROFILE_UNSUPPORTED", "The exact requested native media type is not reported; no fallback was attempted.", 3);
                    if (matches.size() > 1 && !options.nativeTypeIndex)
                        throw ProbeError("REQUESTED_PROFILE_AMBIGUOUS", "More than one native media type matches; specify --native-type-index.", 3);
                    exitCode = RunCapture(device, *matches.front(), options, reader.Get());
                }
                source->Shutdown();
            }
            catch (...)
            {
                source->Shutdown();
                throw;
            }
        }
        catch (const ProbeError& error)
        {
            WriteErrorJson(error);
            exitCode = error.ExitCode();
        }
        MFShutdown();
        CoUninitialize();
        return exitCode;
    }
    catch (const ProbeError& error)
    {
        WriteErrorJson(error);
        return error.ExitCode();
    }
    catch (const std::exception& error)
    {
        const ProbeError wrapped("UNHANDLED_PROBE_ERROR", error.what());
        WriteErrorJson(wrapped);
        return 1;
    }
}
