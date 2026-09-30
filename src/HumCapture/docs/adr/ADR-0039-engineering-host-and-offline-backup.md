# ADR-0039: Engineering host and offline repository backup

2026-09-27. Engineering design baseline under user-approved C15A-C.
Primary Coordinator/Repository engineer and Architect; QA owns verification;
affected capture, risk and release/SBOM owners. Independent human review open.
Existing ARD/PRD/stories/SRS/roles/governance/India baseline apply. No changed
intended use or medical claim; HIL readiness is audited separately.

Expose C14X-Z via explicit engineering-only host commands, requiring
--engineering true. Reuse the existing per-root host mutex and Windows account;
retain the workflow file lock. Canonical versioned request files are bounded
and locally leased. No caller-supplied actor, decoder results or forced PASS.
The decoder remains an explicitly selected, hash-pinned engineering binary;
runtime_enabled/redistribution approval are not changed. Legacy CLI stays intact.

Choose a quiescent, full-directory backup rather than live/incremental backup.
Hold the host guard and workflow guard, lease every included file against
write/delete, pin directories, and compare inventory before publication.
Reject SQLite journal/WAL/SHM files: reconcile/close writers first. Include both
databases, descriptor, staged/committed captures, records and empty directories;
exclude only the ephemeral workflow lock. Bound inventory to 10,000 entries.
Backup requires already initialized compatible workflow/custody ledgers.
This is a cooperating-host maintenance operation, never part of live capture.

Use a version-1 canonical inventory of relative paths, lengths, SHA-256,
repository ID, UTC and signed-in account. Stream bytes and rehash the copy.
Publish the entire completed backup with a same-parent directory rename.
Restore verifies exact inventory and hashes and both database identities into
a new sibling temporary directory; validate, then rename to an absent target.
Never overwrite a repository or remove originals. Interrupted temporary copies
are retained as incomplete, never accepted as published backups. No automatic
retry cleanup. Ordinary local Windows volumes only; no cloud destination.

Trade-off: offline maintenance and a bounded file count are simpler and testable,
but unsuitable for concurrent capture or very large repositories. Revisit for
live backup only with a cross-writer snapshot protocol. Hashes detect damage;
they are not signatures or proof against privileged coherent tampering.
Restored clones preserve repository IDs and must remain isolated until an
explicit operator replacement decision. Backup is not clinical/release approval.

Test actual child-process termination at controlled durable boundaries via a
self-test child harness, not a production crash switch. Distinguish killed
process recovery from power-loss/OS-failure qualification. Audit physical UVC
enumeration, available acquisition binaries, package bridge, host and UI gaps
before proposing a supervised, short, nonclinical HIL session.
