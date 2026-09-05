# HumCapture Evidence Control Tool

This isolated Phase 0 tool creates honest engineering/release identities,
imports raw test artifacts into a controlled local vault, and verifies them.
It does not provide electronic signatures, backup, WORM storage, regulatory
approval, or coordinator application persistence.

The default vault selected by project policy is `HumCapture Evidence Vault`
directly under the local Windows profile root. Do not place it in a redirected
Documents/Desktop folder or cloud-synchronized location without approval.

## Requirements

- Node.js 20 or newer.
- Install the locked dependencies with `npm.cmd ci` before use.

## Commands

```powershell
node src/cli.js snapshot --repo <HumCapture> --output <release.json> --version 0.1.0-p0 --binary <executed.exe> --source-artifact <capture-source.cpp> --sbom <humcapture.cdx.json> --sbom-officially-validated true --sbom-validator <tool-and-version>
node src/cli.js import --source <raw-run> --vault <vault> --source-run-id <id> --test-case-id <id> --release-record <release.json> --disposition INCONCLUSIVE --data-classification SYNTHETIC_NON_SUBJECT --limitation <text>
node src/cli.js verify --vault <vault>
```

Requesting `--classification CONTROLLED_RELEASE` fails unless the HumCapture
subtree is tracked and clean. The current untracked subsystem can create only an
`ENGINEERING_SNAPSHOT` suitable for attributable engineering evidence.

The JSON Schemas under `schemas/` are versioned executable persistence contracts.
Release creation, import, and verification validate records against them.

## Session-control conformance harness

This package also hosts the non-production conformance oracle used by the
existing scoped CI job for `HC-IF-CTRL-001`. The interface artifacts themselves
remain under `docs/interfaces/`; synthetic valid/invalid fixtures are under
`fixtures/control/`, and HC-CTRL-TEST-001–006 exercise JSON Schema compilation,
AsyncAPI reference closure, lifecycle/completion/handoff behavior, every
forbidden transition in the accepted matrix, fail-closed invalid cases, and
fixed/flexible source-count rules.

This placement reuses the already locked Ajv validation surface and does not
couple production coordinator or capture code to the evidence-vault runtime.
