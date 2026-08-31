# ADR-0005 — MVP uses software clock synchronization

**Status:** Accepted  
**Date:** 2026-08-26

## Context

The MVP requires multi-source timing but will not require LED, audio, GPIO, or other hardware synchronization.

## Decision

Use repeated two-way exchanges to estimate offset, drift, RTT, and uncertainty; schedule future starts; retain raw exchanges and timestamp provenance; reconstruct a software-aligned timeline offline. Hardware synchronization and hardware-validated exposure simultaneity are deferred.

## Alternatives considered

- Immediate sequential start commands: rejected due unequal network latency.
- Hardware trigger/LED validation: deferred by MVP scope.

## Rationale

Software synchronization is deployable on Android/UVC/laptop-hotspot workflows and preserves evidence for later refinement.

## Consequences

Targets are provisional until Phase 0. The product must never claim hardware synchronization, and sessions outside thresholds retain deviations.

## Affected components and interfaces

Control protocol, Android timing, UVC timestamps, clock synchronizer, quality, handoff.

## Supersedes / Superseded by

None.

