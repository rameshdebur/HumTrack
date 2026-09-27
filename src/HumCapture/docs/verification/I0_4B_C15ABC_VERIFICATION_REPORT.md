# HC-VR-I0-4B-C15ABC-001: engineering host, backup and process recovery

Date 2026-09-27. User-approved C15A-C and post-batch HIL-readiness audit.
ADR-0039 / HC-IF-EHB-001; AI-assisted implementation and verification.
Independent human QA/regulatory review remains open. Scope only HumCapture.

## Outcome and acceptance boundary

C15A: explicit --engineering true commands expose repository/workflow init,
canonical assignment, collection/verification, history and recovery. Existing
host mutex/account and workflow lock are retained. Null/malformed inputs refuse
with structured output. Legacy CLI and decoder production/distribution flags
are unchanged. This is an engineering CLI, not a trained-operator product UI.

C15B: quiescent full repository snapshot preserves both databases, all present
capture/record files and empty directories; ephemeral workflow lock excluded.
File leases deny write/delete; directory pins and repeated exact inventory
checks detect changes. SQLite journals/WAL/SHM, unsafe links, overlapping roots,
writers and existing destination refuse. Every copied file is rehashed; both
databases are integrity/identity checked, workflow payload hashes checked.
Manifest records exact inventory, Windows account, UTC and repository ID.
Backup/restore publish by same-parent directory rename only after verification.
Interrupted temporary copies remain incomplete, never overwrite originals and
are not automatically deleted. A restored clone must remain isolated.
This preserves existing state, including failures/incomplete staging; it does
not repair pre-existing missing evidence or approve scientific completion.

C15C: self-test child processes are actually killed after durable STARTED,
VERIFIED_READY and catalog-admission checkpoints and during backup/restore.
Recovery is invoked through a fresh executable host process. This demonstrates
OS-released handles and process-restart recovery, not physical power-loss or
disk-removal resilience. The crash checkpoints are self-test-only callbacks,
not production host command-line switches.

## Retained results

| Evidence | Result | Output |
|---|---|---|
| Build/static | Release, zero warnings/errors | I0_4B_C15ABC_BUILD_RESULTS.txt |
| Full automated runtime | 215/215 | I0_4B_C15ABC_RUNTIME_RESULTS.txt |
| Contract regression | 115/115 | I0_4B_C15ABC_CONTRACT_RESULTS.txt |
| SBOM/register | 13/13 plus project inventory/hash checks | I0_4B_C15ABC_SBOM_RESULTS.txt |
| Actual host and pinned decoder | Synthetic video through final commit, backup, isolated restore, committed reconciliation | I0_4B_C15ABC_REAL_RESULTS.txt |
| Physical device preflight | PnP presence and Media Foundation advertised modes; no recording | I0_4B_C15_HARDWARE_ENUMERATION.txt |
| Hosted validation | Push/PR runs bound to containing source commit | GitHub HumCapture validation |
| Capture HIL / field / clinical / regulatory | Not performed in this batch | HC-AUD-C15-HIL-001 readiness only |

New runtime groups:

- 211: actual host registration/run/history/recovery, existing commit path,
  backup/restore with matching identity/history and committed reconciliation;
  overwrite, missing engineering opt-in, unknown option and host-busy refusal.
- 212: preserve failed verification in restored history; overlap, open writer,
  SQLite journal and altered backup bytes refused without publishing a restore.
- 213: actual process kill at all three workflow boundaries; STARTED becomes
  INTERRUPTED, retained verified/admitted boundaries recover exactly.
- 214: killed backup after a copied file cannot publish completion; temporary
  source refused; new backup succeeds. Killed pre-publication restore leaves
  requested target absent; fresh restore retains admission history.
- 215: malformed null assignment/request/backup inventory return structured
  refusal; new repository/workflow init works; pre-cancelled backup unpublished.

Original groups 001-210 remain passing. Build findings (test alias collision,
required mutex-finally cleanup and analyzer simplifications) were corrected;
only final passing output is acceptance evidence. No physical scene, exposure,
frame cadence, LED or hardware synchronization acceptance is inferred.

## Reproduction

Build apps/windows-coordinator/tests/HumCapture.Coordinator.Repository.SelfTest
in Release with --no-restore; run --no-build for full suite, --maintenance for
211-215. --maintenance-real <absolute pinned bin> <absolute media directory>
uses retained synthetic master3.mp4 and engineering-pinned FFmpeg/ffprobe 9.0.1.
Run npm test --prefix tools/sbom and npm run test:contracts --prefix
tools/evidence-control. New host command contract is HC-IF-EHB-001.
Test repositories and partial-copy experiments use owned unique directories
under HumCapture/evidence-vault/workflow-tests; fixtures are cleaned by tests,
not by production recovery. No user repository backup or restore was performed.

## Supply chain and remaining gates

SBOM 0.1.0-i0.4b-c15c, generated 2026-09-27T18:18:49Z, 37 components plus
product / 38 dependency nodes. SHA-256:
`3b9e65c4c8657017ccf347c8ed1f7866a552632bb54e3a16d248f393cf90f347`.
No dependencies added. Licence register refreshed for inventory consistency;
no new legal/price approval or official CLI format audit performed locally.
Published JsonSchema.Net remains 9.4.0; decoder redistribution remains unapproved.

Architecture skill review selected offline bounded maintenance over live backup.
The 10,000-entry limit, full-copy storage cost, same-volume publication boundary,
cooperating-host exclusion and clone-isolation requirement are deliberate MVP
limits. Live capture backup, scale, physical power-loss, controlled release,
independent review, clinical and Indian regulatory acceptance remain open.
HIL audit recommends the single-UVC acquisition/package vertical slice next.
