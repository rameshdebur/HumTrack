# ADR-0003 — Android and UVC use a pluggable capture-source model

**Status:** Accepted  
**Date:** 2026-08-26

## Context

Operators may use phones, wired UVC cameras, or a mixture. The coordinator must not equate capture source with Android.

## Decision

Define a common capability/configure/arm/start/stop/status/finalize/package abstraction. MVP implementations are Android Camera2 and coordinator-local Windows UVC workers. Protocols determine one-or-more source counts and roles.

## Alternatives considered

- Android-only architecture: rejected by product requirement.
- Separate coordinator workflows per source type: rejected because state and completion would diverge.

## Rationale

Common orchestration and package contracts enable single, dual, and mixed workflows while preserving source-specific evidence.

## Consequences

Capabilities, timestamp provenance, IMU absence, and driver limitations remain explicit. Mixed Android/UVC is an MVP acceptance scenario.

## Affected components and interfaces

Coordinator host/UI, Android node, UVC workers, protocols, simulator, quality.

## Supersedes / Superseded by

None.

