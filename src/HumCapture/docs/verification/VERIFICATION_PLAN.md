# HumCapture Verification Plan

**Document ID:** HC-VP-001  
**Status:** Preliminary; P0.1 schema/component evidence and P0.2K-integrated bounded hardware diagnostics exist; qualification remains open

The accepted Phase 0 probe procedures and evidence boundary are defined in
`PHASE_0_HARDWARE_CAPABILITY_PROBE_SPEC.md`, with the gated execution sequence in
`PHASE_0_IMPLEMENTATION_PLAN.md`. These documents do not themselves constitute
probe implementation or hardware evidence.

P0.1 source and automated verification are recorded in
`phase-0-results/P0_1_VERIFICATION_REPORT.md`. This evidence is limited to the
shared evidence contract and local tool behavior.

P0.2A enumeration and P0.2B native short-capture evidence are recorded in their
respective reports under `phase-0-results/`. The P0.2B evidence is mixed: one
C920 passed the measured short-capture cadence check and one failed twice. It
does not establish sustained, generic UVC, field, or qualification evidence.
P0.2A-J integration and the retained-primary-evidence gap assessment are recorded
in `phase-0-results/P0_2K_EVIDENCE_INTEGRATION_REPORT.md`.

## Evidence layers

1. Schema/unit tests.
2. Component tests.
3. Cross-process contract tests.
4. Simulated-node integration and virtual clock.
5. Fault injection and restart/recovery.
6. Hardware-in-the-loop.
7. Sustained performance.
8. Field workflow and trained-operator acceptance.
9. Applicable regulatory, usability, and security review.

Report these levels separately. A successful build or broad test suite does not prove hardware, field, scientific, privacy, or regulatory outcomes.

## Mandatory MVP environments

- Supported Windows 11 x64.
- Windows 10 22H2 x64 technical-compatibility environment.
- Two different Android models.
- At least one UVC camera.
- Dedicated AP and laptop hotspot.
- Automatic transfer and USB/MTP recovery.

## Mandatory scenario families

- One Android, one UVC, two Android, mixed Android/UVC.
- Thirty-minute Android and UVC masters with timing/preview; multi-source soak.
- Manufacturer-documented UVC in-use indicator off/on/off behavior, physical
  identity correlation, source-release behavior, and mismatch reconciliation.
- Network/preview/coordinator/source failure.
- Transfer pause/resume/restart/hash/conflict/USB recovery.
- Repository restart, verification, commit, backup, trash, export.
- Pairing, takeover, replay, malformed messages, path traversal, PII/log redaction.
- Subject/protocol/readiness/capture/quality/cleanup trained-operator flows.

## Evidence record

Each case records test ID, linked requirements/risks, software/schema/protocol versions, OS/device/driver/network, preconditions, steps, expected/actual results, artifacts, date, reviewer, and limitations. UVC indicator cases additionally record exact physical/device identity, baseline/active/released observations, other-process ownership checks, and associated capture evidence. The indicator is not a timing source.

Personally identifiable subject data and unapproved subject video must not enter source control.
