# ADR-0002 — Local scientific master and disposable preview

**Status:** Accepted  
**Date:** 2026-08-26

## Context

Network and UI variability must not corrupt scientific acquisition.

## Decision

Android records authoritative masters locally. UVC workers record locally on the coordinator. Preview is an independent bounded path that drops or disables before affecting master/timing/finalization. Preview never reconstructs master data.

## Alternatives considered

- Network master: rejected because network interruption would threaten acquisition.
- One transport for master/control/preview: rejected because failure coupling violates the invariant.

## Rationale

Local masters survive Wi-Fi, coordinator, and preview failures.

## Consequences

Post-capture transfer, storage preflight, package verification, and recovery are mandatory.

## Affected components and interfaces

Android recorder/preview, UVC workers, coordinator monitoring, transfer and quality.

## Supersedes / Superseded by

None.

