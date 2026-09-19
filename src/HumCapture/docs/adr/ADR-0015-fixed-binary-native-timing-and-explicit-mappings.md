# ADR-0015 — Fixed binary native timing with explicit mappings

**Status:** Accepted  
**Date:** 2026-09-09

## Context

Android Camera2/IMU and Windows UVC expose different timestamp sources,
cadences and metadata. Long captures require efficient, recoverable records,
while downstream analysis requires exact provenance and must not confuse
nominal FPS, host arrival, container PTS or UTC with sensor time. Camera classes
also differ: some are fixed-rate, some variable/adaptive, and some expose no
device timestamp or advanced lens data.

## Decision drivers

- Preserve the native scientific observation before derived alignment.
- Keep Android, UVC and Coordinator decoding deterministic and testable.
- Localize corruption and recover an incomplete final write.
- Support fixed-rate and capability-limited cameras without fabricated data.
- Preserve mappings, uncertainty, coordinate frames and transformations.
- Avoid a database or interleaved multimedia container in the MVP contract.

## Considered options

### CSV/JSON observations

Human-readable, but bulky at sensor cadence, slower to parse and liable to lose
integer/float representation discipline. Retained only as optional derivatives.

### One interleaved camera/IMU container

Can provide a shared transport timeline but couples independent clocks and
failure domains and complicates partial recovery. Rejected for the MVP master.

### Fixed binary streams plus versioned JSON metadata

Small deterministic records preserve high-rate native evidence; JSON expresses
clocks, models, coordinate frames and optional device metadata. Selected.

## Decision

Adopt HC-IF-TIM-001 1.0.0: separate fixed-record little-endian frame and IMU
files, CRC32C per header/record, package SHA-256, explicit presence flags,
independent sequence/clock domains, affine mapping segments with uncertainty,
and versioned timing/camera/IMU JSON sidecars. Native timestamps are
authoritative and never synthesized from nominal rate. Fixed, variable and
adaptive cameras are capability classes rather than universal acceptance gates.

## Consequences

### Positive

- Corrupt/truncated records are located deterministically.
- Android, UVC and Coordinator share golden vectors and one reference oracle.
- Raw evidence survives later improvements to mapping or analytics.
- Capability limitations remain explicit rather than silently normalized.

### Negative

- Producers and consumers need binary codecs and CRC32C.
- New fields generally require a compatible version or new record layout.
- JSON sidecars and binary streams must remain identity-bound in the package.
- Software alignment cannot provide a hardware-synchronization claim.

### Risks and mitigations

- Wrong layout/version interpretation: magic, sizes, versions and golden tests.
- Clock/provenance confusion: explicit clock IDs, epochs, mappings and authority.
- Hidden loss: independent sequences, dispositions, counts and complete decode.
- Incorrect transform/analytics: provenance, validity bounds and uncertainty.

## Affected components and interfaces

Android capture, Windows UVC workers, Coordinator ingestion/verifier, package
manifest, quality evaluation, downstream analytics and simulator/QA.

## Related decisions

Complements ADR-0002, ADR-0003, ADR-0005, ADR-0009, ADR-0011 and ADR-0012.
Supersedes none.
