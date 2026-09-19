# HC-I0.4B-B1 — Repository namespace

**Tags:** I0.4B-B1 | REPOSITORY | NAMESPACE | UUID | PATH-SAFETY | WINDOWS | CONTRACT

## Goal

Baseline a shallow, deterministic and privacy-preserving repository namespace
without changing the exact verified contents of committed source packages.

## Classification and ownership

- Change: cross-component interface, architecture, persistence, privacy and
  compatibility baseline.
- Primary owner: System Architect / repository owner.
- Affected owners: Coordinator, transfer/verifier, Android/UVC package
  producers, recovery, backup/export, HumTrack handoff, QA and risk/regulatory.
- Verification owner: engineering QA for later executable schemas/path tests;
  independent human reviewer remains pending.
- Allowed paths: `src/HumCapture` documentation only.
- Application implementation: not authorized.

## Approved decisions

- [x] `repository.json`, `catalog/`, `staging/`, `quarantine/`, and `subjects/`
  are the version-1 root namespace.
- [x] Committed packages use
  `subjects/{subject_id}/sessions/{session_id}/packages/{package_id}/`.
- [x] Staging and quarantine use their own operation UUID plus package UUID.
- [x] Every identity directory segment is a canonical lowercase UUID.
- [x] Subject names/demographics remain required and Coordinator-local, never
  path authority.
- [x] Trial/source/attempt identities remain in manifest/catalog rather than
  increasing directory depth.
- [x] The package envelope remains byte/path exact and receives no repository
  metadata.
- [x] Same-root/same-volume and fail-closed Windows path/link rules apply.
- [x] Existing destinations are never overwritten; unexpected entries are not
  silently deleted.

## Deferred boundaries

Subject/session operational-record placement, JSON/SQLite schemas, journal
transitions, compatibility windows, executable path/crash fixtures, runtime,
backup/restore, retention, HIL/field, independent review, qualified regulatory
review and controlled release remain open.
