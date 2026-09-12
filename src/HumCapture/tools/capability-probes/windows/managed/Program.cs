using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;

namespace HumCapture.CapabilityProbe.Windows;

internal static class Program
{
    [STAThread]
    public static Task<int> Main(string[] args) => ManagedProbe.RunAsync(args);
}

internal static class ManagedProbe
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        WriteIndented = true
    };

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            if (args is ["--self-test"])
            {
                RunSelfTests();
                Console.WriteLine("SELF_TEST_PASS tests=7");
                return 0;
            }

            string? requestedIdentity = ReadOption(args, "--device-id");
            string? exposureAutoText = ReadOption(args, "--set-exposure-auto");
            string? exposureValueText = ReadOption(args, "--set-exposure-value");
            bool? exposureAuto = exposureAutoText is null ? null : ParseBoolean(exposureAutoText, "--set-exposure-auto");
            double? exposureValue = exposureValueText is null ? null : ParseDouble(exposureValueText, "--set-exposure-value");
            bool changesRequested = exposureAuto.HasValue || exposureValue.HasValue;
            if (changesRequested && requestedIdentity is null)
            {
                throw new CameraSelectionException("EXACT_DEVICE_REQUIRED", "Camera-control changes require --device-id with an exact identity.");
            }
            DeviceInformationCollection devices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            List<CameraCandidate> candidates = devices
                .Select(device => new CameraCandidate(device.Name, device.Id, CameraIdentity.Normalize(device.Id)))
                .ToList();

            IEnumerable<CameraCandidate> selected = requestedIdentity is null
                ? candidates
                : [CameraIdentity.SelectExact(candidates, requestedIdentity)];

            var reports = new List<object>();
            foreach (CameraCandidate candidate in selected)
            {
                reports.Add(await InspectAsync(candidate, exposureAuto, exposureValue));
            }

            WriteJson(new
            {
                format_name = "humcapture-windows-managed-api-spike",
                format_version = "0.1.0",
                api_surface = "Windows.Devices.Enumeration+Windows.Media.Capture",
                generated_utc = DateTimeOffset.UtcNow,
                requested_device_identity = requestedIdentity,
                enumerated_device_count = candidates.Count,
                devices = reports
            });
            return 0;
        }
        catch (CameraSelectionException exception)
        {
            WriteJson(new { error = new { code = exception.Code, message = exception.Message } });
            return 2;
        }
        catch (Exception exception)
        {
            WriteJson(new
            {
                error = new
                {
                    code = "MANAGED_PROBE_FAILED",
                    message = exception.Message,
                    hresult = $"0x{exception.HResult:X8}",
                    exception_type = exception.GetType().FullName
                }
            });
            return 1;
        }
    }

    private static async Task<object> InspectAsync(CameraCandidate candidate, bool? exposureAuto, double? exposureValue)
    {
        bool changesRequested = exposureAuto.HasValue || exposureValue.HasValue;
        try
        {
            using var capture = new MediaCapture();
            var settings = new MediaCaptureInitializationSettings
            {
                VideoDeviceId = candidate.InterfaceId,
                StreamingCaptureMode = StreamingCaptureMode.Video,
                SharingMode = changesRequested ? MediaCaptureSharingMode.ExclusiveControl : MediaCaptureSharingMode.SharedReadOnly,
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            };
            await capture.InitializeAsync(settings);

            var appliedChanges = new List<object>();
            global::Windows.Media.Devices.MediaDeviceControl exposure = capture.VideoDeviceController.Exposure;
            if (exposureAuto.HasValue)
            {
                if (!exposure.Capabilities.AutoModeSupported || !exposure.TrySetAuto(exposureAuto.Value))
                {
                    throw new InvalidOperationException($"Unable to set exposure auto mode to {exposureAuto.Value}.");
                }
                appliedChanges.Add(new { control_id = "exposure", property = "auto_mode", requested = exposureAuto.Value, applied = true });
            }
            if (exposureValue.HasValue)
            {
                if (!exposure.Capabilities.Supported || exposureValue.Value < exposure.Capabilities.Min ||
                    exposureValue.Value > exposure.Capabilities.Max || !exposure.TrySetValue(exposureValue.Value))
                {
                    throw new InvalidOperationException($"Unable to set exposure value to {exposureValue.Value.ToString(CultureInfo.InvariantCulture)}.");
                }
                appliedChanges.Add(new { control_id = "exposure", property = "value", requested = exposureValue.Value, applied = true });
            }

            IReadOnlyList<IMediaEncodingProperties> properties = capture.VideoDeviceController
                .GetAvailableMediaStreamProperties(MediaStreamType.VideoRecord);
            var formats = properties.OfType<VideoEncodingProperties>()
                .Select(property => new
                {
                    width_px = property.Width,
                    height_px = property.Height,
                    fps_numerator = property.FrameRate.Numerator,
                    fps_denominator = property.FrameRate.Denominator,
                    subtype = property.Subtype,
                    bitrate_bps = property.Bitrate
                })
                .OrderBy(format => format.width_px)
                .ThenBy(format => format.height_px)
                .ThenBy(format => format.fps_denominator == 0 ? 0 : (double)format.fps_numerator / format.fps_denominator)
                .ToArray();

            return new
            {
                candidate.Name,
                interface_id = candidate.InterfaceId,
                normalized_instance_id = candidate.NormalizedInstanceId,
                initialization = new { status = "ok", error = (object?)null },
                control_changes = appliedChanges,
                video_record_formats = formats,
                camera_controls = new[]
                {
                    DescribeControl("backlight_compensation", capture.VideoDeviceController.BacklightCompensation),
                    DescribeControl("brightness", capture.VideoDeviceController.Brightness),
                    DescribeControl("contrast", capture.VideoDeviceController.Contrast),
                    DescribeControl("exposure", capture.VideoDeviceController.Exposure),
                    DescribeControl("focus", capture.VideoDeviceController.Focus),
                    DescribeControl("hue", capture.VideoDeviceController.Hue),
                    DescribeControl("pan", capture.VideoDeviceController.Pan),
                    DescribeControl("roll", capture.VideoDeviceController.Roll),
                    DescribeControl("tilt", capture.VideoDeviceController.Tilt),
                    DescribeControl("white_balance", capture.VideoDeviceController.WhiteBalance),
                    DescribeControl("zoom", capture.VideoDeviceController.Zoom)
                }
            };
        }
        catch (Exception exception)
        {
            if (changesRequested)
            {
                throw;
            }
            return new
            {
                candidate.Name,
                interface_id = candidate.InterfaceId,
                normalized_instance_id = candidate.NormalizedInstanceId,
                initialization = new
                {
                    status = "error",
                    error = new
                    {
                        code = "CAMERA_INITIALIZATION_FAILED",
                        message = exception.Message,
                        hresult = $"0x{exception.HResult:X8}",
                        exception_type = exception.GetType().FullName
                    }
                },
                control_changes = Array.Empty<object>(),
                video_record_formats = Array.Empty<object>(),
                camera_controls = Array.Empty<object>()
            };
        }
    }

    private static object DescribeControl(string controlId, global::Windows.Media.Devices.MediaDeviceControl control)
    {
        global::Windows.Media.Devices.MediaDeviceControlCapabilities capabilities = control.Capabilities;
        double? currentValue = null;
        bool? currentAutoMode = null;
        bool currentValueReadable = false;
        bool currentAutoModeReadable = false;
        if (capabilities.Supported)
        {
            currentValueReadable = control.TryGetValue(out double value);
            if (currentValueReadable)
            {
                currentValue = value;
            }

            if (capabilities.AutoModeSupported)
            {
                currentAutoModeReadable = control.TryGetAuto(out bool autoMode);
                if (currentAutoModeReadable)
                {
                    currentAutoMode = autoMode;
                }
            }
        }

        return new
        {
            control_id = controlId,
            supported = capabilities.Supported,
            auto_mode_supported = capabilities.AutoModeSupported,
            minimum = capabilities.Supported ? capabilities.Min : (double?)null,
            maximum = capabilities.Supported ? capabilities.Max : (double?)null,
            step = capabilities.Supported ? capabilities.Step : (double?)null,
            default_value = capabilities.Supported ? capabilities.Default : (double?)null,
            current_value_readable = currentValueReadable,
            current_value = currentValue,
            current_auto_mode_readable = currentAutoModeReadable,
            current_auto_mode = currentAutoMode
        };
    }

    private static string? ReadOption(string[] args, string option)
    {
        int index = Array.IndexOf(args, option);
        if (index < 0)
        {
            return null;
        }

        if (index == args.Length - 1 || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            throw new CameraSelectionException("MISSING_OPTION_VALUE", $"{option} requires a value.");
        }

        return args[index + 1];
    }

    private static bool ParseBoolean(string value, string option)
    {
        if (bool.TryParse(value, out bool parsed))
        {
            return parsed;
        }
        throw new CameraSelectionException("INVALID_OPTION_VALUE", $"{option} requires true or false.");
    }

    private static double ParseDouble(string value, string option)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) && double.IsFinite(parsed))
        {
            return parsed;
        }
        throw new CameraSelectionException("INVALID_OPTION_VALUE", $"{option} requires a finite invariant-culture number.");
    }

    private static void RunSelfTests()
    {
        const string interfaceId = @"\\?\USB#VID_30C9&PID_000E&MI_00#6&16B94EAF&0&0000#{E5323777-F976-4F5B-9B55-B94699C46E44}\GLOBAL";
        const string expected = @"USB\VID_30C9&PID_000E&MI_00\6&16B94EAF&0&0000";
        AssertEqual(expected, CameraIdentity.Normalize(interfaceId), "normalize USB camera identity");
        AssertEqual(null, CameraIdentity.Normalize("SWD\\MMDEVAPI\\virtual"), "do not invent a USB identity");

        var first = new CameraCandidate("HP", interfaceId, expected);
        AssertEqual(first, CameraIdentity.SelectExact([first], expected), "select exact instance identity");
        AssertThrows("DEVICE_NOT_FOUND", () => CameraIdentity.SelectExact([first], "USB\\MISSING"));
        AssertThrows("DEVICE_IDENTITY_AMBIGUOUS", () => CameraIdentity.SelectExact([first, first with { Name = "duplicate" }], expected));
        AssertEqual(false, ParseBoolean("false", "test"), "parse false control option");
        AssertEqual(-5.0, ParseDouble("-5", "test"), "parse numeric control option");
    }

    private static void AssertEqual<T>(T expected, T actual, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"Self-test failed ({name}): expected '{expected}', actual '{actual}'.");
        }
    }

    private static void AssertThrows(string expectedCode, Action action)
    {
        try
        {
            action();
        }
        catch (CameraSelectionException exception) when (exception.Code == expectedCode)
        {
            return;
        }

        throw new InvalidOperationException($"Self-test failed: expected CameraSelectionException '{expectedCode}'.");
    }

    private static void WriteJson(object value) => Console.WriteLine(JsonSerializer.Serialize(value, JsonOptions));
}
