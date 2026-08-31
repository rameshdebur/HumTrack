# HC-GOV-002 — Maintain HumCapture SBOM

**Tags:** GOV.1 | SUPPLY-CHAIN | SBOM | CYCLONEDX | RELEASE | TRACEABILITY

## Goal

Generate, validate, retain and release-bind a complete machine-readable inventory
of current HumCapture software components without converting inventory into an
unsupported security, licence, regulatory or clinical claim.

## Owners and reviews

- Primary owner: release owner / verification infrastructure.
- Affected owners: component engineers, security reviewer, QA, regulatory/risk reviewer.
- Architecture review: completed through accepted ADR-0008.
- Independent security/licence/QA and qualified regulatory review: open.

## Acceptance criteria

- [x] CycloneDX JSON 1.7 selected and documented against current official sources.
- [x] Both npm lockfiles, managed project and both native projects are inventoried.
- [x] Direct/transitive npm packages, frameworks, runtime APIs and build SDK are represented.
- [x] Identifiers, hashes, declared licences, dependency graph, generator,
      timestamp, context, coverage and known unknowns are recorded.
- [x] Regeneration is deterministic for fixed inputs/version/timestamp.
- [x] Project completeness/closure validator passes.
- [x] Official CycloneDX 1.7 validator result retained.
- [x] Current engineering snapshot regenerated with the validated SBOM hash.
- [ ] Independent vulnerability, licence and supplier-risk review completed.
- [ ] Future Android/coordinator manifests added when those components exist.

## Evidence boundary

The current output is a retrospective source-manifest/build-context SBOM. It is
not binary composition, a runtime-loaded-module inventory, VEX, licence approval,
vulnerability clearance, CDSCO evidence sufficiency, or a controlled release.

## Current result

CycloneDX 1.7 SBOM SHA-256
`2f34a8112c2c2ea274c4be7fd4d15443368feb4baa06c3e364dd7fae736f94e5`
contains 15 components and a complete 16-node dependency graph. Four project
tests and official CycloneDX CLI 0.33.1 validation pass. It is retained and
bound to engineering snapshot `HC-ENG-20260831T163246Z-7d17f5afce36`.
