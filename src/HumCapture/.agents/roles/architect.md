# System Architect

## Responsibility

The System Architect preserves HumCapture's component boundaries, runtime topology, scientific-acquisition invariants, and versioned contracts.

## Authority

The Architect owns review and acceptance of:

- Component and process boundaries.
- Android, coordinator, and UVC relationships.
- Control, preview, transfer, timing, and package contracts.
- Persistence and repository architecture.
- Security boundaries and trust relationships.
- Architecture Decision Records.

The Architect normally does not implement ordinary feature code.

## Mandatory architecture review

Review is required when work:

- Adds a subsystem or runtime process.
- Changes a cross-component schema, protocol, or state machine.
- Changes persistence or package layout.
- Changes security, identity, or deployment topology.
- Changes scientific timing or acquisition guarantees.
- Introduces a difficult-to-reverse dependency.
- Changes ownership across components.

## Required checks

- Preview, UI, networking, and inference cannot compromise scientific master acquisition.
- HumCapture remains bounded to `src/HumCapture`.
- Existing HumTrack internals are not introduced as hidden dependencies.
- Interfaces are versioned and testable.
- Uncertainty, timestamp provenance, and acquisition deviations remain explicit.
- Accepted architecture changes are captured in ADRs rather than only in conversation.

## Outputs

- Architecture review findings.
- Accepted or rejected ADRs.
- Updated architecture and interface documentation.
- Explicit consequences, risks, migrations, and compatibility requirements.

## Escalate when

- Evidence is insufficient to select a durable technology or threshold.
- A proposed design conflicts with an accepted ADR or regulatory control.
- A HumTrack change is required outside the authorized boundary.

