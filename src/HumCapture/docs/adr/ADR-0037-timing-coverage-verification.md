# ADR-0037: Timing coverage before verification-record production

**Status:** Accepted by user 2026-09-23
**Date:** 2026-09-23
**Batch:** C14U-W, verification records and repository admission

## Context

The user approved the batch, not a silent reinterpretation of immutable timing
contracts. XFR 1.2 requires timing coverage on master video, frame timestamps
and IMU samples. Its v1 manifest supplies one clock, first/last ticks, record
count, discontinuity count and optional sequence endpoints per artifact.
It does not define endpoint selection for interleaved sensor lanes, the
discontinuity counting rule, or coverage across clock segments.

The existing input reader validates the summary's structure and numeric bounds,
but not equality with samples. The synthetic Android fixture has a mismatched
IMU summary; see HC-VR-I0-4B-C14UVW-PREFLIGHT-001. Passing existing tests does not
establish coverage verification. Existing internal results must not be promoted
to an overall VERIFIED record on that basis.

## Decision

Add a small opt-in, versioned coverage contract before producing verification
records. Specify counts and native-time bounds per logical stream and clock
segment, keeping IMU sensor lanes independent. Define how master-video coverage
binds to accepted source frames, rather than treating container PTS or nominal
FPS as native acquisition time. Define empty streams, sequence gaps and
discontinuities explicitly. Exact wire shape and executable vectors must be
baselined before implementation.

Preserve legacy packages unchanged. Where old coverage can be checked
unambiguously, retain that evidence; where it cannot, record NOT_ASSESSED, not
PASS or an invented scientific failure. Missing required verification prevents
automatic verified admission but does not prevent capture, survivor collection
or retention in staging. Recovery is a reviewed, explicitly versioned evidence
path, not rewriting finalized masters or deleting source data.

Scientific quality remains separate from custody verification and take
completion. No universal FPS, adaptive-frame-rate, IMU-on-UVC or second-camera
requirement is introduced. No host activation, move, receipt or cleanup is
authorized by this clarification.

## Alternatives and trade-offs

- Trust schema-valid summaries: simplest, but would permit a false verification
  claim; not recommended.
- Redefine v1 fields in place: less schema work, but changes interpretation of
  immutable historical packages; not recommended.
- Version the coverage semantics: recommended; a bounded interface change and
  regression vectors are required, and some legacy evidence remains unassessed.

## Ownership and gates

Classification: cross-component interface/scientific-evidence change. Primary
System Architect; affected Coordinator/Repository, Android/UVC, Timing and QA
owners. QA owns fixture, failure-path, compatibility and integration verification.
Release/SBOM and Risk owners review effects before batch completion. Existing
ARD/PRD/stories/SRS/governance and preliminary India baseline apply; no changed
intended use or regulatory claim. Human independent review remains separate.

User approved including the clarification and implementation in C14U-W.
TIMING_COVERAGE_V1_1.md defines the bounded wire/verification semantics.
This does not establish a production release or certification claim.
