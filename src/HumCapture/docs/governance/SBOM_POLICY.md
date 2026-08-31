# HumCapture Software Bill of Materials Policy

**Policy ID:** HC-GOV-SBOM-001  
**Revision:** 1.0  
**Effective date:** 2026-08-31  
**Status:** Active for Phase 0 engineering and future releases

## Purpose

HumCapture shall maintain a machine-readable software bill of materials (SBOM)
for supply-chain visibility, vulnerability response, licence review, controlled
release evidence, and future regulatory assessment. An SBOM is an inventory;
it is not proof that components are secure, suitable, licensed correctly, or
approved by CDSCO.

## Format and minimum content

- Canonical format: CycloneDX JSON 1.7 (`*.cdx.json`).
- The SBOM shall identify the primary HumCapture component, first-party tools,
  direct and transitive third-party packages, required frameworks/platform
  interfaces, dependency relationships, versions, package URLs where available,
  component hashes where derivable, declared licences, generator, timestamp,
  generation context, coverage, and known unknowns.
- `unknown`, inferred, host-provided, and unversioned values must be labelled
  explicitly; they must not be guessed.
- Development/build dependencies and runtime/distributed dependencies shall be
  distinguishable through scope and properties.

The policy baseline follows the current stable CycloneDX specification and the
current CISA SBOM minimum-elements model. These external baselines must be
reviewed for change before a medically positioned release.

## Generation and maintenance

The SBOM shall be regenerated whenever any of the following changes:

1. A package lock, project file, native linker dependency, target framework,
   toolchain/runtime requirement, CI workflow/action dependency, or vendored
   component.
2. The included HumCapture component set or release version.
3. Dependency resolution, even if the declared version range did not change.
4. A release candidate or released build is created.

Generation is lockfile-first. A manifest without a lock/resolution record is a
known-unknown and blocks a controlled release unless reviewed and waived.
Generated output is deterministic when given the same source manifests,
product version, and timestamp.
Manifest discovery is fail-closed: a newly detected npm, .NET, native, Gradle,
Python, Rust, Go, CocoaPods or equivalent supported manifest must be added to
the generator contract before SBOM generation can pass.
Repository CI actions shall be pinned to immutable commit SHAs. Their source
workflow, commit identities, build-only scope and unresolved licence/package
hash review shall be represented explicitly.

## Verification and release control

- The project validator shall check format/version, unique identifiers,
  dependency closure, required minimum fields, source-manifest coverage,
  component hashes/licence declarations or explicit unknowns, and deterministic
  regeneration.
- An official CycloneDX validator shall validate each release SBOM.
- The release record shall contain SBOM path, SHA-256, CycloneDX version,
  generation timestamp, and validation status.
- `CONTROLLED_RELEASE` requires an attached SBOM that passed both project and
  official format validation. Engineering snapshots may carry a retrospective
  SBOM but must state that limitation.
- The SBOM and its hash are retained with the release evidence. A changed SBOM
  creates a new release/snapshot record; an accepted release record is not edited.

## Vulnerability and licence handling

SBOM production does not replace vulnerability or licence analysis. Each release
gate shall separately run applicable ecosystem audits and review newly added,
removed, or changed components. Vulnerability findings, VEX decisions, accepted
risks, and licence approvals are separate controlled records linked to the SBOM.

## Current known unknowns

- HumCapture has no approved legal producer/supplier identity or released version.
- The current Windows native probes depend on host-provided Media Foundation,
  COM/OLE, and Shell APIs whose runtime patch versions vary with Windows.
- The managed probe targets .NET 8 but resolves the installed patch at runtime.
- No Android application dependency manifest exists yet.
- The current SBOM is source-manifest/build-context coverage, not a binary
  composition or runtime-loaded-module scan.
