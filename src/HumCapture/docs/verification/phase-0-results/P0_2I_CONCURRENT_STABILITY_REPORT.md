# P0.2I Conditional Dual-C920 Stability Report

**Report ID:** HC-P0-VR-002I  
**Date:** 2026-08-31  
**Disposition:** Conditional 10-minute stability pass  
**Tags:** P0.2I | MULTI-UVC | STABILITY | USB-TOPOLOGY | EXPOSURE | CADENCE | H264 | MEDIA-INTEGRITY | HARDWARE

## Scope decision

A 30-minute run was initially prepared because that is the Phase 0 normative
qualification soak duration. The operator questioned whether it was
proportionate for the MVP. The attempt was stopped after approximately 45
seconds, all capture processes were closed, and Camera B automatic exposure was
independently confirmed before restarting.

The accepted test was reduced to 10 minutes. This is enough to strengthen the
conditional engineering stability evidence, but it is not a qualification soak.
The cameras also lack the normative HC-P0-UVC-001 1080p60 profile.

## Fixed configuration

| Camera | Exact interface suffix | Observed USB path | Exposure |
|---|---|---|---|
| C920 A | `6&DBA5B52&2&0000` | `PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(1)#USB(3)` | automatic |
| C920 B | `7&2C44E5B7&0&0000` | `PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(3)` | temporary manual `-5` |

Both cameras used native Media Foundation profile 560, H.264 1920x1080 at
requested 30/1. The scene contained no subject data. The laptop remained on AC
power for all 14 host telemetry samples; minimum observed free space was 63.05
GiB.

## Results

| Camera | Run ID | Duration | Samples/decoded | Average fps | Timestamp regressions | Decode errors |
|---|---|---:|---:|---:|---:|---:|
| A | `9D4BA630-FB05-4388-B076-F44983A4980E` | 600.063367 s | 18,002/18,002 | 30.000163 | 0 | 0 |
| B | `7993261C-ED93-494C-A1B7-BC5FD0394FB5` | 600.063367 s | 18,002/18,002 | 30.000165 | 0 | 0 |

Both sinks finalized with exit code 0. Each complete media file passed structural
inspection and full error-free decode. The container nominal rate remained 60
fps and is not used as achieved-cadence evidence.

## Restoration and conclusion

The guaranteed cleanup restored Camera B to automatic exposure. An independent
exact-device read reported initialization `ok`, automatic exposure `true`, value
`-5`, and zero active capture processes.

The P0.2H exact configuration now has one clean single-camera short run, two
clean concurrent 30-second runs, and one clean concurrent 10-minute run. This
supports a conditional MVP engineering configuration. It does not establish
normative 1080p60 support, a 30-minute qualification soak, recovery behavior,
field readiness, or general Logitech C920 compatibility.

