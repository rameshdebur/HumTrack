# ADR-0011 — Exact monotonic time wire representation

**Status:** Accepted  
**Date:** 2026-09-05

## Context

Android, Windows UVC workers, and the coordinator use different monotonic clock
epochs and tick frequencies. Control messages must preserve exact native values
across Kotlin/JVM, .NET, native C++, JSON tooling, and JavaScript without
mistaking UTC or host arrival time for scientific timing.

JSON numbers cannot portably preserve every 64-bit integer through JavaScript.
Sending only nanoseconds would also conceal a device's native tick frequency and
invite unjustified precision claims.

## Decision

A native monotonic instant is represented by `clock_id`, unsigned 64-bit
`ticks`, and integer `ticks_per_second`. `ticks` is a canonical unsigned decimal
string. A mapped session instant additionally carries `session_clock_id`,
`clock_model_id`, and `uncertainty_ns`; its ticks use the same representation.

Each source boot creates a new clock identity. A source attempt cannot silently
continue across a changed boot or clock identity. Raw native timestamps remain
authoritative evidence; mapped session time is a versioned interpretation.

UTC timestamps are retained for audit, display, and record chronology but SHALL
NOT order scientific samples, prove a scheduled start, or replace monotonic
evidence. Conversion and range checks must reject non-canonical, negative,
fractional, overflowed, or provenance-free values.

Content-addressed JSON records use SHA-256 over the UTF-8 bytes of their
[RFC 8785](https://www.rfc-editor.org/rfc/rfc8785.html) JSON Canonicalization
Scheme representation. The record's own `*_content_sha256` property is omitted
before canonicalization, while every other member remains included. This makes
identity independent of JSON member order and gives Kotlin, .NET, native, and
JavaScript implementations one deterministic byte sequence. SHA-256 is the
algorithm specified by [RFC 6234](https://www.rfc-editor.org/rfc/rfc6234.html);
the lowercase 64-hex encoding is the wire form.

## Alternatives considered

- **JSON numeric 64-bit ticks:** compact, but rejected because common JavaScript
  consumers lose integer precision above `2^53 - 1`.
- **Floating-point seconds:** convenient, but rejected because rounding changes
  exact source evidence and equality semantics.
- **Nanoseconds only:** rejected because it hides native resolution/frequency
  and can imply accuracy the source does not provide.
- **UTC timestamps only:** rejected because wall-clock adjustment and network
  delay make them unsuitable as the primary scientific timeline.
- **Serializer-native JSON bytes:** rejected because insignificant member order
  and formatting differences would give equivalent records different hashes.

## Consequences

- Consumers must parse canonical decimal strings into a checked 64-bit integer
  or equivalent arbitrary-precision type.
- Schemas can reject lexical errors; conformance logic enforces the exact
  unsigned 64-bit upper bound and cross-record clock identity.
- Clock-model uncertainty remains explicit and cannot be silently treated as
  zero.
- Producers and consumers need an RFC 8785 implementation; content-hash checks
  fail closed before a bound record is accepted.
- Human-readable JSON is slightly more verbose.
- The timing/IMU binary-stream format remains a separate decision; this ADR
  governs JSON control and evidence records only.

## Affected components and interfaces

Android capture control, Windows coordinator, UVC workers, clock synchronizer,
readiness, start/stop planning, source events, quality, simulator, and
`HC-IF-CTRL-001`.

## Supersedes / Superseded by

None. Complements ADR-0005, ADR-0009, and ADR-0010.
