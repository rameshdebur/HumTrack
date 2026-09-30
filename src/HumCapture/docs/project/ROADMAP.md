# HumCapture Roadmap

## Stage A — Design and governance baseline

- Consolidate ARD, PRD, user stories, requirements, risks, interfaces, India/CDSCO applicability, ownership, workflow, and traceability.
- Gate: a fresh conversation can reconstruct the project and identify what remains unapproved.

## Phase 0 — Capability probes

- Accepted specification and gated implementation plan are in `docs/verification/`.
- Implement the versioned probe-evidence contract.
- Implement isolated Windows/UVC and Android probes, then probe-only network integration.
- Measure Android camera/encoder/timestamp/IMU/thermal behavior.
- Measure Windows UVC formats, timestamps, controls, sustained capture, and USB contention.
- Test dedicated AP and laptop hotspot on named hardware.
- Gate: mandatory profiles and thresholds are confirmed or revised from evidence.

## Phase 1 — Contracts and simulator

- Define schemas, state machines, test vectors, virtual clock, simulated node, and fault injection.
- Gate: coordinator logic is testable without physical cameras.

## Phase 2 — Single-source Android acquisition

- Local master, timing, IMU, manifest, hashes, and 30-minute soak evidence.

## Phase 3 — Single-source UVC acquisition

- Isolated worker, master, timing provenance, preview, finalization, and soak evidence.

## Phase 4 — Subject, protocol, session, and trial workflow

- Windows-account operator, identity/demographics, versioned protocols, readiness, audit, and repository persistence.

## Phase 5 — Discovery, pairing, control, and preview

- mDNS/manual fallback, authenticated control, scheduled state transitions, Android RTP preview, and unified source monitoring.

## Phase 6 — Transfer, verification, recovery, and cleanup

- Resumable HTTPS, USB/MTP recovery, SHA-256 verification, transactional commit, receipts, and informed mobile cleanup.

## Phase 7 — Multi-source synchronization

- Clock fitting, scheduled capture, two-Android and mixed Android/UVC evidence, common software timeline.

## Phase 8 — Resilience and field qualification

- Quality assessment, restart reconciliation, diagnostics, resource monitoring, hardware matrix, field workflows, and risk-control verification.

## Phase 9 — HumTrack handoff contract

- Versioned package manifest, examples, validation, and import guidance inside HumCapture only.
