# P0.1 Evidence-Contract Verification Report

**Report ID:** HC-P0-VR-001  
**Date:** 2026-08-28  
**Disposition:** Gate passed after six independent review passes across three authorized acceptance cycles; P0.2 is unblocked but not started

## Scope

This report covers only the isolated Phase 0 shared evidence tooling in
`tools/capability-probes/shared`. It does not cover Android, UVC, network,
hardware-in-the-loop, sustained capture, field workflow, clinical, or regulatory
behavior.

## Environment

- OS: Microsoft Windows 11 Home Single Language, version 10.0.26200, build 26200.
- Node.js: v22.12.0.
- npm: 10.9.0.
- Repository HEAD observed during verification: `9743fa22ddbd3c2256a423a8b9bdbd5762cfafae`.
- Evidence tool: `@humcapture/phase0-evidence` 0.1.0.
- JSON Schema validator: Ajv 8.20.0 with ajv-formats 3.0.1; dependency versions are locked.

HumCapture was untracked as a directory in the enclosing worktree during this
verification, so the recorded enclosing HEAD does not by itself identify these
new files.

## Implemented evidence surface

- JSON Schema 2020-12 definitions for run manifest, capability report, campaign index, and common types.
- A versioned procedure registry that binds each procedure ID to its platform,
  camera stack, duration, exact stream purposes/profiles, measurements, and
  hardware-qualification status.
- Versioned CSV dictionaries for frames, sensors, thermal/power/storage, and network observations.
- Exact decimal-string preservation for nanosecond values that may exceed IEEE-754 safe integers.
- Synthetic valid capability/run/campaign fixtures.
- Stored invalid examples plus automated invalid-package construction.
- Local CLI for package validation, campaign validation, and deterministic SHA-256 generation.
- Streaming file hashing suitable for large media without reading an entire file into memory.
- Rejection of unsupported versions, unsafe paths, symbolic links, duplicate/missing/unlisted artifacts, byte-length mismatch, hash mismatch, malformed CSV values, stale campaign hashes, and cross-document identity mismatch.

## Automated cases

| Test ID | Behavior | Result |
|---|---|---|
| HC-P0-T001 | Valid synthetic evidence package | Pass |
| HC-P0-T002 | Valid campaign index and referenced manifest | Pass |
| HC-P0-T003 | Stale campaign manifest hash rejected | Pass |
| HC-P0-T004 | Missing required manifest fields rejected | Pass |
| HC-P0-T005 | Newer unsupported evidence version rejected | Pass |
| HC-P0-T006 | Parent-path traversal rejected before file access | Pass |
| HC-P0-T007 | Artifact byte/hash mismatch rejected | Pass |
| HC-P0-T008 | Missing listed artifact rejected | Pass |
| HC-P0-T009 | Unlisted artifact rejected | Pass |
| HC-P0-T010 | Invalid CSV primitive rejected after valid rehash | Pass |
| HC-P0-T011 | Hash generator produces self-consistent package | Pass |

Forty-four additional positive, negative, and cross-document tests now cover
disposition semantics, per-stream profiles and measurements, qualification
duration and coverage, network/compatibility/UVC lifecycle scenarios, capability
vocabulary constraints, exact procedure profiles, one-to-one stream/profile
binding, required evidence, CSV and timestamp enforcement, campaign integrity,
and filesystem-link safety.

## Commands and results

```powershell
node --check src/evidence.js
node --check src/cli.js
npm.cmd test
npm.cmd run validate -- fixtures/valid/android-run
npm.cmd run validate:campaign -- fixtures/valid/campaign-index.json
npm.cmd audit --omit=dev
```

Results:

- JavaScript syntax checks passed.
- 55 of 55 automated tests passed.
- Synthetic run validated with six hashed artifacts and `INCONCLUSIVE` disposition.
- Synthetic campaign and referenced run-manifest hash validated.
- npm reported zero known dependency vulnerabilities at verification time.

## Evidence-level statement

- Source implemented: **Yes**.
- Build/static checks passed: **Yes**, for JavaScript syntax and JSON Schema compilation.
- Automated behavior verified: **Yes**, for the current 55-test suite.
- Runtime cross-process integration verified: **No**.
- Hardware-in-the-loop verified: **No**.
- Sustained performance verified: **No**.
- Field workflow verified: **No**.
- Regulatory or clinical review completed: **No**.

## Independent review result

Six independent review passes were completed across three explicitly authorized
acceptance cycles. All discovered source-contract findings are remediated:
package/campaign integrity; measurement coverage and ordering; procedure-family
disposition semantics; normative platform and exact master/preview profile
bindings; network and compatibility subtests; UVC format/control/topology
vocabulary; exact UVC disconnect/reconnect lifecycle evidence; and exact
one-to-one equality between manifest streams and capability profiles.

The final review independently exercised duplicate bindings, count-equal
missing/extra substitutions, incompatible profile statuses, and capture-profile
injection into every network/compatibility disposition. All adversarial packages
were rejected, and the fresh 55-test baseline and both validation CLIs passed.

Gate P0.1 is therefore **passed/closed** for the Phase 0 source evidence
contract. P0.2 Windows/UVC probe implementation is unblocked but has not
started. This result does not qualify hardware and does not constitute runtime,
field, clinical, regulatory, or release evidence.
