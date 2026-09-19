# Windows/UVC capability probe

This directory contains isolated, non-production Phase 0 diagnostics. It is not
the HumCapture coordinator or the production UVC worker.

P0.2A compares:

- `managed/` — Windows `DeviceInformation` and `MediaCapture` visibility.
- `native-mf/` — native Media Foundation device/media-type visibility.

No output from these tools qualifies a camera by itself. Named-hardware capture,
timestamp, finalization, soak, disconnect/reconnect, and field evidence remain
separate gates.

## P0.2B native short capture

`native-mf/HumCapture.MfCapture.vcxproj` provides exact native-media-type
selection and sequential diagnostic capture. It deliberately rejects missing or
ambiguous device/profile selections. Runtime output must be outside this source
checkout.

Example after listing native profiles and choosing an explicit matching index:

```powershell
native-mf\x64\Release\HumCapture.MfCapture.exe `
  --device-id 'USB\VID_046D&PID_082D&MI_00\…' `
  --width 1920 --height 1080 `
  --fps-numerator 30 --fps-denominator 1 `
  --subtype H264 --native-type-index 560 `
  --duration-seconds 3 --output-dir "$env:TEMP\humcapture-diagnostic"

.\Verify-ShortCapture.ps1 `
  -CaptureDirectory "$env:TEMP\humcapture-diagnostic" `
  -ExpectedDeviceId 'USB\VID_046D&PID_082D&MI_00\…'
```

The source timestamp is the unmodified Media Foundation presentation timestamp
in 100 ns ticks. Host arrival is a separate `QueryPerformanceCounter` observation
converted to nanoseconds; neither is represented as a camera-sensor timestamp.

`Verify-ShortCapture.ps1` requires both structural inspection and a complete
error-free H.264 decode. Frame counts, finalization, or `ffprobe` metadata alone
do not establish media integrity.

## Exact-device exposure diagnostic

The managed probe reports current camera-control values and automatic-mode state.
For isolated diagnostics it can also change exposure on one exact device:

```powershell
managed\bin\Release\net8.0-windows10.0.19041.0\HumCapture.ManagedCameraProbe.exe `
  --device-id 'USB\VID_046D&PID_082D&MI_00\…' `
  --set-exposure-auto false --set-exposure-value -5
```

Control mutation requires an exact `--device-id`. It is temporary diagnostic
behavior, not a production camera policy. Record the initial state, obtain
operator authorization, restore it in a `finally` path, and independently
re-read the device after restoration. Never infer achieved cadence from the
requested exposure value; verify the captured source timestamps and full media.
