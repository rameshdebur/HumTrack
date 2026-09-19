# P0.2G–H Root-Port and Exposure Diagnostic Report

**Report ID:** HC-P0-VR-002GH  
**Date:** 2026-08-31  
**Disposition:** Conditional short-run pass after USB-path and exposure reconciliation  
**Tags:** P0.2G | P0.2H | MULTI-UVC | USB-TOPOLOGY | EXPOSURE | CADENCE | H264 | MEDIA-INTEGRITY | HARDWARE

## Tested identities and topology

| Camera | Parent serial | Exact Windows interface suffix | Observed location path |
|---|---|---|---|
| C920 A | `0E1A0C0F` | `6&DBA5B52&2&0000` | `PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(1)#USB(3)` |
| C920 B | `0352323F` | `7&2C44E5B7&0&0000` | `PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(3)` |

Camera B was moved from the previously corrupt downstream path to the direct
`USBROOT(0)#USB(3)` branch. Both cameras still enumerate below Intel controller
`PCI(1400)`; this was an alternate root-port branch, not a different host
controller. Both used exact native H.264 1920x1080 at 30/1, profile index 560.

## P0.2G automatic-exposure observations

| Run | Camera/mode | Samples | Measured result | Media integrity |
|---|---|---:|---:|---|
| Concurrent | A, automatic controls | 902 | approximately 30 fps | Full decode clean |
| Concurrent | B, automatic exposure | 472 | approximately 15.68 fps | Full decode clean; cadence failed |
| Single | B, automatic exposure | 452 | approximately 15.02 fps | Full decode clean; cadence failed |
| Single, well lit | B, automatic exposure | 865 | approximately 28.8 fps | Failed verifier; non-monotonic DTS reported |

The moved path did not reproduce the prior H.264 corruption. Camera B's similar
low cadence alone shows that concurrency was not required for this cadence
failure. Lighting changed the outcome, supporting exposure/scene dependence,
but the well-lit run still did not meet the acceptance gate.

## P0.2H controlled-exposure observations

The exact-device control probe reported Camera B exposure range `-11` to `-2`,
default `-5`, and automatic mode enabled. With user authorization, only Camera
B was temporarily set to manual exposure `-5`.

| Run | Camera | Run ID | Samples | Average fps | Timestamp regressions | Full decode |
|---|---|---|---:|---:|---:|---|
| Single | B | `92BCF44A-F177-46FB-8E73-9BA7F3856C73` | 902 | 30.0014 | 0 | PASS, zero errors |
| Concurrent 1 | A | `D29C330D-AA28-4475-A883-295D3B6F678C` | 901 | 30.0000 | 0 | PASS, zero errors |
| Concurrent 1 | B | `B76FA279-F5B5-4FEC-BF03-8DFA37D0FAD1` | 902 | 30.0015 | 0 | PASS, zero errors |
| Concurrent 2 | A | `C164EA63-88B2-447E-A07D-278C0FEE8A27` | 902 | 30.0014 | 0 | PASS, zero errors |
| Concurrent 2 | B | `B2651FA3-E97F-4524-ACFB-3520162F11F8` | 902 | 30.0013 | 0 | PASS, zero errors |

All five controlled-exposure artifacts passed structural inspection and a
complete error-free FFmpeg decode. The MP4 nominal rate remained 60 fps and is
not accepted as measured cadence; the table uses decoded duration/frame count
and source evidence.

## Restoration and disposition

The test harness restored Camera B to automatic exposure in a `finally` path.
A separate exact-device probe then read initialization `ok`, exposure automatic
`true`, and current value `-5`.

This evidence supports a conditional short-run configuration: the named camera,
driver, profile, USB topology, scene, and exposure policy must be treated as a
unit. It does not qualify all C920 pairs, establish a different-controller
result, or complete soak, recovery, field, regulatory, or production evidence.
The future product must either enforce a protocol-qualified exposure policy or
measure cadence during readiness and reject/reconcile a nonconforming source.

