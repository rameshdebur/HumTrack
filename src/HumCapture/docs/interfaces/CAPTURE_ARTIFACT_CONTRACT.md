# HC-IF-ART-001 — Archived events and finalization summary

Version 1.0.0. Accepted C14A engineering baseline, 2026-09-15.
Schemas: schemas/capture/v1/. Decision: ADR-0027.

New packages declare HC-IF-ART-001@1.0.0 in interface_profiles. Exactly one required
CAPTURE_EVENTS artifact and one required FINALIZATION_RECORD artifact use JSON
format_version 1.0.0 and application/json. Paths remain manifest-selected; they
are not guessed from file names. Source, boot, session, trial, capture attempt,
package and source-kind identity must agree across archive, summary and manifest.

Both files are RFC 8785 canonical UTF-8 JSON, no BOM, whitespace suffix or duplicate
keys. Schema integer values are restricted to safe JSON integers; native ticks
remain exact uint64 decimal strings. Reject unknown required formats and fields.

## Archive projection

The archive contains source event identity, sequence, event type, native source
time, prior/resulting state, resulting revision and applicable reason. It is a
new archival projection of source-event facts, not an interchangeable control
wire payload or authoritative-state snapshot. Producers must preserve these
fields exactly from source authority; never manufacture events from nominal FPS.
The schema omits nested package inventory/content hashes to avoid recursion.

COMPLETE means the recorded capture window includes FIRST_MASTER_SAMPLE through
FINALIZATION_RESULT with no missing events inside that window. Earlier preparation
events may also be included; the first sequence need not equal one. Sequences
increase, IDs are unique, clocks/frequencies stay stable within the boot-scoped
archive, native ticks do not regress, and adjacent states agree for COMPLETE.
State changes advance revision; non-state events may retain a revision.
PARTIAL requires a reason; gaps are reported, not synthesized. A complete package
cannot claim a partial archive. Reboot/clock discontinuity needs separately scoped
evidence rather than a fabricated continuous timeline.

Only the final event may be FINALIZATION_RESULT, from FINALIZING into the summary
outcome, with matching event ID/reason. Complete finalization requires exactly one
FIRST_MASTER_SAMPLE; a terminal-event-only archive is insufficient. Multiple
first-master events require separate capture attempts, not silent merging.

## Summary and publication

The summary binds event_archive_artifact_id and its byte SHA-256, terminal_event_id,
outcome/reason, identities and finalized_utc. Outcome and audit UTC text match the
manifest; an incomplete reason also matches manifest.finalization_reason.
Complete summaries still explain the normal stop, but the existing complete
manifest continues to omit finalization_reason. No binary/master counts or FPS
are duplicated into this summary; their authoritative evidence remains separate.

Publication order: closed source artifacts -> archive -> summary -> manifest.
The archive does not reference summary/manifest hashes; summary references archive
only; manifest hashes both. Published artifacts remain immutable.

## Evidence boundary

capture-artifact-conformance.js checks schemas, canonical bytes, manifest binding,
event order/native time and terminal consistency using the fixed schema validators.
Its result is NOT a package verification record and cannot produce VERIFIED.
The full C14 verifier must also validate binary frame/IMU streams, video/frame
association, metadata profiles and full decode. Event assertions alone do not
prove recorder closure, sample acquisition, absence of video corruption or quality.
Fixed/variable rates and missing UVC IMU remain governed by HC-IF-TIM-001, unchanged.

Historical packages without this profile remain historical and require their own
supported verifier. Missing/unsupported evidence must not be treated as PASS.
