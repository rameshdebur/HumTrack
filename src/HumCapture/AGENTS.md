# HumCapture Agent Instructions

## Scope and write boundary

HumCapture is a bounded acquisition subsystem of HumTrack. Agents may read the wider HumTrack repository to understand UI and engineering conventions, but may modify only `src/HumCapture` unless the user explicitly authorizes a specific external change.

HumCapture may borrow HumTrack principles; it must not introduce hidden dependencies on HumTrack internals.

## Project authority

When information conflicts, use this order:

1. Executable behavior and directly relevant passing tests.
2. Versioned interface/schema specifications.
3. Accepted ADRs.
4. Current project-state and approved requirements documentation.
5. This file and repository workflow instructions.
6. Agent handoffs or memory.
7. Historical conversation text.

Tests establish only the behavior they directly verify. Obsolete documentation must be corrected when discovered.

## Mandatory design and governance gate

Do not begin application feature implementation until the project state records that the ARD, PRD, user stories, requirements, roles, governance, applicable India/CDSCO baseline, and required interface/risk plans are baselined for that work.

Exploratory probes must remain isolated and clearly labelled; they do not become architecture silently.

## Acquisition invariant

Nothing in preview, networking, UI, transfer, or downstream inference may compromise scientific master acquisition.

Priority is:

```text
Master capture and sensor timestamps
> timing and IMU metadata
> safe finalization
> control and health
> preview
> UI
> post-capture transfer
```

Never substitute nominal FPS, host-arrival time, preview frames, or inferred values for measured source evidence without explicit provenance.

## Context bootstrap

For a new task:

1. Read this file.
2. Read `docs/project/PROJECT_STATE.md` when present.
3. Identify affected components using `.agents/ownership.md`.
4. Read relevant architecture and accepted ADRs.
5. Read relevant requirements, risks, compliance baseline, and interfaces.
6. Inspect current source and tests.
7. Inspect targeted Git history only when rationale remains unclear.
8. Retrieve additional memory only when needed.

Select relevant context; do not load the whole project by default.

## Change discipline

Before editing:

- Classify work as local, cross-component, interface, architectural, regulatory, or exploratory.
- Identify the primary and affected owners.
- Identify affected contracts and risk controls.
- Check the current India/CDSCO and standards baseline when applicability or claims may be affected.
- Preserve unrelated and collaborator changes.

During implementation:

- Stay within delegated paths and scope.
- Avoid unrelated refactoring.
- Preserve backward compatibility unless a contract change is explicitly approved.
- Version schemas, protocols, persistence formats, and approved capture protocols.
- Update behavioral and failure-path tests with behavioral changes.
- Capture durable architecture decisions in ADRs.

Architecture review is required for a new subsystem, cross-component contract, persistence change, security boundary, deployment topology, scientific timing guarantee, ownership change, or difficult-to-reverse dependency.

## Ownership and coordination

Use `.agents/ownership.md` and the responsibility files under `.agents/roles/`.

Any agent may read anything. Modification authority follows delegated responsibility. Cross-component work must name the primary owner, affected owners, interface impact, required reviews, and verification owner.

Parallelize only independent work with non-overlapping edits or already-agreed interfaces. Integrate and review results before completion.

## Regulatory, risk, and claims discipline

India/CDSCO is the primary regulatory jurisdiction. Use current official sources and controlled standards; do not rely on remembered clause text.

Agents may prepare evidence and analyses. Final CDSCO classification, licensing strategy, legal interpretation, and certification claims require a qualified Indian regulatory professional and recorded review.

Do not claim clinical accuracy, diagnostic capability, CDSCO approval, certification, hardware-level synchronization, or medical-device compliance without the defined evidence and authorization.

## Verification discipline

Before completion:

- Run proportionate tests and static checks.
- Inspect failures and the final diff.
- Verify changed interfaces and integration points.
- Check documentation and project state against actual behavior.
- Record what was and was not verified.

Report evidence levels separately:

- Source implemented.
- Build/static checks passed.
- Automated behavior verified.
- Runtime integration verified.
- Hardware-in-the-loop verified.
- Field workflow verified.
- Regulatory or clinical review completed.

One level is not evidence for another.

## Completion and handoff

A task is not complete merely because code was written. Completion normally includes implementation, tests/evidence, affected interface and documentation updates, risk/traceability updates, current project-state updates, ADRs where required, and a concise handoff.

Promote durable discoveries into code, tests, schemas, ADRs, architecture, requirements, risk records, project state, known issues, or handoffs. Conversation history is not durable project memory.
