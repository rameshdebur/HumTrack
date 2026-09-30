# P0.2E Reverse Start-Order Report

**Report ID:** HC-P0-VR-002E  
**Date:** 2026-08-31  
**Disposition:** Start-order hypothesis not supported; failure followed C920 A  
**Tags:** P0.2E | MULTI-UVC | START-ORDER | H264 | MEDIA-INTEGRITY | HARDWARE

C920 B was opened first. C920 A was opened three seconds later, and the two
exact native H.264 1920×1080 at 30/1 captures overlapped for approximately 27
seconds. Both processes finalized 902-sample artifacts and released normally.

| Camera | Start order | Run ID | Measured cadence | Complete decode |
|---|---|---|---:|---|
| B | First | `6D20EF32-3BB7-464B-8256-E07D71705EE4` | 30.0015 fps | PASS, zero errors |
| A | Second | `B1B3EC1D-C925-4B46-8BD1-3E1E4E6EB785` | Not accepted | FAIL, H.264 bitstream/macroblock errors |

The corruption therefore did not follow the first-started stream. Across three
concurrent tests it has followed C920 A while C920 B remained clean. This
narrows, but does not distinguish, a C920 A hardware/fixed-cable issue, its
current USB port/controller path, or a device-specific driver/profile
interaction. The next lowest-risk discriminator is to swap the two physical USB
connections and repeat before altering drivers.

