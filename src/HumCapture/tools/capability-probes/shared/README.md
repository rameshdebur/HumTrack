# Phase 0 Evidence Tool

This is isolated, non-production verification infrastructure for HumCapture
Phase 0. It validates capability-probe evidence packages and generates their
artifact hash indexes. It is not a production capture-package validator.

## Requirements

- Node.js 20 or newer.
- Dependencies installed with `npm.cmd install` on Windows.

## Commands

From this directory:

```powershell
npm.cmd test
npm.cmd run validate -- fixtures/valid/android-run
npm.cmd run validate:campaign -- fixtures/valid/campaign-index.json
npm.cmd run hash -- <evidence-directory>
```

`hash` scans every regular evidence file except `run-manifest.json` and
`hashes.sha256`, rejects symbolic links and unsafe paths, writes the manifest's
artifact inventory, and writes a deterministic SHA-256 index. The run manifest
must already contain the non-artifact run metadata.

`validate` checks JSON Schema, CSV headers and primitive values, safe paths,
artifact completeness, byte lengths, SHA-256 values, and the hash index. It does
not inspect media decodability; later probe increments must add that evidence.

## Contract boundary

Evidence format `1.0.0` is exact. A newer or older version is rejected until the
validator explicitly supports it. Reported, requested, negotiated, and measured
values remain separate. Unknown provenance is represented explicitly rather than
inferred.

The fixtures contain synthetic identifiers and measurements only.
