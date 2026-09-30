# P0.2A Windows Camera API Spike Report

**Report ID:** HC-P0-VR-002A  
**Date:** 2026-08-31  
**Disposition:** Source and named-device API spike passed; P0.2 capture gate remains open  
**Tags:** P0.2A | UVC | CORE | ENUMERATION | HARDWARE

## Scope

This report covers only Windows camera enumeration, identity correlation,
reported-format comparison, and managed standard-control visibility. It does not
cover recording, per-sample timestamps, exact configuration attempts, media
finalization, soak, disconnect/reconnect, concurrent capture, other external
camera models, field workflow, clinical use, or regulatory review.

## Environment and named hardware

- Host: Windows 11 x64, observed OS version `10.0.26200`.
- Managed target: .NET 8, Windows SDK contract floor `10.0.19041.0`, x64.
- Native target: Visual C++ v145, Windows SDK `10.0.19041.0`, x64.
- Camera 1: HP Wide Vision HD Camera,
  `USB\\VID_30C9&PID_000E&MI_00\\6&16B94EAF&0&0000`.
- Camera 2: Logitech HD Pro Webcam C920,
  `USB\\VID_046D&PID_082D&MI_00\\6&DBA5B52&2&0000`, parent serial
  `0E1A0C0F`, hub path `USB(1)#USB(2)`.
- Camera 3: Logitech HD Pro Webcam C920,
  `USB\\VID_046D&PID_082D&MI_00\\7&2C44E5B7&0&0000`, parent serial
  `0352323F`, hub path `USB(1)#USB(3)`.
- Windows service: `usbvideo` for all three video interfaces.
- HP driver: Microsoft `usbvideo.inf`; Logitech video-interface driver version:
  `1.4.40.0`.
- USB class evidence: Video class `0E`, subclass `03`.

## Implemented spike

- Managed enumerator using `DeviceInformation` and `MediaCapture`.
- Exact identity selection; no friendly-name or first-device fallback.
- Explicit initialization failures with stable error codes and HRESULTs.
- Rational frame-rate preservation.
- Managed standard-control support/range reporting.
- Native Media Foundation enumeration and native media-type inspection.
- Automated cross-surface identity and format-set comparison.
- Five synthetic self-tests for identity normalization, exact selection,
  not-found behavior, and ambiguity rejection.

## Runtime findings

Both API surfaces:

- enumerated seven video endpoints after both C920 cameras were attached;
- identified the same HP camera interface;
- inspected it successfully; and
- reported the same 17 unique width/height/frame-rate/subtype combinations.

The HP camera's maximum reported mode was `1280×720 at 30/1 fps`. It did not
report `1920×1080 at 60 fps`. Therefore it is suitable for API, failure-path,
lower-profile diagnostic, and duration experiments, but it cannot qualify the
normative `HC-P0-UVC-001` 1080p60 procedure.

Managed control reporting marked these controls supported:

- backlight compensation;
- brightness;
- contrast;
- exposure;
- hue; and
- white balance.

Focus, pan, roll, tilt, and zoom were reported unsupported through this surface.

### Additional Logitech C920 findings

Both attached C920 units were present, healthy, and distinguishable by interface
instance, parent serial, and USB location. For each camera:

- Windows assigned the video interface to the legacy `Image` PnP class rather
  than the newer `Camera` class; the video-capture API selector still found it;
- managed initialization passed;
- native Media Foundation inspection passed;
- exact cross-surface identity correlation passed;
- managed `MediaCapture` exposed 336 unique format signatures;
- native Media Foundation exposed all 336 managed signatures plus 119 native
  H.264 signatures, for 455 unique signatures total;
- native H.264 included `1920×1080 at 30/1 fps`, but not 60 fps; and
- managed controls reported backlight compensation, brightness, contrast,
  exposure, focus, pan, tilt, white balance, and zoom supported.

The managed/native difference is evidence about API visibility, not a device
failure. It strengthens the proposed native Media Foundation acquisition
boundary because the managed shared-read-only surface omitted native H.264
profiles that Media Foundation exposed.

Future hardware inventory must therefore enumerate video-capture interfaces (and
correlate `usbvideo`/PnP identity) rather than filtering only the PnP `Camera`
class, which would omit both tested C920 units.

## Verification evidence

```powershell
dotnet build tools/capability-probes/windows/managed/HumCapture.ManagedCameraProbe.csproj -c Release
dotnet run --project tools/capability-probes/windows/managed/HumCapture.ManagedCameraProbe.csproj -c Release --no-build -- --self-test
MSBuild.exe tools/capability-probes/windows/native-mf/HumCapture.MfEnumerator.vcxproj /p:Configuration=Release /p:Platform=x64
tools/capability-probes/windows/Invoke-ApiSpike.ps1 -DeviceId 'USB\VID_30C9&PID_000E&MI_00\6&16B94EAF&0&0000'
```

Observed results:

- Managed build: passed with zero errors and zero warnings.
- Native build: passed with zero errors and zero warnings.
- Self-tests: `5/5` passed.
- Managed/native exact identity match: passed.
- HP managed/native format sets: `17/17`, exact match.
- Each C920 managed/native format relation: native superset, `336/455`, with
  119 native-only H.264 signatures.
- Managed and native camera initialization/inspection: passed.

## Evidence levels

- Source implemented: **Yes**, for P0.2A only.
- Build/static checks passed: **Yes**.
- Automated behavior verified: **Yes**, for the five synthetic identity/selection tests and comparison checks.
- Runtime integration verified: **Yes**, limited to enumeration and inspection on this Windows host.
- Hardware-in-the-loop verified: **Partial**, limited to three named physical
  cameras and non-recording API inspection.
- Field workflow verified: **No**.
- Regulatory or clinical review completed: **No**.

## Next gate

P0.2B must attempt an exact requested media type, record a short synthetic-scene
diagnostic, preserve per-sample timestamp/provenance evidence, finalize readable
media, and validate the resulting P0.1 package. All three available cameras
require an inconclusive/nonconforming 1080p60 result rather than a substituted
pass; the C920 cameras can separately exercise native 1080p30 H.264 capture.
