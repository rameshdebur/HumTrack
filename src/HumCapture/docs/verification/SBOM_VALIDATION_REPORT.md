# HumCapture SBOM Validation Report

**Report ID:** HC-SBOM-VR-001  
**Revision:** 1.1  
**Date:** 2026-08-31  
**Disposition:** `PASS` for engineering SBOM generation and format validation  
**Regulatory/release disposition:** Not a controlled release or conformity claim

## Scope

Validate the current source-manifest/build-context HumCapture SBOM against
project policy HC-GOV-SBOM-001 and the official CycloneDX 1.7 validator.

## Inputs and coverage

- `tools/capability-probes/shared/package-lock.json`
- `tools/evidence-control/package-lock.json`
- Managed probe `.csproj`
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
| Primary component | HumCapture `0.1.0-p0.2j` engineering/unreleased |
| Components | 20 |
| Dependency graph nodes | 21, complete closure including primary component |
| SBOM SHA-256 | `605734f0667a18b76f446c2a86a8aeb3c2d6beecddfcfef8907e3d57242fee57` |
| Generated UTC | `2026-08-31T17:45:00.000Z` |
| Project tests | 6/6 passed, including future-manifest drift, floating-action rejection and exact CI package versions |
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
evidence is not edited. The current 20-component CI-aware SBOM is a new
source-control engineering inventory. It is not retroactively bound to that
snapshot and requires a new release/snapshot record for release binding.

## Limitations and open review

- The current CI-aware SBOM remains engineering/unreleased and is not yet bound
  to a new controlled release/snapshot.
- No binary composition or runtime-loaded-module collection was performed.
- Host Windows/.NET patch versions remain runtime-resolved known unknowns.
- No vulnerability/VEX, licence approval, maintainer/supplier-risk, independent
  QA, or qualified regulatory review is conferred by this pass.
- Future Android/coordinator dependencies are absent because those applications
  do not yet exist.

## Evidence levels

- Source implemented: yes.
- Automated behavior: 6 SBOM tests and 12 evidence-control tests pass.
- Official format validation: passed.
- Release integration: engineering snapshot and vault binding passed.
- Binary/runtime composition: not verified.
- Vulnerability/licence/supplier approval: not performed.
- Regulatory/clinical review: not performed.
