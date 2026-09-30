# HC-P0-2I — Conditional Dual-C920 Stability Diagnostic

**Tags:** P0.2I | MULTI-UVC | STABILITY | USB-TOPOLOGY | EXPOSURE | CADENCE | MEDIA-INTEGRITY | HARDWARE

## Goal

Determine whether the exact P0.2H conditional configuration remains stable for
10 continuous minutes rather than only two 30-second runs. This duration is an
MVP engineering diagnostic, not the normative qualification soak.

## Fixed configuration

- Camera A: exact interface suffix `6&DBA5B52&2&0000`, observed path
  `PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(1)#USB(3)`, automatic exposure.
- Camera B: exact interface suffix `7&2C44E5B7&0&0000`, observed path
  `PCIROOT(0)#PCI(1400)#USBROOT(0)#USB(3)`, temporary manual exposure `-5`.
- Both: native Media Foundation profile 560, H.264 1920x1080 at requested 30/1.
- Synthetic/non-subject scene; no other intentional USB or camera load.

## Acceptance criteria

- [x] Preflight identity, topology, control state, available storage, and power
      state are recorded.
- [x] Both cameras run concurrently for at least 10 continuous minutes.
- [x] Both sinks finalize independently.
- [x] Every recorded frame fully decodes with zero media errors.
- [x] Source and host-arrival timestamp sequences do not regress.
- [x] Measured average cadence for each stream remains within the diagnostic
      29–31 fps band; discontinuities and gaps are reported separately.
- [x] Camera B is restored to automatic exposure in guaranteed cleanup and the
      restored state is independently verified.

## Status

done — after the scope was reduced, both cameras completed and fully decoded
18,002 frames over 600.063367 seconds at approximately 30.0002 fps with no
timestamp regressions; Camera B automatic exposure was independently confirmed
after cleanup

## Evidence boundary

Passing this diagnostic would strengthen only the conditional 1080p30
configuration. It cannot pass normative HC-P0-UVC-001 because it is shorter than
30 minutes and these C920s do not report the required 1080p60 profile. It does
not replace recovery testing or the complete P0.1 evidence package.
