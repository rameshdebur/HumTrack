# Protocol scientific rules 1.1

Accepted engineering extension, 2026-09-22, ADR-0036. Snapshot schema:
schemas/control/v1.1/protocol-snapshot.schema.json. This opts in independently
of timing 1.0/1.1; legacy control/snapshot 1.0 is unchanged and not upgraded.
Host/session planning does not consume this extension until explicit activation.

The existing snapshot properties remain, with schema_version=1.1.0 and required
scientific_rules (0-256). Each rule has unique rule_id, existing role_id, level
(REQUIRED/PREFERRED/INFORMATIONAL), metric, operator (GE/LE/EQ) and threshold.
Rules are included in snapshot_content_sha256, using canonical JSON excluding
only that root hash. Numeric thresholds are nonnegative JSON-safe integers.
No default numerical thresholds are specified by this contract.

| Metric | Threshold / evidence |
|---|---|
| OBSERVED_MILLIHZ | Native frame cadence in 0.001 Hz units; exact rational comparison |
| MAX_INTERVAL_US | Maximum native interval in microseconds, exact rational comparison |
| SEQUENCE_GAPS | Sum of missing sequence numbers within segments |
| SEQUENCE_DUPLICATES / SEQUENCE_REGRESSIONS | Counts within segments |
| TIMESTAMP_REGRESSIONS / TIMESTAMP_DUPLICATES | Native timestamp counts within segments |
| DECODED_WIDTH / DECODED_HEIGHT | Actual decoded coded geometry, not requested/negotiated dimensions |
| FOCAL_LENGTH_PRESENT | EQ 1 or 0; reported value present or explicitly reported unavailable; otherwise unassessed |
| TIMESTAMP_PROVENANCE | EQ one camera-schema provenance enum; reported metadata consistent with native clock declaration |
| RATE_CONTROL_CLASS | EQ FIXED, VARIABLE, ADAPTIVE or UNKNOWN; reported negotiated class |

Native cadence uses all source frame records, including recorded nonaccepted
dispositions. It describes recorded source delivery, not video output FPS or
physical exposure accuracy. For a single uninterrupted strictly increasing
timeline: observed Hz = (record_count - 1) * ticks_per_second / (last - first).
Subtract integer timestamps before converting; threshold comparisons never use
floating-point rounding. Sequence gaps remain visible and lower observed delivery
rate. One/zero records have no interval/rate. Segment changes, time/sequence
regressions, duplicate sequence or duplicate native times prevent overall rate
assessment. Maximum-interval acceptance is also unassessed across segment/time/
sequence regression barriers. Per-segment anomalies and observed within-segment
maximum remain reportable; they are never stitched across a restart.

Camera stream ID/source kind must agree with the admitted binary/package even
without decoding. Camera/native-clock provenance and authority must agree;
HOST_SAMPLE maps to MF_SAMPLE_TIME/HOST_SAMPLE, HOST_ARRIVAL maps to
QPC_HOST_ARRIVAL/HOST_ARRIVAL. Reported provenance is not physical qualification.
Metadata's floating observed_hz is not used as measurement authority; raw native
records are recomputed. No physical lens/calibration truth is inferred.

## Aggregation (1.1 only)

Required FAIL takes precedence -> REJECTED. Otherwise required NOT_ASSESSED ->
REVIEW_REQUIRED. Otherwise preferred FAIL/NOT_ASSESSED -> DEGRADED_ACCEPTABLE.
Otherwise -> CONFORMANT. Empty/informational-only rules -> REVIEW_REQUIRED.
NOT_APPLICABLE is not an automatic bypass; unsupported outcomes are rejected.
No rules for the assigned role, absent protocol or legacy snapshot -> review.
These labels apply only to evaluated rules, not overall package/take completion.

The coordinator supplies immutable snapshot bytes and a source/role assignment
projection. The internal evaluator checks schema, canonical bytes, content hash,
snapshot/package binding, source ID, role existence/uniqueness and source kind.
It does not establish approval authenticity, whole-session source assignments,
protocol catalog identity or all session semantics. PROTOCOL_ADMISSION remains
unassessed. Injected decoded observations retain DECODER_PROVENANCE unassessed.
The asynchronous adapter copies bounded snapshot bytes before awaiting decode.

## Clock-model continuity

For each frame stream and each independent IMU sensor lane, segment change,
native timestamp regression or sequence regression creates a barrier. A model
already used before that barrier may not be reused afterward, including when
unmapped records separate the uses. Segment IDs may not reappear within a lane.
The same native clock/model pair may not span different declared segments even
across streams. New valid models allow recovery; raw records are never changed.
Interleaving timestamps from different IMU sensors is not a regression test.

This proves consistency of supplied declarations, not detection of an unreported
physical restart/epoch change. With no mapped records, mapped-model continuity
remains unassessed. Schema clock/epoch identity and model-range checks remain
in force. No hardware synchronization or complete acquisition guarantee follows.

Rejected or review-required data remains transferable/preservable. No source
cleanup, commit receipt, verification record or host activation follows from
these internal results. Existing quality record vocabulary remains unchanged;
mapping these rule results into versioned records is a later integration gate.
