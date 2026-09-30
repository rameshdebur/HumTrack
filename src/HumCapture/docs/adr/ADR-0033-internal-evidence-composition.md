# ADR-0033: Read-only IMU and capture-event evidence composition

Status: Accepted for the user-approved C14L-N engineering batch, 2026-09-17.

Use shared timing-clock bindings for frame and IMU checks. Exact mapped-value
comparison requires explicit timing 1.1; legacy 1.0 never gains invented rounding.
Sensor units/kinds and identities must agree. Report available samples and
continuity anomalies without substituting requested rate for observed evidence.
Validate declared rotation matrices, not physical calibration accuracy.

Capture archive/summary checks consume a caller-supplied manifest projection,
not an unvalidated manifest admission endpoint. Bind exact byte lengths/hashes,
identities and finalization/event semantics to HC-IF-ART-001. The canonical
encoder is restricted to this schema's JSON-safe integers, strings and containers;
it is not a general floating-point RFC 8785 implementation. Unicode is preserved,
invalid surrogate sequences rejected, object keys sorted by UTF-16 ordinal order.
Reference: https://www.rfc-editor.org/rfc/rfc8785.html sections 3.1-3.2.

Compose typed, caller-supplied finalized bytes and decoded-media evidence inside
the repository assembly. Bind decoded evidence to the supplied master hash and
length. The result exposes individual comparisons and incomplete/not-assessed
states, never VERIFIED. File leases, full manifest/protocol admission, cadence,
calibration truth and production activation are separate gates. No network,
camera access, journal writes, receipt or deletion belongs in this component.

Trade-off: a narrow internal composition avoids premature production activation
and new dependencies, but cannot establish provenance of caller-supplied decoded
observations. Synthetic tests prove logic only, not decoding or hardware accuracy.
Retain the existing decoder's separately recorded integration evidence.
