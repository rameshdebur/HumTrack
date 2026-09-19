# P0.2F Physical USB Port-Swap Report

**Report ID:** HC-P0-VR-002F  
**Date:** 2026-08-31  
**Disposition:** Corruption follows USB path 2, not the physical camera  
**Tags:** P0.2F | MULTI-UVC | PORT-SWAP | USB-TOPOLOGY | H264 | MEDIA-INTEGRITY | HARDWARE

## Identity remap

Before the swap, physical C920 A/serial `0E1A0C0F` used downstream path
`USB(2)`, and physical C920 B/serial `0352323F` used `USB(3)`. After the operator
swapped the two plugs:

| Physical camera | Parent serial | Stable Windows interface suffix | New path |
|---|---|---|---|
| A | `0E1A0C0F` | `6&DBA5B52&2&0000` | `USB(3)` |
| B | `0352323F` | `7&2C44E5B7&0&0000` | `USB(2)` |

Both paths are currently below the same observed host-controller/root path:
`PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(1)`.

## Concurrent results after swap

| Run | Physical camera | USB path | Run ID | Samples | Cadence | Full decode |
|---|---|---|---|---:|---:|---|
| 1 | A | USB(3) | `CF356BDA-1A08-43B2-80E4-2EBD54071294` | 902 | 30.0012 fps | PASS, zero errors |
| 1 | B | USB(2) | `1161DFC1-3E05-4EC0-B827-BDBFCD9D041E` | 902 | Not accepted | FAIL, H.264 corruption |
| 2 | A | USB(3) | `D4F9112E-AFB5-448F-95E6-653CEB80BF25` | 902 | 30.0014 fps | PASS, zero errors |
| 2 | B | USB(2) | `E658671F-0F77-43F6-B9AA-5C9E447FBD56` | 902 | Not accepted | FAIL, H.264 corruption |

Before the swap, C920 A was corrupt on path 2 and C920 B was clean on path 3 in
three concurrent tests, including reversed startup order. After the swap, C920
B became corrupt on path 2 and C920 A became clean on path 3 in both repeats.

## Conclusion

The supported cause is connection-path/concurrent-load behavior, not a defect
that follows either physical C920 body or its fixed cable. The evidence does not
yet distinguish the physical port, downstream hub path, shared-controller
bandwidth/allocation, electrical integrity, or driver interaction. Both cameras
remain individually usable subject to their separate single-camera evidence.
The current two-port combination is rejected for concurrent scientific capture.

## Next test

Move the camera currently on `USB(2)` to another physical port, preferably under
a different host controller/root hub, retain the other camera on `USB(3)`,
remap identity/topology, and require two clean concurrent full-decode runs. Do
not change driver or profile simultaneously, so causal evidence remains clear.

