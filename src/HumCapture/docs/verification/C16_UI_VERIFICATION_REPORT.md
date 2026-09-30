# C16A-C software-only coordinator UI verification

HC-VR-C16-001 | 2026-09-30 | engineering evidence, not release/clinical approval.

## Scope and outcome

Approved guided Session/Cameras/Capture/Results UI implemented in independent
Avalonia/Fluent code. Synthetic fixture protocols and sources only; one or two
required roles, fixed-rate compatibility, locked identity and separate service
process with SQLite state. Failure/restart/new-attempt and simulated processing
retry retain history. Existing repository inspection uses supported read-only
queries and explicit historical labels. No physical camera was activated.

## Verified

- Release build: zero warnings/errors (C16_BUILD_RESULTS.txt).
- Full runtime suite: 217/217 (C16_RUNTIME_RESULTS.txt), including runtime216's
  21 simulator assertions and runtime217's actual headless screen/service flow.
- Existing executable contracts: 115/115 (C16_CONTRACT_RESULTS.txt).
- SBOM/register: 13/13, project CycloneDX validator passes
  (C16_SBOM_RESULTS.txt); this is not a new official CycloneDX CLI claim.
- Windows apphost executable independently ran all 21 service assertions
  (C16_APPHOST_AND_RENDER_RESULTS.txt). No native window acceptance implied.
- Headless keyboard input/focus, invalid form guard, recording navigation guard,
  close confirmation with keep-open, four-screen normal flow and explicit
  simulation/NOT COMMITTED labels pass. 1120x830 and 760x540 renders retained.
- Launcher PowerShell syntax parsed without errors; native launcher interaction
  remains human-review work.
- NuGet advisory query for Desktop including transitive dependencies reported
  no vulnerable packages using configured NuGet sources on this run. This is
  source advisory coverage, not an independent binary/security assessment.

## Retained evidence

Raw text results above are versioned. Final headless PNGs remain locally under
evidence-vault/c16-ci-ca3686db4b0f43a9a28979bc4a6b7197; paths and SHA-256 hashes
are retained in C16_APPHOST_AND_RENDER_RESULTS.txt. No WORM/backup/certification
claim. The service test creates synthetic repositories only. No decoder needed.

Initial SBOM 0.1.0-i0.4b-c16c contains 56 components plus product, 57 dependency nodes:
SHA-256 9df89789cc12faee95a1808c89befe4e62fa5ba8c44f1f17e12d17f5ad60ded3.
Eighteen new NuGet identities and the first-party desktop are inventoried.
Per-package C16 review dates are recorded; older register profiles were not
silently re-reviewed. MIT declarations and ANGLE's BSD-style licence were
inspected from exact restored packages; native composition/notice bundling and
qualified legal/distribution review remain open.

## Development findings corrected

- Cross-platform Avalonia.Desktop pulled Tmds.DBus.Protocol 0.21.2 and restore
  blocked on GHSA-xrw6-gwf8-vvr9. Selected Windows-specific Avalonia.Win32/Skia;
  no advisory suppression. Linux D-Bus is absent from the retained graph.
- Avalonia.BuildServices assets excluded after inspecting its build-stats target.
- Headless TextChanged notifications needed dispatcher draining before asserting
  disabled state; final actual UI test includes the invalid-form assertion.
- Reviewed rendered layout: grouped/left-aligned fields and shorter path display.
- Added survivor test to prevent source loss from stopping a simulated peer.

## Evidence boundaries and open gates

Source/build/automated software and subprocess integration verified. Native
Windows rendering/input, screen reader, high contrast, DPI/scaling, trained
human usability and HIL are NOT accepted by these tests. Production capture
workers, actual media-package UI verification/commit, Android, clinical,
regulatory and independent human review remain open. Fixture 720p30 profiles
are not production protocol approval or a change to acquisition requirements.
Simulation processing writes state/history only; it never marks real custody,
scientific quality or session completion. UI shows latest bounded history;
older rows are retained. HIL remains explicitly deferred by the user.

All edits remain under src/HumCapture. Desktop build/tests enter the existing
self-test graph; no parent CI file or HumTrack implementation was modified.
Hosted checks must be evaluated separately against the pushed source SHA.

Hosted cbdfabb run36657167077 passed contract/runtime and SBOM checks but failed
the existing Node dependency audit on fast-uri3.1.6. C16_DEPENDENCY_PATCH_REPORT.md
records the narrow 3.1.7 remediation, rerun evidence and superseding SBOM c16c.1.
