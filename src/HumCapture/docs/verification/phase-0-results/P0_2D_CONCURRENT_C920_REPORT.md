# P0.2D Concurrent Logitech C920 Report

**Report ID:** HC-P0-VR-002D  
**Date:** 2026-08-31  
**Disposition:** Concurrent device operation demonstrated; media-integrity acceptance failed  
**Tags:** P0.2D | MULTI-UVC | LOGITECH-C920 | USB | H264 | MEDIA-INTEGRITY | HARDWARE

## Online claim assessment

The claim that two or more Logitech C920 cameras cannot function simultaneously
is too broad. Logitech publishes specific guidance for simultaneous C920 use,
including known driver conflicts, use of the generic Windows driver, and direct
motherboard USB connections rather than a hub:

- [Logitech: Resolve issues when multiple C920s webcams are used simultaneously](https://support.logi.com/hc/en-us/articles/360023371773-Resolve-issues-when-multiple-C920s-webcams-are-used-simultaneously)

Microsoft documents that UVC streams reserve USB bandwidth and that available
bandwidth depends on the controller, hubs, devices, and selected alternate
settings. Therefore simultaneous support is configuration-specific:

- [Microsoft: USB bandwidth allocation](https://learn.microsoft.com/en-us/windows-hardware/drivers/usbcon/usb-bandwidth-allocation)
- [Microsoft: USB device layout](https://learn.microsoft.com/en-us/windows-hardware/drivers/usbcon/usb-device-layout)

## Tested configuration

- Two Logitech C920 units with exact identities previously recorded as C920 A
  and C920 B.
- Driver provider `Logitech`, version `1.4.40.0`, INF `oem133.inf`, service
  `usbvideo` on both video interfaces.
- Observed USB paths `USB(1)#USB(2)` and `USB(1)#USB(3)`; the exact controller
  bandwidth relationship remains to be qualified.
- Two independent native Media Foundation processes.
- Exact native type index 560: H.264 1920×1080 at negotiated 30/1.
- Thirty-second concurrent acquisition per run; separate MP4 and timing evidence.

## Results

| Concurrent run | Camera | Run ID | Samples | Measured cadence | Decode result |
|---|---|---|---:|---:|---|
| 1 | A | `B95E2C87-10A4-4C1F-94AE-5DAF129B4ECE` | 902 | 30.0014 fps | FAIL: H.264 macroblock/bitstream errors |
| 1 | B | `E87EA8F6-D017-48B7-AFBC-78B409BF0D32` | 902 | 30.0014 fps | PASS: zero decode errors |
| 2 | A | `57885AAE-725B-4F68-9B3A-028AC119FD3C` | 893 | Not accepted | FAIL: H.264 macroblock/bitstream errors |
| 2 | B | `5B577958-4A41-4217-9AE2-97BB61D4C77B` | 902 | 30.0013 fps | PASS: zero decode errors |

Both cameras opened concurrently, their indicators illuminated concurrently,
and both capture processes finalized and released without API errors. Frame
counts alone initially appeared successful. Full decoding showed repeatable
corruption only in C920 A during concurrent capture. Known clean single-camera
runs from both devices establish that the current failure is associated with
the concurrent configuration, though this evidence does not yet distinguish
driver, USB topology/bandwidth, startup ordering, native profile, or a specific
camera interaction as the cause.

## Verification correction

`Verify-ShortCapture.ps1` now treats any `ffprobe` or full `ffmpeg` decode error
as a failure. The corrupt A artifact fails and the clean B artifact passes. This
prevents complete-looking sample/frame counts from masking damaged video.

## Decision

- **Simultaneous enumeration/opening:** supported on this configuration.
- **Simultaneous finalized artifacts:** supported mechanically.
- **Simultaneous scientifically valid H.264 1080p30 capture:** **failed/not
  approved** on this configuration.
- **Universal C920 prohibition:** not justified by official documentation or
  this test.
- **Product rule:** require concurrent qualification only when a selected
  protocol requires multiple UVC sources; reject or reconcile the complete
  named camera/driver/USB/profile combination when any stream fails integrity.

## Next reconciliation tests

1. Map both ports to physical host controllers and test separate controllers.
2. Compare the Microsoft inbox/generic UVC driver against the current Logitech
   provider configuration using a controlled, reversible driver test.
3. Reverse camera startup order to determine whether failure follows camera A
   or the first-started stream. **Completed in P0.2E:** corruption followed
   C920 A when it started second; the first-started C920 B remained clean.
4. Compare duplicate native H.264 profile index 567 and a lower-bandwidth
   protocol-approved profile without silent substitution.
5. Require two clean concurrent runs and then a soak before approval.

Raw artifacts remain outside source control under the signed-in account's
temporary directory. This is diagnostic hardware evidence, not qualification.

## Follow-up evidence

P0.2E reversed the startup order with a three-second offset. C920 A remained
corrupt and C920 B remained clean, so startup order is not the supported cause.
See `P0_2E_REVERSE_START_ORDER_REPORT.md`.

P0.2F then physically exchanged the two USB connections. Corruption moved from
physical C920 A to physical C920 B and remained on downstream path `USB(2)` in
two repeats; the camera moved to `USB(3)` was clean. See
`P0_2F_PHYSICAL_PORT_SWAP_REPORT.md`.
