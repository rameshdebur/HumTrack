#include <windows.h>
#include <mfapi.h>
#include <mferror.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <wrl/client.h>

#include <iomanip>
#include <iostream>
#include <sstream>
#include <string>
#include <vector>

using Microsoft::WRL::ComPtr;

namespace
{
struct MediaTypeReport
{
    UINT32 width{};
    UINT32 height{};
    UINT32 fpsNumerator{};
    UINT32 fpsDenominator{};
    GUID subtype{};
};

struct DeviceReport
{
    std::wstring name;
    std::wstring symbolicLink;
    std::vector<MediaTypeReport> mediaTypes;
    HRESULT inspectionResult{S_OK};
};

std::string Utf8(const std::wstring& value)
{
    if (value.empty())
    {
        return {};
    }

    const int size = WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, value.data(),
        static_cast<int>(value.size()), nullptr, 0, nullptr, nullptr);
    if (size <= 0)
    {
        return {};
    }

    std::string result(static_cast<size_t>(size), '\0');
    WideCharToMultiByte(CP_UTF8, WC_ERR_INVALID_CHARS, value.data(),
        static_cast<int>(value.size()), result.data(), size, nullptr, nullptr);
    return result;
}

std::string JsonEscape(const std::string& value)
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
            else
            {
                output << character;
            }
        }
    }
    return output.str();
}

std::string GuidString(const GUID& value)
{
    wchar_t buffer[40]{};
    if (StringFromGUID2(value, buffer, static_cast<int>(std::size(buffer))) == 0)
    {
        return {};
    }
    return Utf8(buffer);
}

std::string HResultString(const HRESULT result)
{
    std::ostringstream output;
    output << "0x" << std::uppercase << std::hex << std::setw(8) << std::setfill('0')
        << static_cast<unsigned long>(result);
    return output.str();
}

std::wstring AllocatedString(IMFAttributes* attributes, const GUID& key)
{
    wchar_t* value = nullptr;
    UINT32 length = 0;
    if (FAILED(attributes->GetAllocatedString(key, &value, &length)))
    {
        return {};
    }

    std::wstring result(value, length);
    CoTaskMemFree(value);
    return result;
}

HRESULT InspectMediaTypes(IMFActivate* activation, DeviceReport& report)
{
    ComPtr<IMFMediaSource> source;
    HRESULT result = activation->ActivateObject(IID_PPV_ARGS(&source));
    if (FAILED(result))
    {
        return result;
    }

    ComPtr<IMFSourceReader> reader;
    result = MFCreateSourceReaderFromMediaSource(source.Get(), nullptr, &reader);
    if (FAILED(result))
    {
        source->Shutdown();
        return result;
    }

    for (DWORD index = 0;; ++index)
    {
        ComPtr<IMFMediaType> mediaType;
        result = reader->GetNativeMediaType(
            static_cast<DWORD>(MF_SOURCE_READER_FIRST_VIDEO_STREAM), index, &mediaType);
        if (result == MF_E_NO_MORE_TYPES)
        {
            result = S_OK;
            break;
        }
        if (FAILED(result))
        {
            break;
        }

        GUID majorType{};
        if (FAILED(mediaType->GetGUID(MF_MT_MAJOR_TYPE, &majorType)) || majorType != MFMediaType_Video)
        {
            continue;
        }

        MediaTypeReport type{};
        if (FAILED(MFGetAttributeSize(mediaType.Get(), MF_MT_FRAME_SIZE, &type.width, &type.height))
            || FAILED(MFGetAttributeRatio(mediaType.Get(), MF_MT_FRAME_RATE, &type.fpsNumerator, &type.fpsDenominator))
            || FAILED(mediaType->GetGUID(MF_MT_SUBTYPE, &type.subtype)))
        {
            continue;
        }
        report.mediaTypes.push_back(type);
    }

    source->Shutdown();
    return result;
}

void WriteReport(const std::vector<DeviceReport>& devices)
{
    std::cout << "{\n"
              << "  \"format_name\": \"humcapture-windows-native-mf-api-spike\",\n"
              << "  \"format_version\": \"0.1.0\",\n"
              << "  \"api_surface\": \"Media Foundation MFEnumDeviceSources/IMFSourceReader\",\n"
              << "  \"enumerated_device_count\": " << devices.size() << ",\n"
              << "  \"devices\": [\n";

    for (size_t deviceIndex = 0; deviceIndex < devices.size(); ++deviceIndex)
    {
        const DeviceReport& device = devices[deviceIndex];
        std::cout << "    {\n"
                  << "      \"name\": \"" << JsonEscape(Utf8(device.name)) << "\",\n"
                  << "      \"interface_id\": \"" << JsonEscape(Utf8(device.symbolicLink)) << "\",\n"
                  << "      \"inspection_status\": \"" << (SUCCEEDED(device.inspectionResult) ? "ok" : "error") << "\",\n"
                  << "      \"inspection_hresult\": \"" << HResultString(device.inspectionResult) << "\",\n"
                  << "      \"native_media_types\": [\n";

        for (size_t typeIndex = 0; typeIndex < device.mediaTypes.size(); ++typeIndex)
        {
            const MediaTypeReport& type = device.mediaTypes[typeIndex];
            std::cout << "        {\"width_px\": " << type.width
                      << ", \"height_px\": " << type.height
                      << ", \"fps_numerator\": " << type.fpsNumerator
                      << ", \"fps_denominator\": " << type.fpsDenominator
                      << ", \"subtype_guid\": \"" << GuidString(type.subtype) << "\"}"
                      << (typeIndex + 1 == device.mediaTypes.size() ? "\n" : ",\n");
        }

        std::cout << "      ]\n"
                  << "    }" << (deviceIndex + 1 == devices.size() ? "\n" : ",\n");
    }

    std::cout << "  ]\n}\n";
}
}

int wmain()
{
    SetConsoleOutputCP(CP_UTF8);
    HRESULT result = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    if (FAILED(result))
    {
        std::cerr << "{\"error\":{\"code\":\"COM_INITIALIZATION_FAILED\",\"hresult\":\""
                  << HResultString(result) << "\"}}\n";
        return 1;
    }

    result = MFStartup(MF_VERSION, MFSTARTUP_FULL);
    if (FAILED(result))
    {
        std::cerr << "{\"error\":{\"code\":\"MF_STARTUP_FAILED\",\"hresult\":\""
                  << HResultString(result) << "\"}}\n";
        CoUninitialize();
        return 1;
    }

    ComPtr<IMFAttributes> attributes;
    result = MFCreateAttributes(&attributes, 1);
    if (SUCCEEDED(result))
    {
        result = attributes->SetGUID(MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE,
            MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID);
    }

    IMFActivate** activations = nullptr;
    UINT32 count = 0;
    if (SUCCEEDED(result))
    {
        result = MFEnumDeviceSources(attributes.Get(), &activations, &count);
    }

    if (FAILED(result))
    {
        std::cerr << "{\"error\":{\"code\":\"MF_ENUMERATION_FAILED\",\"hresult\":\""
                  << HResultString(result) << "\"}}\n";
        MFShutdown();
        CoUninitialize();
        return 1;
    }

    std::vector<DeviceReport> devices;
    devices.reserve(count);
    for (UINT32 index = 0; index < count; ++index)
    {
        DeviceReport report{};
        report.name = AllocatedString(activations[index], MF_DEVSOURCE_ATTRIBUTE_FRIENDLY_NAME);
        report.symbolicLink = AllocatedString(activations[index],
            MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_SYMBOLIC_LINK);
        report.inspectionResult = InspectMediaTypes(activations[index], report);
        devices.push_back(std::move(report));
        activations[index]->Release();
    }
    CoTaskMemFree(activations);

    WriteReport(devices);
    MFShutdown();
    CoUninitialize();
    return 0;
}
