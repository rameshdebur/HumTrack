# ADR-0008 — CycloneDX SBOM as a Controlled Release Artifact

**Status:** Accepted  
**Date:** 2026-08-31

## Context

HumCapture combines private Node.js tools, third-party npm packages, a managed
.NET probe, native Windows/Media Foundation probes, SDK import libraries, and
host-provided runtime frameworks. Package audits alone do not provide a durable,
release-specific inventory across these ecosystems. Future Android/coordinator
work will add further dependency surfaces.

The project requires attributable design and verification evidence suitable for
medical-adjacent lifecycle governance while avoiding unsupported CDSCO or
security claims.

## Decision drivers

- Machine-readable direct and transitive component inventory.
- Coverage across npm, .NET, native Windows and future Android components.
- Component identifiers, hashes, licences, relationships and known unknowns.
- Deterministic regeneration and release-record binding.
- Interoperability with security, regulatory and customer tooling.
- A stable current format without adopting an unreleased future specification.

## Considered options

### Human-maintained dependency table

Simple to read but rejected as the authoritative format. It drifts easily,
cannot reliably express transitive relationships, and is difficult to validate.

### SPDX

A mature alternative with strong licence use cases. Not selected for the initial
canonical format because CycloneDX provides a direct fit for dependency graphs,
VEX evolution, build context and medical-device supply-chain tooling. An SPDX
export can be added later without changing the canonical inventory decision.

### CycloneDX 1.7

Selected as the current stable CycloneDX/ECMA-424 generation. It supports the
required component, relationship, hash, licence, property and lifecycle fields.
CycloneDX 2.0 is not selected because it is not yet a stable ratified release at
the decision date.

### Ecosystem-specific SBOMs only

Rejected as the release boundary. They remain useful inputs, but separate npm,
.NET and native inventories do not provide one HumCapture release view.

## Decision

HumCapture will maintain one canonical CycloneDX JSON 1.7 SBOM generated from
all in-scope locked manifests and project files. It will distinguish first-party,
third-party, platform/runtime and build-only components, and explicitly record
known unknowns rather than infer versions.

The generated SBOM is versioned source evidence and a controlled release
artifact. Every release record stores its SHA-256, format/specification version,
generation timestamp and validation status. A controlled release is rejected
without a project-valid and officially CycloneDX-valid SBOM.

SBOM generation does not establish absence of vulnerabilities, licence approval,
binary/runtime completeness, regulatory conformity or clinical suitability.
Those remain separate review records linked to the SBOM.

## Consequences

- Dependency/project-file changes require SBOM regeneration and review.
- The current engineering SBOM is retrospective and cannot turn the P0.2J
  diagnostic into released-build evidence.
- Host-provided Windows and .NET patch resolution remains an explicit known
  unknown until binary/runtime collection is implemented.
- Future Android and coordinator manifests must be added to generator coverage.
- A future move to CycloneDX 2.0 or dual SPDX publication requires a reviewed
  compatibility decision, not an in-place silent format change.
- Vulnerability scanning, VEX, licence approval and supplier monitoring remain
  separate controls.

## Affected components and interfaces

- `tools/sbom/` — deterministic generator and project validator.
- `sbom/humcapture.cdx.json` — canonical current engineering inventory.
- Release record schema 1.1.0 — optional for engineering snapshots, mandatory
  and officially validated for `CONTROLLED_RELEASE`.
- Evidence vault — retains the exact release record and its referenced SBOM hash.

## Related decisions

- ADR-0001 — bounded HumCapture subsystem.
- ADR-0007 — controlled evidence vault and release identity.

## References

- <https://cyclonedx.org/specification/overview/>
- <https://cyclonedx.org/schema/bom-1.7.schema.json>
- <https://www.cisa.gov/sites/default/files/2025-08/2025_CISA_SBOM_Minimum_Elements.pdf>

## Supersedes / Superseded by

None.
