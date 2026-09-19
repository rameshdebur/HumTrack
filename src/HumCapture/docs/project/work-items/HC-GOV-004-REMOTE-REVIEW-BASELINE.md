# HC-GOV-004 — Remote Review Baseline

**Change ID:** HC-CHG-20260831-002  
**Tags:** GOV.1P | SCM | GITHUB | PULL-REQUEST | REMOTE-READINESS  
**State:** Pull request open; CI and enforced branch protection not established  
**Change owner:** Signed-in Windows account operator, assisted by Codex Orchestrator  
**Repository:** `rameshdebur/HumTrack`  
**Branch:** `codex/humcapture-baseline`  
**Pull request:** https://github.com/rameshdebur/HumTrack/pull/1  
**Created:** 2026-08-31

## Objective and boundary

Publish the isolated HumCapture engineering baseline for remote review without
including four pre-existing local HumTrack commits or modifying files outside
`src/HumCapture`.

This record does not authorize merge, application implementation, a controlled
release, hardware qualification, clinical use, or regulatory claims.

## Remote baseline

- Remote base branch: `master`.
- Refreshed remote base:
  `67c47f4ba5c29c46c3bdbcfc72133a0492a8ac12`.
- Initial isolated HumCapture baseline:
  `fc3dbd02aa6b73e7ca42bb084229c627b859d006`.
- Baseline closure record before this remote-status update:
  `bd5a2f39ab8bac8fed7e8f1a914bc16a99b826ba`.
- Pull-request head after this record is pushed: the commit containing the
  final accepted version of this record.
- Pull-request state at inspection: `OPEN`, non-draft, mergeable.
- Changed baseline paths: 142, all under `src/HumCapture`.

## Verification carried into review

- Phase 0 evidence-contract tests: 55/55 passed.
- Evidence-control tests: 12/12 passed.
- SBOM tests: 4/4 passed.
- Managed probe Release build and 7/7 self-tests passed.
- Native Media Foundation Release x64 build and 8/8 self-tests passed.
- Project CycloneDX 1.7 validation and retained SHA-256 passed.
- Fresh isolated checkout reproduced the tests, builds, self-tests, SBOM hash,
  and synthetic-fixture hashes.
- The fresh native build emitted one environment-specific warning because the
  verification worktree was under the Windows temporary directory; the same
  source built with zero warnings in the normal workspace. No product source
  warning was suppressed.

These are source/build/automated verification levels. They do not replace the
documented hardware, field, independent, regulatory, or clinical evidence
gates.

## Remote-control inspection

At the time the pull request was opened:

| Control | Observed state |
|---|---|
| GitHub Actions workflows | None |
| Pull-request status checks | None |
| `master` branch protection | Not configured |
| Repository rulesets | None |
| Independent reviewer approval | Open |

The pull request creates a review surface but does not by itself satisfy the
protected-review or CI controls in HC-SOP-SW-004.

## Scope escalation required

An effective GitHub Actions workflow must be stored under the repository-root
`.github/workflows/` path, outside the HumCapture write boundary. Protecting
`master` or adding a repository ruleset also affects every HumTrack component.

Those repo-wide changes require explicit authorization naming the external
path/settings and review of their effect on existing HumTrack development. They
must not be simulated by placing a non-functional workflow under
`src/HumCapture`.

## Decision

The isolated branch and pull request are accepted as the remote engineering
review baseline. Merge remains blocked pending review. CI and enforced branch
protection remain open governance controls, not silently waived controls.
