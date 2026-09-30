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
- [x] Scoped repository CI workflow and immutable GitHub Action commit pins are inventoried.
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

Current source-control CycloneDX 1.7 SBOM SHA-256
`605734f0667a18b76f446c2a86a8aeb3c2d6beecddfcfef8907e3d57242fee57`
contains 20 components and a complete 21-node dependency graph. Six project
tests pass, including rejection of floating GitHub Action tags and unversioned
Chocolatey build packages. Official
CycloneDX CLI 0.33.1 validation is recorded in `HC-SBOM-VR-001`.

The earlier 15-component SBOM SHA-256
`2f34a8112c2c2ea274c4be7fd4d15443368feb4baa06c3e364dd7fae736f94e5`
remains immutable in and bound to engineering snapshot
`HC-ENG-20260831T163246Z-7d17f5afce36`. The updated CI-aware SBOM does not
retroactively alter that snapshot and requires a future release/snapshot record
before it can be described as release-bound.
