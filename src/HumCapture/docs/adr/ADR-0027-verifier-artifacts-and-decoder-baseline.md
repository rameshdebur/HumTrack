# ADR-0027 — Verifier artifacts and controlled decoder baseline

Status: Accepted for user-authorized C14A engineering; independent review pending.
Date: 2026-09-15. Parent: HC-CHG-20260915-002 / approved C14 objective.

Primary owner: System Architect/Coordinator engineer. Affected: Android, UVC,
QA, Risk/Regulatory and Release/SBOM. Verification owner: engineering QA.
Classification: additive versioned package artifacts and planned runtime dependency.
Existing requirements, roles, governance and preliminary India engineering baseline
apply. No new clinical, synchronization or conformity claim.

## Decisions

HC-IF-ART-001 1.0.0 defines two canonical JSON artifacts: a capture-event archive
and finalization summary. They use existing identity, source-state and native-time
definitions, but the archive is a projection, NOT a full control wire message.
Wire terminal snapshots include package hashes: embedding them in their own
hashed package would create a circular dependency. The archive excludes snapshots
and package/content hashes. The summary binds the archive's ID/hash; the manifest
binds both. No file contains its own hash or the resulting package-content hash.

Complete finalization requires a complete contiguous archived capture window,
one first-master event and the exact terminal finalization event. Partial archives
retain explicit reasons and cannot prove complete finalization. These checks do
not establish actual media decode, sample continuity, quality or timing accuracy.
Required scientific checks still need the independent runtime verifier.

Dispatch new artifacts by role, format_version 1.0.0, application/json and profile
HC-IF-ART-001@1.0.0. Historical fixtures/packages are not rewritten or silently
assigned this profile. Existing contracts and C13 admission boundaries remain.

Select the Gyan FFmpeg 9.0.1 Windows x64 essentials archive as the planned
engineering decoder candidate, with exact versioned URL and publisher SHA-256 in
decoder-lock.json. Runtime remains disabled; archive bytes, executable hashes,
build configuration, embedded libraries and decode behavior need validation before
activation. Existing PATH FFmpeg 7.1.1 is not promoted or modified. SBOM explicitly
marks the archive excluded/planned, not installed or shipped.

The publisher declares GPLv3. Retain legal/licence review and redistribution
approval as open gates, not automatic consequences of subprocess isolation.
No installer, download, PATH change, binary redistribution or decoder execution
is introduced by this contract slice.

## Alternatives and trade-offs

- Full wire event archive: rejected because terminal inventory creates a hash cycle.
- Windows Media Foundation-only verifier: avoids a new distributed decoder bundle
  but needs separate decode/error/format qualification; retained as an alternative.
- Arbitrary PATH FFmpeg: rejected because the tool identity cannot be reproduced.
- Automatic VERIFIED after artifact checks: rejected; hashes and event assertions
  do not prove decoding or scientific metadata correctness.

Revisit decoder choice if licensing, supported hardware/OS, codec or verification
evidence fails. The runtime worker must be bounded/cancellable, read-only, use
software decoding initially, and report NOT_ASSESSED/failed for unsupported checks.
