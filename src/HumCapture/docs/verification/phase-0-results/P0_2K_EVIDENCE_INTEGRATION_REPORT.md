# P0.2K Windows/UVC Evidence Integration Report

**Report ID:** HC-P0-VR-002K  
**Date:** 2026-09-01  
**Disposition:** Integration complete; P0.2 engineering findings preserved, normative qualification remains open  
**Tags:** P0.2K | UVC | EVIDENCE | INTEGRATION | TRACEABILITY | RETENTION | GAP-ASSESSMENT

## 1. Purpose and decision

This report consolidates the P0.2A-J Windows/UVC diagnostic history and records
which evidence remains independently machine-verifiable. It closes the planned
P0.2 evidence-integration activity; it does not close the Phase 0 hardware
qualification gate.

The integrated evidence supports continued interface design and prospective
probe work. It does not support a generic UVC compatibility claim, normative
1080p60 qualification, production recovery, a controlled release, or a
regulatory/conformity claim.

## 2. Integrated engineering reports

The following SHA-256 values identify the exact report files reviewed for this
integration. These reports are controlled diagnostic summaries. A report is not
a substitute for its primary raw capture, telemetry, configuration, or lifecycle
records.

| Increment | Report ID | Recorded disposition | Report file SHA-256 |
|---|---|---|---|
| P0.2A | HC-P0-VR-002A | Named-device API spike passed; capture gate remained open | `76805f9ce35a7f26d4933f15f983c9f73d44a72f249a750d1ffb95c4bbc3187a` |
| P0.2B | HC-P0-VR-002B | Source implemented; named-camera cadence acceptance blocked | `2a8c95263335c3017f3867fef043eaaa65ea97e7acfd7d12cea4a0f05400febe` |
| P0.2C | HC-P0-VR-002C | Indicator behavior passed for two named C920 configurations | `b4d613e21efa886fc7706999cdd7e049641984e3c19486a9e930cdd83faeec08` |
| P0.2D | HC-P0-VR-002D | Concurrent opening demonstrated; media-integrity acceptance failed | `8cd8367435d389ff01c3dd4afa380fb6e0250d0c6e267392d1103b25f877a217` |
| P0.2E | HC-P0-VR-002E | Start-order hypothesis not supported | `59797dd591df6d98de304c1e98d19fb3166e0eb2ce01552042d99318144dc461` |
| P0.2F | HC-P0-VR-002F | Corruption followed rejected downstream USB path 2 | `dfc5a4515fe0f1c86be2c6cfc8e957d27f2f3d5b5177da03311a22a665ef4770` |
| P0.2G-H | HC-P0-VR-002GH | Conditional short-run pass after topology/exposure reconciliation | `187b36dd0e6f8021d7906e955276cf701f0ba76462d9b1f380b843199db81e68` |
| P0.2I | HC-P0-VR-002I | Conditional 10-minute stability pass | `107bf6a6368eca91d2a1401a51d0739c40f8fe43c699a9df47e574f62f1a6966` |
| P0.2J | HC-P0-VR-002J | Bounded 1080p30 recovery diagnostic passed; review remains draft | `a15b999aa03127f495a21e14047563f614e687160a65a60b290a50c8eb7d5cbe` |

## 3. Retained machine-verifiable primary evidence

Only the two P0.2J runs below remain in the controlled evidence vault. Full
vault verification passed on 2026-09-01. Both receipts bind to engineering
snapshot `HC-ENG-20260831T163246Z-7d17f5afce36`, release-record SHA-256
`756b8c7a36d2239501ee5401c29fae16d2648a178b32f286338ac8611674c269`,
and CycloneDX SBOM SHA-256
`2f34a8112c2c2ea274c4be7fd4d15443368feb4baa06c3e364dd7fae736f94e5`.

| Evidence ID | Source run | Disposition/review | Artifact-set SHA-256 | Receipt SHA-256 |
|---|---|---|---|---|
| `HC-EV-5becff40f4271131c8b43641` | `1858C775-7907-43B3-9A3C-59104285E826` | `INCONCLUSIVE` / `DRAFT` | `5becff40f4271131c8b43641b8b6db43409959cfd085e583ca0a2effc60efa5b` | `aebe2a5c5682d7f67769762f6c65baa3dc74f9893a07f6ba90472a439e0310f9` |
| `HC-EV-16e6576bbcf4f1896d64aac6` | `2D358446-A581-4DDA-92FA-92B5F71606A9` | `PASS` / `DRAFT` | `16e6576bbcf4f1896d64aac64fca1d5482a92a762fb59e5e1b44e5b9d4d1d3ca` | `5a42b4f1d7c2af42bf83f6a06d53ee6d833b22789d67b5afbf40f661cbd43964` |

The interrupted run retains 265 fully decoded frames plus its raw terminal
HRESULT and false-completion rejection. The distinct post-reconnect run retains
898 fully decoded frames at 29.9000 measured fps with zero decode errors and
timestamp regressions.

## 4. Evidence gaps and deviations

1. Primary raw evidence for P0.2A-I was deleted or was never imported. The
   versioned reports preserve results and selected hashes, but those runs cannot
   now be independently reprocessed from primary artifacts.
2. The two retained P0.2J diagnostic runs do not contain all measurement files
   required by the normative P0.1 UVC evidence contract, including the complete
   thermal/power/storage and machine-recorded lifecycle evidence set.
3. The physical reconnect was operator-observed and not independently
   timestamped by hardware or an operating-system device-event recorder.
4. P0.2J used exact H.264 1920x1080 at 30/1 because neither available C920
   reports the normative HC-P0-UVC-002 1080p60 profile.
5. Review state is `DRAFT`; independent QA approval, evidence backup/restore,
   trusted signing/WORM or validated eQMS controls, and a controlled released
   build remain open.

Missing evidence must be captured prospectively. It must not be reconstructed
from nominal container values, host-arrival time, recollection, or inferred
timestamps.

## 5. Integrated technical conclusions

- Exact UVC identity, exact native profile selection, measured source cadence,
  finalized media, and complete decoding are feasible through the isolated
  native Media Foundation probe.
- Camera compatibility is a property of the complete named configuration:
  camera, driver, profile, USB topology, controls/exposure, scene, and load.
- The original downstream path-2 concurrent configuration is rejected. The
  reconciled topology with temporary Camera B exposure `-5` is conditional MVP
  evidence only, despite its clean 10-minute run.
- A device-loss capture must remain incomplete even when its partial artifact
  finalizes and decodes. A reconnected device starts a distinct run/artifact.
- Physical in-use indicators are optional operator/privacy evidence and never
  timing evidence.
- No tested camera supports the normative 1080p60 UVC procedure.

## 6. Gate disposition and next controls

P0.2K evidence integration is complete at the engineering-document level. The
P0.2 hardware-qualification gate remains open because the normative profile,
complete prospective evidence package, independent review, and required
retention controls are not satisfied.

Before any future qualifying hardware run:

1. select hardware that reports the exact protocol profile;
2. pre-create the complete registry-bound evidence package and measurement
   collectors, including lifecycle and thermal/power/storage records;
3. retain every result, including failed/inconclusive attempts, directly in the
   controlled vault;
4. verify the package, release/SBOM binding, and full media decode before
   disposition; and
5. obtain independent review without rewriting immutable raw evidence.

The next software-design activity is the normative interface-contract baseline.
Production application implementation remains blocked until that baseline and
the applicable governance gate are approved.

**Prepared by:** Agent-generated engineering integration record  
**Independent reviewer:** Open  
**Qualified regulatory review:** Open where dossier or conformity use is proposed

