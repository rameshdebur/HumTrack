# HC-IF-TIM-001 — Timing, IMU, camera metadata and clock mapping

**Version:** 1.0.0  
**Status:** Accepted engineering baseline  
**Scope:** Android Camera2 and Windows UVC acquisition packages

## Purpose and authority

This contract preserves measured source evidence for downstream alignment and
camera-motion analysis. Native timestamps remain authoritative. Mapped session
time, cadence statistics, coordinate transforms and motion events are derived
interpretations with explicit provenance and uncertainty.

The scientific priority is master capture and native sensor timing, then safe
finalization, health, preview, UI and transfer. A failure in analytics,
preview, networking or transfer cannot rewrite or synthesize acquisition data.

## Package artifacts

Every complete Android or UVC source package contains
`timing/frame-timestamps.bin`, `metadata/timing-metadata.json` and
`metadata/camera-metadata.json`. Android additionally contains
`imu/imu-samples.bin` and `imu/imu-metadata.json`; the metadata explicitly lists
unavailable sensors and the sample file preserves every available requested
stream. Missing protocol-required IMU capability makes quality nonconformant;
it does not fabricate sensor observations.

CSV is an optional derived inspection export and is never the scientific
master. Each camera and each Android IMU file has an independent stream UUID.
Multiple cameras in one trial do not thereby share a clock or synchronization.

## Common binary envelope

Integers are unsigned little-endian unless stated otherwise. Floating-point
values are IEEE-754 binary32 little-endian. UUIDs are the 16 bytes shown by
canonical UUID hexadecimal text order; Windows GUID mixed-endian memory layout
is not used.

| Offset | Size | Field |
|---:|---:|---|
| 0 | 8 | Magic: `HCTIME1\0` for frames or `HCIMU1\0\0` for IMU |
| 8 | 2 | Major version, `1` |
| 10 | 2 | Minor version, `0` |
| 12 | 2 | Header size, `64` |
| 14 | 2 | Record size: frame `96`, IMU `80` |
| 16 | 4 | Flags; bit 0 means finalized |
| 20 | 16 | Stream UUID |
| 36 | 16 | Capture-attempt UUID |
| 52 | 8 | Complete record count |
| 60 | 4 | CRC32C of bytes 0–59 |

CRC32C uses the Castagnoli polynomial. Each fixed record ends with CRC32C over
all preceding record bytes. Finalized streams require an exact record count and
no partial tail. During recovery, a non-finalized stream may discard only its
partial final record; the discarded byte count and recovery action are recorded.
Finalized package manifests bind file byte lengths and SHA-256 hashes.

Unknown major versions fail closed. A higher minor version is accepted only by
a consumer that understands the declared sizes and required semantics. Version
1.0 validators therefore reject higher minor versions. Acquisition is
append-only; later clock fits or calibrations create referenced derivatives and
never edit the binary master.

## Frame record, 96 bytes

| Offset | Size | Field |
|---:|---:|---|
| 0 | 8 | HumCapture frame sequence |
| 8 | 8 | Authoritative native/source ticks |
| 16 | 8 | Platform source-frame number |
| 24 | 8 | Host-arrival ticks |
| 32 | 8 | Mapped session ticks |
| 40 | 8 | Encoded/container presentation ticks, signed little-endian |
| 48 | 8 | Video presentation-order frame index |
| 56 | 4 | Clock-model ID; zero when unmapped |
| 60 | 4 | Mapping uncertainty, nanoseconds |
| 64 | 4 | Capture segment ID |
| 68 | 2 | Frame disposition |
| 70 | 2 | Presence flags |
| 72 | 4 | Reserved status flags; zero in v1.0 |
| 76 | 8 | Exposure duration, nanoseconds |
| 84 | 8 | Rolling-shutter skew, nanoseconds |
| 92 | 4 | Record CRC32C |

Presence bits 0–6 correspond in table order to the optional fields at offsets
16, 24, 32, 40, 48, 76 and 84. Absent fields contain zero bytes but zero never
means absent. Dispositions are `1 ACCEPTED`, `2 DROPPED_BEFORE_ENCODING`,
`3 REJECTED_CORRUPT`, `4 ENCODER_FAILURE`, `5 FINALIZATION_LOSS`, and
`255 UNKNOWN`.

The sequence represents a source frame observed at the scientific acquisition
boundary, not preview refresh. Platform frame number is separate. Encoder
duplication/deletion, unmatched frames, source gaps and finalization loss remain
explicit. Generated constant-rate duplicates are sparse camera-metadata video
transformation events linked to their source sequence and video index; they are
not fabricated source-frame records. A restart creates a new segment. Nominal
FPS cannot create source records or timestamps.

## IMU record, 80 bytes

| Offset | Size | Field |
|---:|---:|---|
| 0 | 8 | Per-sensor sequence |
| 8 | 8 | Authoritative native sensor ticks |
| 16 | 8 | Host-arrival ticks |
| 24 | 8 | Mapped session ticks |
| 32 | 16 | `x`, `y`, `z`, `w` binary32 values |
| 48 | 4 | Clock-model ID; zero when unmapped |
| 52 | 4 | Mapping uncertainty, nanoseconds |
| 56 | 4 | Capture segment ID |
| 60 | 2 | Sensor-stream numeric ID |
| 62 | 1 | Sensor kind |
| 63 | 1 | Platform accuracy, signed; `-1` unavailable |
| 64 | 2 | Presence flags |
| 66 | 2 | Reserved status flags; zero in v1.0 |
| 68 | 4 | Delivery batch ID |
| 72 | 4 | Reserved; zero in v1.0 |
| 76 | 4 | Record CRC32C |

Presence bits 0–3 mean host arrival, mapped session time, `w`, and batch ID.
Kinds are `1 ACCELEROMETER`, `2 GYROSCOPE`, `3 ROTATION_VECTOR`, `4 GRAVITY`,
`5 LINEAR_ACCELERATION`, and `6 MAGNETOMETER`. Present components must be finite;
NaN and infinity are rejected. Each numeric sensor ID maps to a UUID and
metadata entry. Every sensor has an independent sequence domain.

Version 1.0 rejects unknown header/presence/disposition/sensor values,
non-zero reserved fields, non-canonical bytes hidden behind a clear presence
bit, and clock-model evidence without mapped time. This prevents an older
consumer from silently accepting semantics it does not understand.

Android requests raw accelerometer and gyroscope at 100 Hz and derived rotation
vector/gravity/linear acceleration at 50 Hz when available, with active-capture
`maxReportLatencyUs=0`. Requested cadence is not evidence. Native event times,
arrival/batch evidence, actual cadence, gaps, duplicates, regressions and
discontinuities are retained. Scientific masters are never interpolated or
resampled.

## Clock domains and mappings

Android SensorEvent time uses the platform monotonic sensor clock. Camera2
`SENSOR_TIMESTAMP` may be compared directly only when its documented timestamp
source shares that clock and epoch. Unknown camera time sources receive a
distinct clock ID. Encoder PTS remains separate.

For Windows UVC, device time, Media Foundation sample/presentation time and QPC
arrival remain distinct. In the absence of device time, provenance is
`HOST_SAMPLE` or `HOST_ARRIVAL`, never sensor time.

Clock IDs are scoped to a boot/capture epoch. Version 1 models are identity,
offset or affine: `target_ticks = scale * source_ticks + offset_ticks`. Each
model states clocks, validity interval, anchors, residuals, uncertainty and
`VALID`, `DEGRADED` or `UNAVAILABLE` quality. No model crosses a regression,
restart or epoch change. UTC is audit-only. No software mapping supports a
hardware-synchronization claim.

## Camera, lens and coordinate metadata

Camera metadata separates advertised, selected and observed cadence and accepts
fixed, variable, adaptive and unknown rate-control classes. Lack of adaptive
rate support is `NOT_SUPPORTED`, not a universal failure. Protocol rules decide
required cadence, provenance and lens fields.

Reported camera identity, UVC driver/media type, Android Camera2 identity,
focal length, sensor size, distortion/intrinsics, focus, zoom/crop,
stabilization, exposure, ISO and orientation are retained when exposed. Missing
values carry reasons and are never guessed. Geometry-relevant changes are sparse
events linked to the first affected frame.

Raw Android natural device axes are +X right, +Y toward the top and +Z out of
the screen; screen rotation does not rewrite raw samples. Display, sensor,
preview, recorded-video, device-to-camera, gravity and session transforms remain
separate with handedness, units and provenance.

Camera–IMU association is explicit, versioned and validity-bounded. Co-location
in a phone does not prove shared time or calibrated rotation. UVC-to-phone IMU
association requires operator assignment and documented/calibrated mounting.
Derived horizon, relative pitch/roll, motion intensity and pan/tilt candidates
reference their raw streams, clock model, transform, algorithm and uncertainty.
Yaw may drift; IMU-only translation or world position is not claimed.

## Quality and protocol gates

Protocol conditions are `REQUIRED`, `PREFERRED` or `INFORMATIONAL`. Pre-capture
camera states are `READY`, `READY_WITH_LIMITATIONS`, `BLOCKED` and `NOT_TESTED`.
Post-capture states are `CONFORMANT`, `DEGRADED_ACCEPTABLE`, `REVIEW_REQUIRED`
and `REJECTED`. A controlled reconciliation may retry the same configuration,
select another advertised mode/port, use a protocol-approved downgrade, replace
the camera or cancel/document an authorized exception. No mode changes silently.

Quality, package transfer/commit and protocol-take acceptance are independent.
A rejected take is preserved and transferred. Network failure does not change
scientific quality. Only protocol-required cameras affect overall take
acceptance; a protocol never acquires a second-camera requirement implicitly.

## Conformance evidence

Canonical vectors are under `tools/evidence-control/fixtures/timing/v1/` with
byte length, record count and SHA-256 identity. HC-TIM-TEST-001–012 cover schema,
golden decode/round-trip, integrity, truncation recovery, versions, independent
sensor sequences, cadence/discontinuities, mappings, metadata, fixed-rate
acceptance and frame/video association. These are source-level automated
contract tests, not Android/UVC runtime, HIL, field, clinical or regulatory
evidence.

## Compatibility

Version 1.0.0 adds package artifacts previously listed generically by
HC-IF-XFR-001 without changing transfer mechanics. Existing pre-contract
packages remain historical artifacts and are not silently upgraded. Consumers
must dispatch by artifact role, declared schema/binary version and magic; unknown
required semantics fail safely.

## Normative technical references

- Android `SensorEvent` timestamps and device coordinates:
  https://developer.android.com/reference/android/hardware/SensorEvent
- Android `SensorManager` sampling and batching semantics:
  https://developer.android.com/reference/android/hardware/SensorManager
- Android Camera2 `CaptureResult.SENSOR_TIMESTAMP`:
  https://developer.android.com/reference/android/hardware/camera2/CaptureResult#SENSOR_TIMESTAMP
- Android camera timestamp-source declaration:
  https://developer.android.com/reference/android/hardware/camera2/CameraCharacteristics#SENSOR_INFO_TIMESTAMP_SOURCE
- Microsoft Media Foundation device timestamp attribute:
  https://learn.microsoft.com/en-us/windows/win32/medfound/mfsampleextension-devicetimestamp
- Microsoft QueryPerformanceCounter guidance:
  https://learn.microsoft.com/en-us/windows/win32/sysinfo/acquiring-high-resolution-time-stamps
- RFC 6234 SHA algorithms: https://www.rfc-editor.org/rfc/rfc6234.html
