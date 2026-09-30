# HumCapture Development Workflow

## Default lifecycle

```text
User request
→ context selection
→ scope and ownership classification
→ architecture/regulatory impact check
→ requirement and risk identification
→ task decomposition
→ implementation or isolated exploration
→ integration
→ independent review
→ verification
→ traceability and knowledge updates
→ handoff
```

## Decision gate

Classify non-trivial work before editing:

- **Local:** One component, no contract change.
- **Cross-component:** Multiple owners; coordinate interfaces and integration.
- **Interface:** Specify and review the contract before or alongside implementation.
- **Architectural:** Accepted ADR required before broad implementation.
- **Regulatory/risk:** Refresh applicable India/CDSCO baseline and plan evidence.
- **Exploratory:** Isolate, time-box, and do not silently promote to architecture.

## Status tags

Every active work item, project-state update, verification report, and concise
handoff should include a quickly scannable tag line:

```text
PHASE | COMPONENT | LAYER | ACTIVITY | EVIDENCE
```

Use stable uppercase terms and add a sixth tag only when it materially improves
scope recognition. Examples:

```text
P0.2A | UVC | CORE | ENUMERATION | HARDWARE
P0.2B | UVC | CORE | CAPTURE | TIMING | HARDWARE
P1.1 | COORDINATOR | UI | SUBJECT-WORKFLOW | AUTOMATED
```

- **Phase:** governed increment or work-item identifier.
- **Component:** `UVC`, `ANDROID`, `COORDINATOR`, `TRANSFER`, `REPOSITORY`,
  `TIMING`, `CONTRACT`, or another owned component.
- **Layer:** `CORE`, `UI`, `NETWORK`, `STORAGE`, `SECURITY`, `CONTRACT`, or
  `GOVERNANCE`.
- **Activity:** concrete surface such as `ENUMERATION`, `CAPTURE`, `PAIRING`,
  `FINALIZATION`, `TEST`, or `REVIEW`.
- **Evidence:** highest evidence surface currently being exercised, such as
  `SOURCE`, `AUTOMATED`, `RUNTIME`, `HARDWARE`, or `FIELD`.

Tags communicate scope only. They do not replace the separate evidence-level
statement or imply that a gate has passed.

## Pre-implementation checklist

- Current project state permits the work.
- Applicable ARD/ADR and requirement IDs exist.
- Affected interface and risk-control IDs are identified.
- Primary owner and reviewer are named.
- Required hardware, data, policy, and acceptance evidence are available or explicitly deferred.
- Paths remain inside `src/HumCapture` unless the user approved otherwise.

## Parallel work

Parallelize only independent tasks with non-overlapping edits or stable reviewed interfaces. Shared contracts, project state, and traceability are integrated by one owner after component work completes.

## Review and completion

Implementation and final verification should use different contexts where practical. Completion evidence must distinguish source, build, automated behavior, runtime integration, hardware, field, and regulatory/clinical review.

## Model routing

Use the strongest available reasoning for architecture, regulatory/risk analysis, difficult debugging, and independent review. Use efficient coding capability for bounded routine implementation. Correctness, traceability, and verification take priority over model cost or agent count.
