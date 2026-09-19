# HC-GOV-005 — HumCapture CI and Master Protection

**Tags:** GOV.1Q | CI | GITHUB | MASTER-PROTECTION | SBOM | TRACEABILITY

**Engineering disposition:** `PASS` — CI and protection implemented and remotely verified  
**Controlled-release disposition:** `BLOCKED` — independent human review remains open

## Goal

Establish a reproducible, scoped HumCapture status check and use its proven
check identity to protect `master` without importing unrelated HumTrack source
or representing automated checks as independent approval, hardware evidence,
or regulatory conformity.

## Authorized scope and classification

- Change classification: governance/build infrastructure with repository-level
  interface impact.
- Authorized repository-root change: `.github/workflows/humcapture-ci.yml`.
- Authorized remote setting: protection/rules for `master`.
- Product source remains bounded to `src/HumCapture`; no coordinator or other
  HumTrack product source is changed by this work item.
- Primary owner: HumCapture release/verification owner.
- Affected owner: HumTrack repository administrator.
- Verification owner: human operator/release owner; independent review remains
  separate and open.

## CI contract

The stable required-check name is `HumCapture validation`. It runs only for
HumCapture paths or its own workflow and performs:

1. locked Node.js dependency installation;
2. evidence-contract, evidence-control and SBOM policy tests;
3. retained SBOM hash verification and runtime-dependency audit;
4. managed probe Release build and self-tests; and
5. native Media Foundation Release builds and non-hardware self-tests.

The workflow uses a pinned Windows Server 2025/Visual Studio 2026 runner label,
exact Node.js/.NET versions, the exact Windows SDK 10.0.19041 Chocolatey package,
read-only repository permission and commit-pinned GitHub Actions. CI
intentionally performs no camera acquisition and therefore creates no HIL or
field-workflow evidence.

## Acceptance criteria

- [x] Repository workflow is path-scoped to HumCapture and has read-only contents permission.
- [x] External GitHub Actions use immutable commit SHAs.
- [x] CI workflow/actions are included in the CycloneDX SBOM contract.
- [x] Floating Action references fail an automated SBOM test.
- [x] Chocolatey build packages require exact versions in the SBOM contract.
- [x] Pull-request check `HumCapture validation` completes successfully on GitHub.
- [x] `master` requires the proven check and an up-to-date branch.
- [x] Force pushes and deletion of `master` are blocked; administrators are included.
- [x] Remote settings are read back and recorded.
- [ ] Independent human approval is completed before controlled release.

## Solo-maintainer review boundary

GitHub does not count a pull-request author's own approval as independent
approval. The initial protection may therefore enforce the automated check,
current-branch requirement, conversation resolution, linear history, force-push
and deletion restrictions without falsely marking independent review complete.
A second qualified human review remains an explicit controlled-release blocker.

## First remote-run finding

The first GitHub runs passed source checkout, locked dependency installation,
55 evidence-contract tests, 12 evidence-control tests, five then-current SBOM
tests, both dependency audits and the managed build/self-tests. Native build
stopped truthfully with `MSB8036` because the Visual Studio 2026 runner did not
preinstall Windows SDK `10.0.19041.0`. The remediation preserves the project
target and installs exact Chocolatey package
`windows-sdk-10-version-2004-all@10.0.19041.685`; it does not retarget the
native projects or waive native verification.

## Verified remote result

- Verified commit: `8ff67d8b6104b729428f6866de75660f388b09c5`.
- PR run: `https://github.com/rameshdebur/HumTrack/actions/runs/33421222644` — `SUCCESS`.
- Required check: `HumCapture validation`, bound to GitHub Actions app ID `15368`.
- `master` read-back: strict/up-to-date status check enabled; pull request
  required with zero mandatory approvals; stale reviews dismissed; conversation
  resolution and linear history required; administrators included; force pushes
  and branch deletion disabled.
- The zero-approval setting supports a solo-maintainer engineering flow. It does
  not satisfy the independent-review blocker for controlled release.

## Post-governance resumption point

After this work item closes, hardware work resumes at:

`P0.2J | UVC | DEVICE LOSS | DETECTED + FINALIZED | HIL`

The remaining work is same-identity reconnect/rediscovery, followed by a new,
separate post-reconnect capture that finalizes and fully decodes. Completed
device-loss and partial-finalization evidence is not repeated or relabelled.
