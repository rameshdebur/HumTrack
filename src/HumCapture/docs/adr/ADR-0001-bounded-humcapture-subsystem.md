# ADR-0001 — HumCapture is a bounded HumTrack subsystem

**Status:** Accepted  
**Date:** 2026-08-26

## Context

HumCapture borrows HumTrack UI and engineering principles but must not disrupt the existing, heavily developed HumTrack application.

## Decision

All HumCapture code, tests, documentation, and governance remain under `src/HumCapture`. Wider HumTrack files are read-only unless the user explicitly permits a specific change. Integration uses a versioned handoff package rather than internal HumTrack dependencies.

## Alternatives considered

- Separate repository: deferred because a monorepo currently improves traceability.
- Direct integration into existing HumTrack projects: rejected for isolation and authorization reasons.

## Rationale

The boundary protects collaborator work and allows future extraction through stable contracts.

## Consequences

HumCapture needs its own applications, governance, repository, and interfaces. Direct importer work requires separate permission.

## Affected components and interfaces

All HumCapture components and future HumTrack handoff.

## Supersedes / Superseded by

None.

