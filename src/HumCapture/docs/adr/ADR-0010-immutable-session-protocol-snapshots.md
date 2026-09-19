# ADR-0010 — Immutable session protocol snapshots

**Status:** Accepted  
**Date:** 2026-09-05

## Context

Protocols are reusable, approved, versioned definitions. A capture session must
retain the exact rules used to plan its source count, trial slots, retakes,
completion, and handoff even if a protocol is revised later. The MVP must also
permit correction before acquisition without allowing an in-progress session to
be silently reinterpreted under different rules.

## Decision

Each planned session owns an immutable, content-hashed protocol snapshot. Before
any scientific-master sample exists, a trained operator may create a new audited
snapshot revision and supersede the prior session plan. Once any master sample
has been accepted, the session remains bound to that snapshot for its lifetime.

A material protocol change after acquisition begins requires the existing
session to be closed incomplete and a new session to be created. It is never an
in-place edit. Required trial slots are satisfied by exactly one accepted,
complete trial; retakes and excluded trials remain immutable and traceable.

Session completion requires all required slots and packages to be resolved,
explicit operator review, an immutable completion record, and a versioned
handoff manifest. Reopening a complete session preserves its prior completion
and handoff records, records reason and operator, returns it to completion
review, and creates new versions if it completes again.

## Alternatives considered

- Reference only the current protocol version. Rejected because later protocol
  edits would change the apparent rules for historical data.
- Permit in-place protocol replacement during capture. Rejected because it
  weakens identity, completion, risk-control, and audit traceability.
- Copy only selected protocol fields into the session. Rejected because omitted
  rules could make later reconstruction ambiguous.
- Prohibit every pre-capture correction. Rejected as unnecessarily rigid for a
  trained-operator MVP; audited snapshot supersession is sufficient before any
  master sample exists.

## Rationale

Content-hashed snapshots make historical interpretation deterministic while
keeping routine protocol authoring separate from session execution. Starting a
new session for a material mid-capture change is operationally less convenient,
but it prevents partial acquisitions from being relabelled as satisfying rules
that were not active when they were recorded.

## Consequences

- Snapshot identity, revision, source selection, trial plan, and content hash
  become required session-control data.
- Pre-capture revisions append audit evidence and never overwrite prior plans.
- Mid-capture protocol replacement fails closed.
- Closed-incomplete sessions retain missing/failed requirements and never make a
  successful protocol claim.
- Reopened complete sessions preserve prior completion/handoff versions.
- Implementations need idempotent session commands, transition validation, and
  completion predicates in addition to source-attempt state handling.

## Affected components and interfaces

Windows coordinator, protocol catalog, session/trial repository, Android and
UVC source assignment, transfer/verification, quality, handoff, simulator, and
`HC-IF-CTRL-001`.

## Supersedes / Superseded by

None. Complements ADR-0004 and ADR-0009.
