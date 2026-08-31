# HumCapture Software Lifecycle and AI-Assisted Development Procedure

**Procedure ID:** HC-SOP-SW-004  
**Revision:** 1.0  
**Effective date:** 2026-08-31  
**Status:** Active for engineering work; regulatory applicability and conformity remain unapproved

## Purpose

This procedure controls HumCapture software changes from initiation through
release and maintenance. It ensures that human-written, generated, transformed,
and AI-assisted work is subject to the same requirements, risk, review,
verification, configuration, and release controls.

This procedure supports preparation for applicable India/CDSCO and software
lifecycle expectations. It does not by itself demonstrate conformity with
IEC 62304, ISO 14971, the Medical Devices Rules, 2017, or any certification
scheme. Clause-level conformity requires the approved intended use and
classification, controlled standards and regulatory sources, objective
evidence, and qualified review.

## Scope

This procedure applies to source code, tests, schemas, build and packaging
files, dependencies, generated artifacts, documentation that defines behavior,
release tooling, evidence tooling, and configuration within `src/HumCapture`.

Exploratory probes remain clearly labelled and isolated. They do not become
product architecture, qualified hardware evidence, or released application
behavior without passing the applicable gates in this procedure.

## Governing principles

1. Product and process decisions remain the responsibility of accountable
   people. AI and automation are tools, not approvers or evidence authorities.
2. AI output is treated as an untrusted draft until its provenance, behavior,
   interfaces, dependencies, security impact, and verification have been
   reviewed.
3. Control effort is proportional to intended use, software safety
   classification, affected risks, reversibility, and evidence level.
4. Traceability is maintained across the controlled change, not forced into
   every individual commit. A change record or review may bind a set of commits
   to requirements, risks, tests, evidence, and a release.
5. Coverage percentages, analytical tolerances, and vulnerability thresholds
   are never adopted as generic regulatory values. They must be derived from
   approved requirements, risk controls, measurement uncertainty, and the
   intended use.
6. An SBOM is inventory evidence. It does not replace vulnerability,
   exploitability, licence, supplier, or maintenance review.

## Roles and independence

- **Change owner:** defines the change, maintains traceability, implements or
  coordinates it, and records AI/automation use when material.
- **Component owner:** confirms component and interface correctness.
- **Verification owner:** defines and executes requirement- and risk-based
  verification and records limitations.
- **Independent reviewer:** reviews evidence without relying only on the change
  owner's conclusions when independence is required by risk, release policy,
  or an applicable standard.
- **Release owner:** confirms the exact source/build identity and release gate.
- **Regulatory/Risk Reviewer:** reviews applicability, claims, risk controls,
  and dossier use. Final regulatory conclusions require a qualified Indian
  professional.

One person may perform multiple roles during early engineering work, but may
not represent that arrangement as independent review. A controlled or medically
positioned release requiring independence must use another competent person or
an approved external reviewer and retain the review record.

For the MVP, the signed-in Windows account provides operator attribution only;
it is not a qualified electronic signature.

## Change classification

Before editing, the change owner records the affected scope and classifies the
work as one or more of:

- local implementation;
- cross-component or interface;
- architectural or persistence;
- security, privacy, or supply chain;
- scientific acquisition, timing, or analytical behavior;
- regulatory, intended-use, or claims affecting;
- exploratory or diagnostic.

The record identifies the primary owner, affected owners, requirement and risk
links, interfaces, backward-compatibility impact, planned verification, review
needs, and expected evidence level. Missing approved requirements or interfaces
block product implementation; they do not block a clearly bounded exploratory
probe.

## AI and automated-tool use

Material AI assistance includes generated or substantially transformed code,
tests, schemas, algorithms, security decisions, requirements, risk controls, or
release evidence. The change record shall identify, as applicable:

- the tool and material contribution;
- the accountable change owner;
- important assumptions and externally sourced material;
- validation performed rather than relying on generated explanations;
- any new dependency, licence, privacy, confidentiality, or security impact.

Prompts or complete conversations are not retained by default. Only information
needed to reproduce or understand a material engineering decision is promoted
into controlled project artifacts. Subject data, credentials, private keys,
proprietary datasets, and other unapproved confidential material shall not be
submitted to an AI service.

AI may assist with review, but an AI review is not the required human or
independent approval. AI-generated tests are not independent evidence unless
their oracle, inputs, expected results, and failure sensitivity are themselves
established independently.

## Controlled workflow

### 1. Initiate and plan

- Establish a change identifier and concise objective.
- Link affected requirements, risks, interfaces, ADRs, compatibility promises,
  and claims.
- Define acceptance criteria and evidence level before implementation.
- Record the intended branch or controlled working baseline when version
  control is available.
- Obtain architecture or Regulatory/Risk review before implementation when the
  scoped change triggers those gates.

### 2. Implement within the approved boundary

- Work only in delegated paths and preserve unrelated changes.
- Keep source, tests, generated artifacts, and dependency declarations
  attributable to the change.
- Version externally consumed schemas, protocols, persistence formats, and
  approved capture protocols.
- Do not silently substitute nominal or inferred values for measured scientific
  evidence.

### 3. Build and analyze

Run checks appropriate to the affected technology and risk, including:

- compiler/build/static checks;
- format, schema, and contract validation;
- secret and credential detection;
- applicable static security analysis;
- dependency resolution, SBOM regeneration, and SBOM validation;
- vulnerability, exploitability/VEX, licence, supplier, and maintenance review
  when dependencies or a release are affected.

A vulnerability finding is not disposed solely by CVE presence or absence.
Disposition records severity, affected configuration, applicability,
exploitability, available remediation, compensating controls, residual risk,
owner, and due date. Unresolved findings block release when the approved
security/risk criteria require it.

### 4. Verify behavior

- Trace verification to approved requirements and risk controls.
- Test normal, boundary, negative, interruption, recovery, conflict, restart,
  and compatibility behavior as applicable.
- Establish expected results independently of the implementation under test.
- Derive tolerances from approved performance requirements and measurement
  uncertainty; label provisional engineering thresholds explicitly.
- Treat code-coverage metrics as supporting information, not proof of
  correctness or regulatory conformity.
- Separate source, build/static, automated, runtime, hardware, field, and
  regulatory/clinical evidence levels.

For HumCapture, acquisition verification takes priority over downstream
analytics: measured source timing, frame integrity, metadata provenance, safe
finalization, recovery, transfer verification, and workflow completion are the
relevant MVP evidence domains.

### 5. Review and approve

The reviewer checks:

- requirement, risk, interface, and backward-compatibility impact;
- the complete diff and generated-file provenance;
- new dependencies and security/privacy implications;
- adequacy and independence of test oracles;
- failures, deviations, unresolved anomalies, and unverified areas;
- consistency between behavior, evidence, documentation, and project state.

Review findings are resolved, accepted through the approved risk process, or
left as explicit release blockers. A tool-generated approval cannot satisfy a
human or independent-review gate.

### 6. Baseline and release

A controlled release requires, at minimum:

- a clean, committed and reproducibly identified HumCapture source baseline;
- an approved version and immutable release record;
- source/build and executed-binary identity as applicable;
- passed required checks and verification reports;
- requirements-risk-design-test-release traceability;
- a validated, hash-bound SBOM and separate security/licence dispositions;
- unresolved anomaly and residual-risk review;
- release-owner approval and required independent/regulatory reviews.

An untracked or dirty tree may produce only an `ENGINEERING_SNAPSHOT`. It may
not be called a controlled release or used as regulatory conformity evidence.

## Version-control rules

HumCapture shall use a simple protected-main workflow when it is placed under
version control:

- `main` represents the integrated controlled baseline; direct uncontrolled
  changes are prohibited.
- Short-lived change branches are preferred; a permanent `develop` branch is
  not required for the MVP.
- A review/change record binds its commits to requirements, risks, verification,
  dependency changes, AI contribution, reviewers, and disposition.
- Release tags identify approved release records; a tag alone does not approve
  a release.
- Emergency changes follow the same retrospective traceability, review, risk,
  and release controls and are never silently exempted.

The hosting platform and specific CI/security products are replaceable tools.
Their selected versions and configurations become controlled build/release
inputs when used for release evidence.

## Required records

Retain the applicable:

1. change/review record and linked source revision;
2. requirements, risk, interface, and ADR references;
3. AI/automation contribution declaration when material;
4. build, static, security, dependency and licence results;
5. verification plan, inputs, expected results, results and deviations;
6. review findings and attributed approvals;
7. release record, SBOM, hashes, unresolved anomalies and risk disposition;
8. evidence-vault receipt for retained hardware/runtime evidence.

Records shall follow `HC-GOV-EVIDENCE-001`; SBOMs shall follow
`HC-GOV-SBOM-001`. New controlled changes use
`templates/CHANGE_REVIEW_RECORD_TEMPLATE.md` or an approved equivalent.
Conversation history is not a controlled record.

## Current limitations and release blockers

- HumCapture is currently untracked in the parent repository, so protected
  branches, commit traceability, pull-request review, and controlled release
  identity are not yet available.
- No CI platform or mandatory scanner set has been approved.
- Software safety classification and final India/CDSCO applicability remain
  unresolved.
- Independent signing/review, evidence backup/restore, vulnerability/VEX and
  licence approval, supplier review, and validated eQMS controls remain open.

These limitations do not invalidate exploratory engineering evidence, but they
must remain visible and block controlled or medically positioned release.

## External baseline

- Medical Devices Rules, 2017, as amended: controlling India regulatory source.
- Current CDSCO medical-device software guidance: informative applicability
  input; verify its status and route-specific checklist before use.
- IEC 62304: candidate software lifecycle baseline if applicable; use a
  controlled licensed copy for edition and clause mapping.
- ISO 14971: candidate risk-management baseline if applicable; use a controlled
  licensed copy and qualified risk review.
- IEC 81001-5-1: candidate health-software cybersecurity lifecycle baseline.

Tool names, coverage percentages, tolerances, and branch models are not treated
as regulatory requirements unless a controlled requirement explicitly adopts
and justifies them.
