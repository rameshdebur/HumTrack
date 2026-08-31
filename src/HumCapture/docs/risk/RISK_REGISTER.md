# HumCapture Preliminary Risk Register

All entries are `Open / not implemented / not verified` unless later evidence says otherwise. Qualitative ratings are deferred until intended use and risk method are professionally reviewed.

| Risk ID | Hazard or hazardous situation | Principal controls | Requirement links |
|---|---|---|---|
| HC-RISK-001 | Capture associated with wrong subject/session/trial | UUIDs, confirmation, manifest identity, no manual destination, audit | HC-DATA-REQ-001/003, HC-SEC-REQ-005 |
| HC-RISK-002 | Incomplete capture presented as complete | Required-source/trial state, verification before commit, recovery state | HC-DATA-REQ-002/004, HC-COORD-REQ-004 |
| HC-RISK-003 | Silent frame/timestamp/metadata loss | Sequence/timestamps, discontinuity detection, quality report | HC-TIME-REQ-005, HC-AND-REQ-001 |
| HC-RISK-004 | Incorrect temporal alignment misleads downstream analysis | Raw exchanges, uncertainty, provenance, thresholds/deviation | HC-TIME-REQ-001–005 |
| HC-RISK-005 | Wrong lens/calibration/camera movement invalidates geometry | Physical lens identity, profile matching, IMU movement, explicit uncalibrated state | HC-SYS-REQ-003, future calibration requirements |
| HC-RISK-006 | Preview/UI/network disrupts scientific master | Separate bounded paths, foreground/worker capture, resource priority | HC-SYS-REQ-002/005, HC-AND-REQ-002 |
| HC-RISK-007 | Premature Android deletion causes data loss | Commit receipt, safe/incomplete separation, explicit confirmation | HC-AND-REQ-005, HC-DATA-REQ-002/004 |
| HC-RISK-008 | Storage, thermal, battery, or USB contention interrupts capture | Preflight, max duration, monitoring, soak/capability tests | Future phase-specific requirements |
| HC-RISK-009 | Unauthorized coordinator/device or replayed command | Pairing, pinned trust, authenticated control/transfer, replay protection | HC-SEC-REQ-001/003/004 |
| HC-RISK-010 | Malicious/corrupt package overwrites valid data | Path validation, identity/hash, idempotency, quarantine, transactional commit | HC-DATA-REQ-001–003 |
| HC-RISK-011 | PII exposed through discovery, phone, logs, or export | Data minimization, coordinator-local identity, redaction, explicit export | HC-SEC-REQ-002/004/005 |
| HC-RISK-012 | Operator misinterprets preview, completion, quality, or override | Guided workflow, distinct states, reasons, training, accessibility | HC-USE-REQ-001–004, HC-COORD-REQ-004 |
| HC-RISK-013 | Unsupported OS/device/driver presented as qualified | Capability/version negotiation, compatibility matrix, named evidence | HC-COMPAT-REQ-001–003 |
| HC-RISK-014 | Unsupported medical/certification claim | Claims register, evidence linkage, CDSCO gate, qualified review | HC-REG-REQ-001–004 |
| HC-RISK-015 | A documented camera in-use indicator does not illuminate during acquisition or remains illuminated after release, misleading the operator about camera/privacy state | Exact physical identity correlation, API-authoritative state, off/on/off qualification observation, other-process check, reconciliation or named-configuration rejection | HC-UVC-REQ-004, HC-USE-REQ-002/004 |
| HC-RISK-016 | Multiple required UVC sources open and report complete frame counts while one stream is corrupted by driver, USB, profile, or concurrency interaction | Protocol-scoped concurrent preflight, named topology/driver evidence, per-stream complete decode, no silent substitution, reject/reconcile combination, soak | HC-SYS-REQ-003, HC-UVC-REQ-005, HC-TIME-REQ-005 |
| HC-RISK-017 | Primary verification evidence is lost, altered, selectively retained, or cannot be bound to the tested source/build | Controlled non-temporary vault, complete hash inventory, conflict rejection, release identity, retention/backup, independent review and future signature/eQMS control | HC-DATA-REQ-001/003, HC-REG-REQ-003/004 |
| HC-RISK-018 | A vulnerable, malicious, unmaintained, unlicensed, substituted, or unidentified software component enters a build or remains undiscoverable during response | Locked dependencies, CycloneDX SBOM per release, component hashes/identifiers, dependency review, ecosystem audit, supplier/licence review, vulnerability monitoring and VEX/patch process | HC-SEC-REQ-006, HC-COMPAT-REQ-002/003, HC-REG-REQ-003/004 |
| HC-RISK-019 | Incorrect, insecure, fabricated, or insufficiently verified AI/automated output enters a controlled baseline or creates misleading evidence | Treat generated output as untrusted draft, material-use declaration, requirement/risk traceability, independent test oracle, human review, proportionate independent review, privacy/secret restrictions, exact release identity | HC-SEC-REQ-004/006, HC-REG-REQ-003/004; HC-SOP-SW-004 |

## Current Phase 0 evidence notes

- **HC-RISK-003 / HC-RISK-013, 2026-08-31:** The P0.2B diagnostic kept
  requested, negotiated, nominal encoded, and measured rates separate. One
  named C920 delivered approximately 26 fps and then 24 fps after exact 30/1
  negotiation, while the other delivered approximately 29.92 fps. The first
  camera remains unaccepted; no compatibility or qualification claim is made.
- **HC-RISK-015, 2026-08-31:** HC-P0-UVC-003 passed on both named C920
  configurations. Each selected camera produced two consistent off/on/off
  indicator cycles correlated to fresh frames and source release, while the
  non-selected unit remained off. This does not create a universal indicator
  requirement or a timing claim.
- **HC-RISK-016, 2026-08-31:** Two concurrent C920 runs opened and finalized
  both streams, but C920 A contained H.264 decode corruption both times while
  C920 B remained clean. The named concurrent configuration is not approved.
- **HC-RISK-016 follow-up:** Reverse startup order did not move the failure.
  Swapping physical cameras between downstream paths 2 and 3 moved corruption
  to the camera on path 2 in both repeats. The current connection combination
  is rejected; neither camera body/fixed cable is identified as the primary cause.
- **HC-RISK-003 / HC-RISK-008 / HC-RISK-016 reconciliation:** Moving Camera B
  to direct root-port branch `USBROOT(0)#USB(3)` removed the prior corruption in
  observed short runs, but automatic exposure produced approximately 15 fps
  both concurrently and alone. With Camera B temporarily held at exposure `-5`,
  it passed alone and both cameras passed two concurrent 30-second full-decode
  runs at approximately 30 fps. Automatic exposure was restored and independently
  confirmed. This is a conditional short diagnostic, not qualification; scene
  range, exposure policy, soak, recovery, and topology persistence remain open.
- **HC-RISK-003 / HC-RISK-008 / HC-RISK-016 stability follow-up:** The exact
  reconciled P0.2H configuration passed a 10-minute concurrent run with 18,002
  fully decoded frames per camera, approximately 30.0002 fps, no decode errors,
  and no timestamp regressions. All sampled power states were on AC and Camera B
  automatic exposure was independently confirmed after cleanup. The duration
  was deliberately proportionate to the MVP diagnostic; it is not the normative
  30-minute 1080p60 qualification soak, and recovery remains open.
- **HC-RISK-002 / HC-RISK-003 / HC-RISK-008 / HC-RISK-012 recovery follow-up:**
  P0.2J live-disconnected exact Camera A during acquisition. Media Foundation
  returned raw HRESULT `0xC00D3EA2`; the probe finalized and fully decoded all
  265 received frames while marking requested duration false/read error. The
  ordinary verifier rejected false normal completion and Windows confirmed the
  exact interface absent. Overall recovery remains `INCONCLUSIVE` until the same
  identity is reconnected and a separate post-reconnect artifact passes.
- **HC-RISK-017 initial control, 2026-08-31:** ADR-0007 and evidence-control
  evidence receipt 1.0.0 and SBOM-aware release record 1.1.0 are implemented.
  Twelve automated tests cover successful
  import, idempotency, conflicting run identity, artifact/release tampering, and
  malformed/unexpected records, and vault-boundary rejection. P0.2J raw evidence is retained and verifies as
  `HC-EV-5becff40f4271131c8b43641`. Residual risk remains high enough to block a
  dossier claim because backup/restore, access audit, independent approval,
  signature/WORM/eQMS controls, and an approved clean release are open.
- **HC-RISK-018 initial control, 2026-08-31:** ADR-0008, SBOM policy
  HC-GOV-SBOM-001 and a deterministic CycloneDX 1.7 generator now cover both
  npm lockfiles, the managed and native probes, target frameworks, Windows
  runtime APIs and build SDK. Four project tests, including future-manifest
  drift detection, pass. This inventory does not
  replace vulnerability, licence, maintainer/supplier, binary composition or
  runtime module analysis; those reviews remain open.
  The generated 15-component/16-node CycloneDX 1.7 SBOM passed project checks
  and official CycloneDX CLI 0.33.1 validation and is bound to the engineering
  snapshot. This reduces inventory uncertainty but does not close the risk.
- **HC-RISK-019 governance control, 2026-08-31:** HC-SOP-SW-004 defines a
  tool-neutral, risk-based workflow for material AI/automation use, human
  accountability, independent test oracles, review, traceability, and release.
  The control is documented but not operationally verified: HumCapture remains
  untracked, no protected review workflow or CI is approved, and no independent
  release review exists. The risk remains open.
