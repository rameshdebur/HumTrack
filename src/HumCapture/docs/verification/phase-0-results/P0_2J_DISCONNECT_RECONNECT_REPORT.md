# P0.2J UVC Disconnect/Reconnect Verification Report

**Report ID:** HC-P0-VR-002J  
**Test case ID:** HC-P0-UVC-002-DIAG-01  
**Revision:** 1.0 — technical execution complete  
**Date:** 2026-08-31 to 2026-09-01  
**Disposition:** `PASS` for the bounded 1080p30 diagnostic; independent review remains `DRAFT`  
**Operator:** `LAPTOP-RARRB8C4\rams2`  
**Tags:** P0.2J | UVC | RECOVERY | DEVICE-LOSS | FINALIZATION | RECONNECT | MEDIA-INTEGRITY | HARDWARE

## 1. Purpose and traceability

Verify the ARD/PRD intent that an interrupted source is not represented as an
uninterrupted successful master, already-received evidence is preserved where
possible, and a reconnected source begins a separate capture artifact.

| Trace type | References |
|---|---|
| User stories | HC-US-CAP-006–008, HC-US-CAP-012 |
| Requirements | HC-COORD-REQ-002/004, HC-UVC-REQ-002/003, HC-TIME-REQ-005 |
| Risks | HC-RISK-002/003/008/012/013 |
| Architecture/governance | ARD acquisition invariant; ADR-0006 native Media Foundation boundary; HC-P0-SPEC-001 sections 7, 9–11 |
| Normative procedure family | HC-P0-UVC-002 |

This report follows the project's controlled verification-record fields:
identified test, purpose, references, configuration, preconditions, procedure,
expected and actual results, errors/deviations, artifacts, disposition,
limitations, evidence level, and open actions. It is standards-aligned project
evidence; it is not a claim of ISO, IEC, CDSCO, or medical-device conformity.

## 2. Test objective

1. Start exact-profile H.264 acquisition from one exact UVC identity.
2. Disconnect that physical camera during fresh-frame delivery.
3. Record the terminal Windows/Media Foundation error without converting the
   interrupted attempt into a normal completion.
4. Attempt to finalize and fully decode already-received samples.
5. Confirm device disappearance.
6. Reconnect and rediscover the same identity.
7. Create and verify a separate post-reconnect artifact and run ID.

## 3. Controlled configuration

| Item | Recorded value |
|---|---|
| Host | Windows 11 Home Single Language, version `10.0.26200`, build `26200` |
| Camera | Logitech HD Pro Webcam C920 A, parent serial previously correlated as `0E1A0C0F` |
| Exact interface | `USB\VID_046D&PID_082D&MI_00\6&DBA5B52&2&0000` |
| Driver version | `1.4.40.0` |
| Requested/native profile | H.264, 1920x1080, 30/1 fps, native index 560 |
| Acquisition API | Native Media Foundation Source Reader/Sink Writer |
| Probe source SHA-256 | `CB8D8120DC399C2B8DEE4FF8F275748F18A19F5F35DDCE6C52D71305CB3D12B2` |
| Executed binary SHA-256 | `4B90835FA33B117DE0C0DC8D59B0CBA94645DC819A5B43DAEA06074A8368B8FD` |
| Data classification | Synthetic/non-subject diagnostic scene |

## 4. Preconditions and control test

- Native probe rebuilt with zero warnings/errors; 8/8 native self-tests passed.
- Managed probe rebuilt with zero warnings/errors; 7/7 managed self-tests passed.
- PowerShell verifier syntax passed.
- A 3-second no-disconnect control in recovery mode reached its requested
  duration, finalized, decoded 91/91 frames, and passed ordinary verification.
- Ordinary verification was changed to reject any recovery-mode artifact whose
  requested duration was not reached.
- Before the post-reconnect run, the native probes rebuilt with zero warnings
  or errors and the capture probe passed 8/8 self-tests. Current source and
  executable SHA-256 values matched the retained engineering snapshot.

## 5. Procedure and actual observations

| Step | Expected result | Actual result | Status |
|---|---|---|---|
| Select exact Camera A/profile | One unambiguous identity; exact negotiation | Exact interface and native profile 560 selected | PASS |
| Begin acquisition | Fresh encoded samples and active camera indicator | Samples delivered; operator disconnected the illuminated selected camera | PASS |
| Detect live device loss | Terminal event remains distinct from normal stop | `ReadSample` terminated with HRESULT `0xC00D3EA2`; requested duration not reached | PASS |
| Preserve partial attempt | Sink finalizes already-received samples where possible | Sink finalized 265 samples into a separate 8.833267-second MP4 | PASS |
| Decode entire partial artifact | No hidden H.264 corruption | 265/265 frames decoded; FFprobe and FFmpeg reported no errors | PASS |
| Prevent false normal completion | Ordinary verifier rejects interrupted attempt | Expected rejection: `Capture ended before its requested duration: read_error` | PASS |
| Confirm disappearance | Exact interface no longer present | Camera A count `0`; only Camera B remained enumerated | PASS |
| Reconnect same physical camera | Same exact identity returns | Parent serial `0E1A0C0F` and exact interface `6&DBA5B52&2&0000` returned; Camera B remained separately identifiable | PASS |
| Start new artifact after reconnect | New run ID; normal finalization and full decode | Separate run `2D358446-A581-4DDA-92FA-92B5F71606A9` reached 30 seconds, finalized, and decoded 898/898 frames | PASS |
| Verify post-reconnect timing/media | Measured cadence in diagnostic band; no regressions or hidden corruption | 30.0334 s, 29.9000 measured fps, zero decode errors and timestamp regressions | PASS |

The recorded lifecycle is an ordered sequence, not one continuous file:

| Order | Lifecycle event | Identity/artifact |
|---:|---|---|
| 1 | Pre-disconnect acquisition active | Run `1858C775-7907-43B3-9A3C-59104285E826` on Camera A exact interface |
| 2 | Terminal device-loss read event | Same run; `read_error`, HRESULT `0xC00D3EA2`, requested duration false |
| 3 | Partial sink finalization and complete decode | 265/265 frames; controlled evidence `HC-EV-5becff40f4271131c8b43641` |
| 4 | Exact device absent | Camera A interface count zero; Camera B remained present |
| 5 | Same physical/Windows identity rediscovered | Parent serial `0E1A0C0F`; exact Camera A interface restored |
| 6 | New post-reconnect acquisition and normal completion | Run `2D358446-A581-4DDA-92FA-92B5F71606A9`; controlled evidence `HC-EV-16e6576bbcf4f1896d64aac6` |

The physical USB reattachment time was operator-observed but not independently
timestamped by a hardware event recorder; no exposure-synchronization or
hardware-timing claim is made from this sequence.

## 6. Error, deviation, and disposition

The observed `0xC00D3EA2` is retained as raw platform evidence. It is not
translated into a more specific hardware diagnosis without an authoritative
mapping and corroboration. The interrupted recording is a finalized partial
attempt, not a complete capture and not a successful protocol trial.

The test deviates from normative HC-P0-UVC-002 because the available C920 does
not report the required 1080p60 profile; this diagnostic used 1080p30. The
post-reconnect MP4 reported a nominal stream rate of 60 fps while measured
average cadence was 29.9000 fps. The nominal header is not substituted for
measured source evidence. The complete bounded diagnostic therefore passes,
but it does not qualify HC-P0-UVC-002 or establish production recovery behavior.

## 7. Evidence artifacts

The authoritative retained copies are outside source control in the local
controlled evidence vault. The original interrupted evidence is immutable; the
post-reconnect result is a second receipt rather than an edit to the first.

| Control record | Value |
|---|---|
| Controlled evidence ID | `HC-EV-5becff40f4271131c8b43641` |
| Source run ID | `1858C775-7907-43B3-9A3C-59104285E826` |
| Artifact-set SHA-256 | `5becff40f4271131c8b43641b8b6db43409959cfd085e583ca0a2effc60efa5b` |
| Engineering snapshot | `HC-ENG-20260831T163246Z-7d17f5afce36` |
| Release-record SHA-256 | `756b8c7a36d2239501ee5401c29fae16d2648a178b32f286338ac8611674c269` |
| Receipt SHA-256 | `aebe2a5c5682d7f67769762f6c65baa3dc74f9893a07f6ba90472a439e0310f9` |
| CycloneDX 1.7 SBOM SHA-256 | `2f34a8112c2c2ea274c4be7fd4d15443368feb4baa06c3e364dd7fae736f94e5` |
| SBOM validation | Project validator and official CycloneDX CLI 0.33.1 passed |
| Review state | `DRAFT` |
| Vault verification | Passed for both retained runs on 2026-09-01 |

The original `%TEMP%` directory is source provenance only and is no longer the
authoritative retention location.

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| `capture-result.json` | 984 | `B0176A51FF56B21E8B0940742E6B66E7486CE3B770975042926EEC404EEAD236` |
| `capture.mp4` | 3,441,691 | `36982B4B6A38D1997BBE3B7BAAA997A73F452CEC381EFEF50102564ACD77C1E5` |
| `frames.csv` | 47,895 | `EA6A715ABCBEC483F1965D1018105CB4C4499EE665706ADA87A7025F652EF3AC` |

Post-reconnect controlled record:

| Control record | Value |
|---|---|
| Controlled evidence ID | `HC-EV-16e6576bbcf4f1896d64aac6` |
| Source run ID | `2D358446-A581-4DDA-92FA-92B5F71606A9` |
| Test case ID | `HC-P0-UVC-002-DIAG-01-POST-RECONNECT` |
| Artifact-set SHA-256 | `16e6576bbcf4f1896d64aac64fca1d5482a92a762fb59e5e1b44e5b9d4d1d3ca` |
| Receipt SHA-256 | `5a42b4f1d7c2af42bf83f6a06d53ee6d833b22789d67b5afbf40f661cbd43964` |
| Review state | `DRAFT` |

| Artifact | Bytes | SHA-256 |
|---|---:|---|
| `capture-result.json` | 983 | `8617A75A74F4404FCFB8E648E61731118D089356ABD37E52CABA77A4B4BB7F04` |
| `capture.mp4` | 11,384,849 | `1DE430DF49F95A3D030C5FE230845F709CB3A93EC484D42037B941BF2FAE6C9E` |
| `frames.csv` | 164,528 | `BF1C89DF97A7F4BBCB95D714A8D8A5CBCA024F6247EB0E9C6CCB596D0AEF02F2` |

## 8. Evidence level and limitations

- Source implemented: yes, isolated recovery diagnostic only.
- Build/static checks: passed.
- Automated/component behavior: control path and false-completion rejection passed.
- Hardware-in-the-loop: live disconnect, device disappearance, partial
  finalization/full decode, same-identity rediscovery, and separate normal
  post-reconnect capture/full decode passed.
- Runtime recovery integration: bounded diagnostic path passed; production
  coordinator recovery is not implemented or verified.
- Field workflow: not verified.
- Regulatory/clinical review: not performed.

## 9. Open actions and review

1. Obtain independent QA review before treating P0.2J as independently closed.
2. Retain this diagnostic in P0.2 evidence-package integration without
   representing it as normative 1080p60 qualification.
3. Verify production coordinator recovery separately when that implementation exists.

**Prepared by:** Agent-generated engineering evidence record  
**Independent reviewer:** Open  
**Qualified regulatory review:** Not applicable to this diagnostic result; no conformity claim
