# ADR-0034: Internal finalized-package input admission

Accepted 2026-09-19 for user-approved C14O-Q. Primary Coordinator/Repository
engineer; affected Architect, Timing, Android/UVC, QA and Release/SBOM.
Existing ARD/PRD/SRS, roles/governance, preliminary India baseline, XFR/TIM/ART
contracts and HC-RISK-022/030/031 apply. No changed intended use or medical claim.

Implement an internal local-folder adapter, not journal admission or a host
endpoint. Validate the embedded manifest schema, canonical content hash,
inventory, profiles, role/version/media/path bindings, and file lengths/hashes.
Reuse existing schema, canonical subset, path rules and scientific validators.
Keep historical/unsupported profiles explicit; do not infer a timing version.
Incomplete packages may supply only surviving artifacts; missing scientific
groups stay unassessed. Optional unimplemented artifacts are hashed and reported
unassessed; required unimplemented semantics are unsupported.

Read-only file handles deny writing/deletion until evaluation finishes. Windows
directory handles pin the local ancestor/package paths against rename/delete;
inspect handle attributes and link count. Re-enumerate the file set before
returning. Directory sharing does not forbid creation: added files are detected
at the final snapshot, not prevented forever. No guarantee against privileged
raw-disk writes or changes after lease disposal is claimed.

Stream hashes for videos; retain bounded metadata/binary arrays only. Envelope:
16 MiB manifest/JSON, 256 artifacts, 256 package directories, existing binary
reader record limits, 128 MiB aggregate buffered sidecars. Exceeding limits is
unsupported, not truncation. Revisit limits using representative capture evidence.

Trade-off: a bounded read lease plus final inventory check is simpler than an
additional copied repository or service. It can temporarily block source edits
and cannot attest to media-decoder provenance. Decoded observations remain a
separate internal input in this batch; actual decoder connection is C14R-T.
No verifier record, VERIFIED, transactional admission, commit, receipt, cleanup,
or host activation. Protocol acceptance and scientific quality remain separate.
