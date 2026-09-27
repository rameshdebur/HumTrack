# HC-IF-EHB-001 version 1.0.0

Baseline 2026-09-27, ADR-0039, C15A-C. All new commands require --engineering
true and --root <absolute local root>. Existing commands remain unchanged.

- repository-init: explicitly initialize a new empty engineering repository.
- workflow-init: explicitly create the workflow ledger in an existing repository.
- workflow-assign --request <canonical CaptureAssignment JSON>.
- workflow-run --request <canonical BoundVerificationRequest JSON>
  --source <absolute finalized-package directory> --decoder <absolute pinned bin>.
- workflow-history --attempt <UUID>: bounded first 100 events, historical only.
- workflow-recover --attempt <UUID>: exact C14X-Z recovery, no source cleanup.
- repository-backup --destination <absent absolute backup directory>.
- repository-restore --source <published backup>: root is the absent restore target.

No unknown/duplicate options, implicit initialization or production decoder
enablement. JSON results schema_version 1.0.0; exit 0 success, 2 usage, 3 refusal,
4 retained failed/interrupted attempt, 7 cooperating host busy, 130 cancellation.
Output includes state/reason/next-action and verification hash, not video bytes.
Usage/refusal envelopes reuse the existing host schema_version 1.2.0; new
successful operation/workflow envelopes use 1.0.0 as above.

Backup envelope contains payload/ and backup-manifest.json. Manifest DTO:
schema_version, repository_id, created_utc, windows_account, directories,
files[{path,length,sha256}]. All paths are canonical relative paths; exact
case-insensitive-unique inventory, no links or extra files; maximum 10,000
entries and 32 MiB manifest. Only catalog/verification-workflow.lock is omitted.
Both databases required; no SQLite journal/WAL/SHM accepted. Copy retains full
repository state, including incomplete staging, without promoting it to success.

Source root and destination must not overlap. Temporary siblings have unique
.partial-UUID suffixes and are retained on failure. Only the requested published
directory is accepted; temporary suffixes are refused as backup sources.
Originals remain untouched, except ordinary cooperative lock-file creation.
Restore creates an isolated clone, preserving identity; never operate both
clones as the same production repository. No encryption/signature, scheduled
backup, live-capture backup, migration or overwrite is implied.
