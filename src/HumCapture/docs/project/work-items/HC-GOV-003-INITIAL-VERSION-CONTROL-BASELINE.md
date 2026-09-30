# HC-GOV-003 — Initial Version-Control Baseline

**Change ID:** HC-CHG-20260831-001  
**Tags:** GOV.1N | SCM | INITIAL-BASELINE | TRACEABILITY  
**State:** Accepted as an engineering source-control baseline  
**Change owner:** Signed-in Windows account operator, assisted by Codex Orchestrator  
**Component owner:** Orchestrator / Release owner  
**Verification owner:** Codex QA role; not independent of implementation  
**Independent reviewer:** Not performed; required before any controlled or medically positioned release  
**Created:** 2026-08-31

## Objective and boundary

Place the existing bounded `src/HumCapture` subtree under Git version control
without staging or modifying unrelated HumTrack work. Exclude compiled output,
runtime captures, caches, local evidence-vault data, and secrets. Preserve the
small synthetic evidence fixtures required by automated contract tests.

This change creates a source-control baseline only. It does not create a
controlled product release, qualify hardware, approve application
implementation, or establish regulatory conformity.

## Classification and ownership

- Classification: software lifecycle, configuration, supply chain, evidence,
  and release-control governance.
- Primary owner: Orchestrator / Release owner.
- Affected owners: all HumCapture component owners, QA, Security Reviewer, and
  Regulatory/Risk Reviewer.
- Paths: only `src/HumCapture`.
- External HumTrack changes: prohibited and excluded from staging.
- Architecture review: not required; no runtime architecture or interface is
  changed.
- Regulatory/Risk review: required before dossier or medically positioned use,
  not satisfied by this engineering baseline.

## Traceability

| Type | IDs or artifacts | Disposition |
|---|---|---|
| Procedure | HC-SOP-SW-004 | First operational use of the change-control procedure |
| Evidence control | HC-GOV-EVIDENCE-001, ADR-0007 | Raw evidence remains outside Git; summaries and schemas are versioned |
| Supply chain | HC-GOV-SBOM-001, ADR-0008, HC-SEC-REQ-006 | Validated CycloneDX SBOM is included; vulnerability/licence review remains open |
| Risks | HC-RISK-017, HC-RISK-018, HC-RISK-019 | Baseline improves source identity; independent review and other residual controls remain open |
| Project boundary | HC-SYS-REQ-001, ADR-0001 | Only the bounded HumCapture subtree may be committed |

## AI and prior-provenance declaration

Material AI assistance: substantial. Codex assisted with architecture,
governance, probe code, tests, evidence tooling, reports, and this baseline
record during exploratory development. Human direction and hardware
observations were supplied by the signed-in operator.

Because the subtree predates its first Git commit, exact per-file and per-change
AI provenance cannot be reconstructed. The limitation is accepted only for this
initial engineering baseline and must not be represented as independent review
or release evidence. Future material changes use an individual change/review
record or approved equivalent.

No subject data, credentials, private keys, raw capture media, or retained raw
hardware evidence is intended for the baseline.

## Baseline preparation

- Parent repository branch at initiation: `master`.
- Parent HEAD at initiation: `9743fa22ddbd`.
- Numerous unrelated modified and untracked HumTrack paths were present before
  this change and are expressly excluded.
- HumCapture-local ignore rules exclude build output, dependencies, raw video,
  diagnostic logs, and local vault/staging paths while retaining the required
  synthetic fixture log.
- HumCapture-local Git attributes enforce LF for deterministic text, schemas,
  fixtures, reports, and hash-bound artifacts; common future media formats are
  explicitly binary.
- Remote-review branch base: `origin/master` at
  `67c47f4ba5c29c46c3bdbcfc72133a0492a8ac12`.
- Baseline commit identity:
  `fc3dbd02aa6b73e7ca42bb084229c627b859d006`.
- Source-equivalent local preparation commit
  `6c47f6a3e9f54696c220f3eae67c5724ffe10622` was not used as the review-branch
  parent because it inherited four unrelated local HumTrack commits.
- Closure commit identity: the commit containing the final accepted version of
  this record; report externally after commit.

## Verification plan

Before the baseline commit:

1. Inspect the exact staged paths and prove every path is under
   `src/HumCapture`.
2. Confirm no ignored compiled/runtime output is staged.
3. Scan staged source for likely credential/private-key material and inspect
   findings.
4. Run the Phase 0 evidence-contract tests, evidence-control tests, SBOM tests,
   schema/CLI validation, managed build/self-tests, and native selection
   self-tests appropriate to the existing baseline.
5. Validate the generated SBOM with project tooling and confirm its retained
   SHA-256.
6. Inspect the staged diff summary and file-size outliers.

## Results and decision

| Check | Result |
|---|---|
| Candidate path boundary | Only `src/HumCapture` selected; unrelated parent changes excluded |
| Exact staged baseline | 142 files, 12,086 inserted lines; boundary and generated-output checks passed |
| Large candidate files | No non-ignored candidate exceeded 1 MiB |
| Generated/runtime output | Managed/native build output, `node_modules`, raw video and ordinary logs ignored |
| Checkout byte stability | Subtree-local Git attributes enforce LF and mark common media formats binary |
| Required synthetic log | Explicitly unignored and exercised by evidence-contract tests |
| Likely credential/private-key marker scan | No marker found; this is supporting evidence, not a complete secret audit |
| Phase 0 evidence-contract tests | 55/55 passed |
| Evidence-control tests | 12/12 passed |
| SBOM tests | 4/4 passed |
| Managed probe | Release build passed with 0 warnings/errors; 7/7 self-tests passed |
| Native Media Foundation probe | Release x64 build passed; 8/8 self-tests passed |
| Project SBOM validation | Passed, CycloneDX 1.7, 15 components, 16 dependency nodes |
| SBOM SHA-256 | `2f34a8112c2ea274c4be7fd4d15443368feb4baa06c3e364dd7fae736f94e5` |

The first evidence-contract test attempt stopped because local dependencies had
been removed during cache cleanup. `npm ci --ignore-scripts --no-audit
--no-fund` restored the exact lockfile dependencies in ignored `node_modules`
directories; the complete rerun then passed.

Final decision: accepted as the initial **engineering source-control baseline**
at commit `fc3dbd02aa6b73e7ca42bb084229c627b859d006`. The HumCapture scoped working
tree was clean immediately after that commit. This is not a controlled release.
Independent review, remote branch protection, CI records, vulnerability/VEX,
licence/supplier approval, and qualified regulatory review remain open.
