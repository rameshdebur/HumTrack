# HumCapture SBOM Validation Report

**Report ID:** HC-SBOM-VR-001  
**Revision:** 1.4  
**Date:** 2026-09-11  
**Disposition:** `PASS` for engineering SBOM generation and format validation  
**Regulatory/release disposition:** Not a controlled release or conformity claim

## Scope

Validate the current source-manifest/build-context HumCapture SBOM against
project policy HC-GOV-SBOM-001 and the official CycloneDX 1.7 validator.

## Inputs and coverage

- `tools/capability-probes/shared/package-lock.json`
- `tools/evidence-control/package-lock.json`
- Managed probe `.csproj`
- Windows Coordinator repository and self-test `.csproj` plus NuGet lock files
- Native capture and enumerator `.vcxproj` files
- SBOM generator package manifest
- Scoped `.github/workflows/humcapture-ci.yml` workflow and its three immutable
  GitHub Action commit pins
- Exact Chocolatey package for Windows SDK 10.0.19041 build compatibility
- Derived .NET target framework, Windows target/toolset, native import libraries,
  host runtime APIs, generation context and known unknowns

## Generated result

| Field | Result |
|---|---|
| Format | CycloneDX JSON 1.7 |
| Primary component | HumCapture `0.1.0-i0.4b-c3` engineering/unreleased |
| Components | 31 |
| Dependency graph nodes | 32, complete closure including primary component |
| SBOM SHA-256 | `58161eca97eaa69c14ebfb47d66f7f814c63f241f4bca94b43beba35545f0dfd` |
| Generated UTC | `2026-09-11T11:44:30.127Z` |
| Project tests | 6/6 passed, including NuGet/Coordinator coverage, future-manifest drift, floating-action rejection and exact CI package versions |
| Project validator | Passed |
| Deterministic regeneration | Passed for fixed manifests, version and timestamp |

## Official validation

| Field | Result |
|---|---|
| Validator | CycloneDX CLI `0.33.1+b3cfa4b0edc356dad07e0b6e7ab6da0a94af0246` |
| Validator source | Official `CycloneDX/cyclonedx-cli` GitHub release `v0.33.1` |
| Validator asset SHA-256 | `9b360974c41a5d612eb026fb5e3bb1fa28e08865f7a4f915665b8bc94bd3bc31` |
| Published asset digest match | Passed before execution |
| Command contract | JSON input, CycloneDX `v1_7`, fail on errors |
| Result | `BOM validated successfully` |

## Release/evidence binding

The earlier validated 15-component SBOM remains retained in the evidence vault
and referenced by engineering snapshot `HC-ENG-20260831T163246Z-7d17f5afce36`.
The snapshot and P0.2J evidence retain its exact bytes and SHA-256; accepted
evidence is not edited. The 31-component I0.4B-C3 inventory supersedes the C2
engineering inventory while retaining the same dependency graph; C3 changes
repository source behavior but adds no dependency. It is not retroactively
bound to the earlier snapshot and requires a new release/snapshot record for
release binding.

## Limitations and open review

- The current CI-aware SBOM remains engineering/unreleased and is not yet bound
  to a new controlled release/snapshot.
- No binary composition or runtime-loaded-module collection was performed.
- Host Windows/.NET patch versions remain runtime-resolved known unknowns.
- Current NuGet and Node advisory queries report no findings; no VEX, licence
  approval, maintainer/supplier-risk, independent QA, or qualified regulatory
  review is conferred by this pass.
- Future Android dependencies and later Coordinator/UI dependencies remain
  absent because those components do not yet exist.

## Evidence levels

- Source implemented: yes.
- Automated behavior: 6 SBOM tests, 91 contract/evidence-control tests and 45
  Coordinator repository runtime tests pass.
- Official format validation: passed.
- Release integration: engineering snapshot and vault binding passed.
- Binary/runtime composition: not verified.
- Vulnerability query: no current-source findings; VEX/licence/supplier
  approval not performed.
- Regulatory/clinical review: not performed.
