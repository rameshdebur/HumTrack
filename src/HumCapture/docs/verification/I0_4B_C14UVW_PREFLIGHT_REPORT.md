# C14U-W verification-record preflight

**Record:** HC-VR-I0-4B-C14UVW-PREFLIGHT-001
**Date:** 2026-09-23
**Inspected source:** b9f74ea9dc6bd3a82a65fa3540657d37a1484555
**Level:** source inspection and synthetic fixture diagnostic, not HIL

## Objective and method

Determine whether existing internal evidence can truthfully supply every
required package-verification check. Inspect XFR 1.2, the manifest v1 schema,
PackageInputManifest and PackageInputEvidence. Decode the committed
`tools/evidence-control/fixtures/timing/v1/package-input-vectors.json`
`android-exact` IMU bytes using `decodeImuStream` from
`tools/evidence-control/src/timing-contract-conformance.js`.

## Direct observation

The IMU artifact declares clock `20000000-0000-4000-8000-000000000002`,
frequency 1000000000, first ticks `1000000000`, last ticks `1066666666`,
record count `3`, discontinuity count `0`. Decoded records, in file order:

| Sensor lane | Sequence | Native ticks | Segment |
|---|---|---|---|
| 1 | 0 | 1000000000 | 0 |
| 1 | 1 | 1010000000 | 0 |
| 2 | 0 | 1001000000 | 0 |

The declared last tick equals neither the final record nor the maximum observed
native tick. This is a synthetic package-summary defect, not evidence of a
physical Android-camera failure or corrupted sensor bytes. The generator assigns
the same coverage constants to frame, master and IMU artifacts.

Source inspection confirms the current coverage gate checks schema/numeric
bounds, not agreement with sample data. No overall VERIFIED record is currently
produced by PackageInputEvidence, so this finding is not evidence of a false
production verification record already being emitted.

## Existing regression observation

Executed the existing Release binary (no fresh build) using:

```powershell
dotnet run --project apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest -c Release --no-build -- --package-input
```

Output: `SUMMARY total=21 passed=21 failed=0`, groups 173-193. This is a
baseline observation only; those tests do not establish timing-summary binding.
No changed implementation was tested, and no full-suite, fresh build, actual
decoder, hardware, field or regulatory acceptance is claimed for this preflight.

## Disposition and next action

Hold verification-record production at the design gate. Proposed ADR-0037 asks
for versioned, sample-bound coverage semantics and explicit legacy NOT_ASSESSED.
Do not rewrite historical package bytes, silently correct retained evidence,
or treat missing verification as scientific rejection. After approval, add
negative regression coverage before correcting the generated synthetic fixture.
