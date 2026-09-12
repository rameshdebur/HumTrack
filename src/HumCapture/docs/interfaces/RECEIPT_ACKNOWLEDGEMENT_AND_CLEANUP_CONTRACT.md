# HumCapture Receipt Acknowledgement and Cleanup Contract

**Contract ID:** HC-IF-RCP-001  
**Version:** 1.0.0  
**Status:** Accepted engineering baseline  
**Date:** 2026-09-07

## 1. Boundary and authorities

This contract begins only after the common verifier has passed and the
coordinator has durably committed the exact package. That commit—not receipt
delivery, acknowledgement, or source deletion—satisfies the package part of
session completion. Cleanup failure never reverses a completed session.

The repository is authoritative for commit and receipt creation. Android is
authoritative for durable receipt storage and source deletion results. The
trained operator is authoritative for selecting eligible packages and
confirming cleanup. Records are carried over the existing HC-IF-SEC-001 mutual
TLS channel; the MVP adds no receipt signature, cloud service, scheduler, or
background deletion.

## 2. Immutable receipt

There is one revision-1 receipt for one exact commit/package-content hash. It
binds receipt, commit, coordinator, device, session, trial, source, attempt and
package IDs; package-content and artifact-set SHA-256 values; repository-relative
path; schema version; and issue time. It contains no subject name, date of
birth, reusable credential, token, signature, or master content.

An identical replay is idempotent. Reuse of receipt ID, commit ID, or the same
package/hash binding with different identity, hash, destination, or issue data
is a blocking conflict. The coordinator records recovery-required status and
Android retains source files. Receipts cannot be superseded or regenerated to
resolve uncertainty.

## 3. Android acknowledgement

Android compares the incoming receipt with its exact finalized local package,
then durably stores it before returning `ACCEPTED`. Repeated delivery returns
`ALREADY_ACKNOWLEDGED`. Any identity or hash mismatch returns
`REJECTED_MISMATCH`, does not store the receipt, enters recovery, and retains
the package. An acknowledgement means the receipt is durably stored and
matched; it does not mean that any file has been deleted.

If the coordinator loses an acknowledgement, it first queries Android using
the exact receipt identity. `NOT_STORED` causes replay of the identical receipt;
`STORED_ACK_PENDING` requests the acknowledgement again; `ACKNOWLEDGED`
reconciles the existing acknowledgement. None creates a new receipt, repeats
repository commit, or re-verifies committed bytes.

## 4. Cleanup eligibility and operator action

The normal online progression is:

```text
RECEIPT_ACKNOWLEDGED -> SAFE_TO_DELETE -> DELETED_FROM_SOURCE
```

The coordinator shows only eligible packages and displays device, package
count, aggregate size, capture-finalized date, and committed date. The trained
operator explicitly selects one or more packages and gives one confirmation.
The request binds every package ID, content hash, receipt ID/revision,
acknowledgement ID, source ID, size, dates, and exact artifact paths.

Android rechecks its durable receipt and local package immediately before
deletion. It rejects a selected package while capture, finalization, transfer,
or recovery is active. Unpairing never deletes data. There is no automatic,
scheduled, age-based, or background cleanup.

## 5. Results, partial recovery, and manual fallback

Android returns exactly one result per requested package:

- `DELETED`: no requested artifact remains.
- `PARTIAL_DELETE`: remaining artifact paths and a reason are recorded.
- `NOT_FOUND`: absence is reported, not re-labelled as successful deletion.
- `REJECTED_NOT_SAFE`: eligibility changed or local evidence is insufficient.

A partial retry may address only the reconciled remaining paths under the same
receipt/package identity. Deletion history remains durable on both sides.
Damaged or missing receipt evidence never permits deletion.

USB/MTP remains a network-failure fallback using coordinator verification and
durable commit. For the MVP it does not convey trusted acknowledgement back to
Android and therefore cannot authorize programmatic deletion. The coordinator
may present the same verified package list for an informed manual device
cleanup; that manual act and its outcome are recorded separately and never
fabricated as an Android acknowledgement.

## 6. Compatibility and evidence boundary

HC-IF-CTRL-001 is additively advanced to version 1.3.0. The five new JSON
Schema 2020-12 records remain schema version 1.0.0. Existing historical
receipt records without the newly defined optional `device_id` continue to
parse, but cannot enter the I0.3C acknowledgement/cleanup flow; new I0.3C
receipts require it through semantic conformance. Records are not rewritten.
Unknown required semantics and unsupported major
versions fail closed with source data retained.

HC-RCP-TEST-001–010 are source-level conformance evidence only. Android
persistence/deletion, coordinator catalog/repository integration, restart and
power-loss behavior, USB manual workflow, HIL, field use, independent review,
and qualified regulatory review remain open.

## 7. Normative technical references

- AsyncAPI Specification 3.1.0: https://www.asyncapi.com/docs/reference/specification/v3.1.0
- JSON Schema Draft 2020-12: https://json-schema.org/draft/2020-12
- RFC 8785 JSON Canonicalization Scheme: https://www.rfc-editor.org/rfc/rfc8785
- HC-IF-XFR-001, HC-IF-SEC-001, and ADR-0014.
