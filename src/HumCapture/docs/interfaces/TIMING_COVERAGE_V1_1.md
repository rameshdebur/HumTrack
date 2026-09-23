# Timing coverage and verification-record production

**Contract:** HC-IF-XFR-001 opt-in profile 1.3.0 / manifest 1.1.0
**Authority:** user-accepted ADR-0037, 2026-09-23

Manifest 1.0 and its schema remain unchanged. Manifest 1.1 requires the
XFR 1.3 profile and replaces each timed artifact's coverage with:
stream_id, clock_id, ticks_per_second, record_count and spans.
Coverage declarations on other artifact roles are unsupported and refused.
Frame/master coverage uses the frame binary stream and its native clock;
IMU coverage uses the IMU binary stream and its native clock. Frequencies
come from timing metadata, never nominal FPS or container time bases.

Spans are ordered by lane_id, then zero-based run_index. Camera lane is 0;
IMU lane is its positive sensor_stream_id. Within a lane, preserve file order.
Start a new run when segment_id changes, native ticks regress, or sequence
does not strictly increase. A span has lane_id, run_index, segment_id,
record_count, first_ticks, last_ticks, first_sequence, last_sequence and
sequence_gap_count. The latter counts forward nonconsecutive adjacencies,
not estimated lost samples. Repeated native timestamps remain observable;
they do not by themselves split a run or prove a missing frame.
Run boundaries preserve discontinuities without a fabricated continuous range.

Frame coverage includes every source record. Master coverage includes only
disposition ACCEPTED (1) records and must also pass decoded-frame association.
IMU coverage includes every sample independently per sensor lane. Empty
coverage has record_count "0" and spans []; no invented timestamp endpoints.
All record/tick/sequence/count strings are canonical uint64 decimal values.
Segments are uint32; lane IDs are 0..65535. Manifest SHA-256 binds all coverage.
The current engineering envelope is 16,384 spans per artifact and a 16 MiB
manifest; exceeding it refuses verification, not acquisition or source retention.
The verifier reconstructs the full coverage object and requires exact equality.
Metadata stream/clock identities, sample CRCs and capture identities remain
independently checked. This checks declarations, not physical clock truth.

Version 1.0 coverage is retained unchanged and reported NOT_ASSESSED by the new
producer, with a specific reason: its summary semantics are not sufficient for
this versioned comparison. Existing low-level comparisons still run; legacy
files are not relabelled as 1.1. Fresh v1.1 source packages require correctly
produced coverage. A historical migration/review path is not implemented here.

## Internal record and admission boundary

Produce the existing verification-record 1.0 shape only from a held package
lease and the actual pinned decoder. No observation callback can create it.
Required check NOT_ASSESSED or FAIL yields aggregate FAILED in that existing
wire vocabulary; the check reason distinguishes unavailable verification from
measured failure. FAILED is not a scientific-quality rejection or a delete
instruction. Invalid input/identity bytes produce an error, not invented results.

The twelve existing checks retain artifact-dependent applicability. A survivor
package with no master can verify the integrity of its retained events and
finalization; this does not make its FINALIZED_INCOMPLETE capture complete.
For timed artifacts require v1.1 coverage and all applicable frame/IMU metadata
comparisons. Recorded mapped times in TIM 1.0 remain unassessed; unmapped native
records do not acquire a fabricated mapping requirement. Actual TIM 1.1 mapped
records must compare exactly. Physical calibration/synchronization, full session
authorization and protocol quality are not silently implied by custody checks.
Retain scientific condition outcomes and missing dimensions as evidence
references in the record, separately from technical verification disposition.

An internal repository adapter derives package identities from leased manifest
bytes and the operator from WindowsIdentity. Caller supplies stable operation,
record IDs and audit time for retries, not a pre-made verification outcome.
Only VERIFIED calls existing staged admission, under the repository gate while
the package lease is held. Records bind exact canonical bytes by SHA-256.
An exact retry uses the same IDs/time and bytes; conflicts never overwrite.
Nonverified records are returned to the caller without journal advancement;
the batch adds no production host invocation or new failed-record storage path.
Cancellation is checked before admission; once synchronous durable admission
begins it finishes or reports its existing recovery error.
No move, commit, receipt, automatic deletion, or take-completion transition.
