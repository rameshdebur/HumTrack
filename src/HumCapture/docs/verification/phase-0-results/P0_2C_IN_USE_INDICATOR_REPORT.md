# P0.2C UVC In-Use Indicator Report

**Report ID:** HC-P0-VR-002C  
**Date:** 2026-08-31  
**Disposition:** Passed for two named Logitech C920 configurations  
**Tags:** P0.2C | UVC | HARDWARE | IN-USE-INDICATOR | PRIVACY | OPERATOR

## Scope and interpretation

This report covers the manufacturer-documented physical activity indicators on
two attached Logitech C920 units. An indicator is an optional camera capability;
a camera designed without one is `NOT_APPLICABLE` and is not rejected for its
absence. Indicator behavior is operator/privacy evidence, not capture timing,
synchronization, recording integrity, or scientific evidence.

## Named identities

| Label | Windows device-interface ID | Parent serial | USB location |
|---|---|---|---|
| C920 A | `USB\VID_046D&PID_082D&MI_00\6&DBA5B52&2&0000` | `0E1A0C0F` | `USB(1)#USB(2)` |
| C920 B | `USB\VID_046D&PID_082D&MI_00\7&2C44E5B7&0&0000` | `0352323F` | `USB(1)#USB(3)` |

The trained operator confirmed that only the selected camera illuminated and
that C920 A and C920 B illuminated different physical units. The units were not
externally labelled, so their physical identity is retained through this
relative A/B mapping plus Windows/parent/USB evidence rather than a visible
asset label.

## Observations and associated capture evidence

All four observations were: indicator off before camera open, continuously on
during fresh-frame acquisition, and off after finalization/source release.

| Camera | Run ID | Samples | Duration | Measured cadence | Indicator result |
|---|---|---:|---:|---:|---|
| A | `BC9C99AA-BF67-4908-BBFA-5AA5DB0D84C6` | 722 | 30.081133 s | 24.0018 fps | PASS |
| A repeat | `AC88069E-2262-41A2-86F9-FEF5A22AAF8F` | 902 | 30.065167 s | 30.0015 fps | PASS |
| B | `3DB7682C-54BC-4325-9835-AF1B7D1E6395` | 902 | 30.065333 s | 30.0013 fps | PASS |
| B repeat | `0E60A328-EE16-4A26-A36D-DA719B7CE91B` | 902 | 30.065200 s | 30.0015 fps | PASS |

Every run finalized a readable H.264 1920×1080 MP4. CSV sample rows equalled
the reported and decoded frame counts. Each run reported one stream tick, zero
media-type changes, zero timestamp regressions, and no capture process remained
after release. Raw media and frame evidence remain outside source control under
the signed-in account's temporary directory.

## Separate cadence finding

C920 A delivered approximately 24 fps in its first indicator observation and
approximately 30 fps in its repeat under the same requested/negotiated profile.
This confirms variable behavior and does not resolve P0.2B cadence acceptance.
The indicator result passes independently because it corresponded correctly to
actual use in both runs. C920 B sustained approximately 30 fps in both runs.

## Evidence levels

- Protocol and requirements integrated: **Yes**.
- Runtime capture/source-release correlation: **Yes**, on four named runs.
- Hardware-in-the-loop indicator behavior: **Passed**, limited to the two named C920 configurations.
- Generic UVC indicator claim: **No**; applicability is model-specific.
- Timing, cadence, soak, field, regulatory, or clinical qualification: **No**.

