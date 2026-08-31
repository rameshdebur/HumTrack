# HC-GOV-005 — HumCapture CI and Master Protection

**Tags:** GOV.1Q | CI | GITHUB | MASTER-PROTECTION | SBOM | TRACEABILITY

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
exact Node.js/.NET versions, read-only repository permission and commit-pinned
GitHub Actions. CI intentionally performs no camera acquisition and therefore
creates no HIL or field-workflow evidence.

## Acceptance criteria

- [x] Repository workflow is path-scoped to HumCapture and has read-only contents permission.
- [x] External GitHub Actions use immutable commit SHAs.
- [x] CI workflow/actions are included in the CycloneDX SBOM contract.
- [x] Floating Action references fail an automated SBOM test.
- [ ] Pull-request check `HumCapture validation` completes successfully on GitHub.
- [ ] `master` requires the proven check and an up-to-date branch.
- [ ] Force pushes and deletion of `master` are blocked; administrators are included.
- [ ] Remote settings are read back and recorded.
- [ ] Independent human approval is completed before controlled release.

## Solo-maintainer review boundary

GitHub does not count a pull-request author's own approval as independent
approval. The initial protection may therefore enforce the automated check,
current-branch requirement, conversation resolution, linear history, force-push
and deletion restrictions without falsely marking independent review complete.
A second qualified human review remains an explicit controlled-release blocker.

## Post-governance resumption point

After this work item closes, hardware work resumes at:

`P0.2J | UVC | DEVICE LOSS | DETECTED + FINALIZED | HIL`

The remaining work is same-identity reconnect/rediscovery, followed by a new,
separate post-reconnect capture that finalizes and fully decodes. Completed
device-loss and partial-finalization evidence is not repeated or relabelled.
