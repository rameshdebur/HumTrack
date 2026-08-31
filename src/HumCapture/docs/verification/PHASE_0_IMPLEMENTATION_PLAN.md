# Phase 0 Capability-Probe Implementation Plan

**Document ID:** HC-P0-PLAN-001  
**Status:** Accepted plan; P0.1 gate passed; P0.2A passed and P0.2B hardware acceptance is blocked  
**Date:** 2026-08-28

## 1. Outcome

Implement isolated engineering probes and run a named-hardware campaign that
confirms, restricts, or rejects HumCapture's provisional MVP hardware profiles.

This plan does not authorize production coordinator, Android capture, subject,
protocol, session, transfer, repository, or HumTrack-integration features.

## 2. Ownership and review

- **Primary implementation owner:** Software Engineer assigned to verification infrastructure.
- **Architecture owner:** System Architect.
- **Verification owner:** QA/Independent Reviewer.
- **Affected specialists:** Android camera/encoder, Windows Media Foundation/UVC, timing/performance, and network specialists as needed.
- **Regulatory/risk reviewer:** Reviews evidence handling, claims, and changes to risk controls; the probes do not establish regulatory or clinical acceptance.
- **Write boundary:** `src/HumCapture` only.

Proposed isolated implementation paths are:

```text
tools/capability-probes/
  android/
  windows/
  shared/

tests/capability-probes/

docs/verification/phase-0-results/
  compatibility-matrix.md
  campaigns/
```

Raw video and large evidence remain outside source control. Only redacted
summaries, hashes, reviewed compatibility results, and synthetic fixtures may be
committed.

## 3. Implementation principles

1. Keep all probe code visibly labelled non-production and outside production app paths.
2. Prefer command-line or minimal diagnostic surfaces; detailed product UI is out of scope.
3. Separate reported, requested, negotiated, and measured capability values.
4. Preserve raw timestamp values and declare their clock domain and provenance.
5. Never silently substitute a resolution, frame rate, format, camera, or timestamp source.
6. Use synthetic scenes, identifiers, and payloads only.
7. Make each evidence document format-versioned and independently validatable.
8. Treat every unsupported or inconclusive result as evidence, not as a probe failure to hide.
9. Do not promote exploratory code into production without separate review and authorization.

## 4. Increment P0.1 — Evidence contract and fixtures

### Build

- Define draft JSON Schemas for the run manifest and capability report.
- Define CSV column dictionaries for frames, sensors, thermal/power/storage, and network measurements.
- Define stable units, enums, timestamp-domain names, provenance fields, and null/unknown behavior.
- Create synthetic valid and invalid example evidence packages.
- Create a local evidence validator and SHA-256 index generator.
- Define a campaign index that references evidence without embedding large artifacts.

These schemas are Phase 0 evidence contracts, not the production capture-package
contracts scheduled for Phase 1.

### Gate P0.1

- Schema and example validation passes.
- Invalid, missing, newer-version, path-escape, and hash-mismatch fixtures fail explicitly.
- Architect and QA approve the distinction between probe evidence and production contracts.
- No subject data or production repository path is present.

**Current evidence:** Source and 55 automated tests were verified on 2026-08-28.
See `phase-0-results/P0_1_VERIFICATION_REPORT.md`. Integrity, measurement,
per-stream, procedure-family, normative profile, one-to-one profile binding,
network, compatibility, UVC vocabulary, and exact UVC lifecycle findings are
remediated. Final independent adversarial review passed. Gate P0.1 is
passed/closed; P0.2 is in progress. P0.2A enumeration passed. The isolated
P0.2B native short-capture source builds and finalizes readable media, but one
of two C920 units failed the measured 29–31 fps diagnostic band twice; see
`phase-0-results/P0_2B_SHORT_CAPTURE_REPORT.md`.

## 5. Increment P0.2 — Windows and UVC probe

### Build

- Create a Windows x64 probe runner that records host, OS, driver, power, storage, USB, and network-adapter identity.
- Enumerate UVC devices and their reported formats and controls through the Windows camera stack.
- Attempt an exact requested format and report the actual negotiated format.
- Capture synthetic-scene video with sample sequence, timestamps, clock provenance, discontinuities, queue pressure, and finalization evidence.
- Record USB topology and negotiated speed when the operating system exposes them.
- Implement short diagnostic and 30-minute qualification modes.
- Implement disconnect/reconnect as two explicitly separate capture artifacts.
- Add a guided operator observation for manufacturer-documented camera in-use
  indicators, correlated to exact device identity, active frame delivery, final
  source release, and the associated run evidence.
- Produce the P0.1 evidence package and human summary.

The first implementation task shall be a bounded Windows-camera API spike. It
shall compare the information available from the managed Windows capture surface
with native Media Foundation. Native access is selected only where required for
format negotiation, controls, timestamps, or provenance. Record the selection as
a short architecture review before expanding the probe.

### Gate P0.2

- At least one physical UVC device is enumerated without silent substitution.
- A short run finalizes and its evidence validates.
- Timestamp provenance is explicit, including `unknown` when the stack cannot prove more.
- Indicator applicability is recorded for every tested UVC device. A device
  without an available/documented indicator is `NOT_APPLICABLE` and is not
  rejected for that absence. A device with one has a repeatable
  baseline/active/released result or is explicitly conditional, failed, or
  inconclusive; the indicator is never used as timing evidence.
- A 30-minute run is possible from the tool, but completing it is campaign evidence rather than source implementation evidence.
- Automated tests cover evidence generation and failure reporting; hardware behavior is reported separately.

## 6. Increment P0.3 — Android local probe

### Build

- Create a Kotlin diagnostic application using Camera2, MediaCodec/MediaMuxer, and Android sensor APIs.
- Enumerate logical/physical cameras, stream combinations, encoder profiles, lens/calibration metadata, sensors, thermal state, battery, and storage.
- Let the operator choose the physical/logical camera and exact test profile.
- Show requested and negotiated configuration before recording.
- Run master-only and master-plus-preview-plus-IMU tests without allowing preview pressure to block the master path.
- Attempt 1080p120 only when the complete stream and encoder combination reports support.
- Generate locally finalized P0.1 evidence using synthetic scenes and identifiers.
- Provide short diagnostic and 30-minute qualification modes.

### Gate P0.3

- Capability enumeration works on one physical Android phone.
- A short master run finalizes with frame and sensor timestamp provenance.
- Requested, reported, negotiated, and measured values remain distinct.
- Preview loss or disabling does not terminate the diagnostic master run.
- Unsupported optional modes are visible and do not block the mandatory-profile test.

## 7. Increment P0.4 — Probe discovery and network tests

### Build

- Advertise a probe-only mDNS service from Android and browse/resolve it on Windows.
- Provide manual endpoint entry when mDNS is unavailable.
- Use an operator-visible, run-scoped pairing token so an unrelated device cannot submit or control probe evidence accidentally.
- Transfer capability/evidence summaries without subject data.
- Generate one and two synthetic 720p15 preview-like loads; this tests network capacity and does not require two cameras.
- Measure latency, jitter, loss, reconnection, clock exchanges, synthetic transfer throughput, interruption, and byte-range resume.
- Run the same procedures on a named dedicated AP and named Windows laptop hotspot.

This is a disposable probe protocol. It shall not be described as the production
control, security, pairing, preview, timing, or transfer protocol.

### Gate P0.4

- Discovery and manual fallback are independently demonstrated.
- Run-scoped pairing rejects an unpaired sender.
- Discovery, preview-load, clock, and transfer results are reported separately.
- Network interruption does not terminate an Android local diagnostic master.
- Dedicated-AP and hotspot results identify the exact adapter/driver/configuration.

## 8. Increment P0.5 — Named-hardware campaign

### Prepare

- Record the exact device matrix and physical connection topology.
- Confirm sufficient controlled storage and use synthetic motion/test charts.
- Fix probe source revisions, evidence-format versions, procedures, and operator instructions for the campaign.
- Identify the independent reviewer before starting qualification runs.

### Execute

- HC-P0-AND-001 through HC-P0-AND-003 on two different Android models.
- HC-P0-UVC-001 and HC-P0-UVC-002 on named Windows/UVC configurations.
- HC-P0-NET-001 and HC-P0-NET-002 on the dedicated AP and laptop hotspot.
- HC-P0-COMP-001 on Windows 10 22H2 x64, with Windows 11 x64 as the primary baseline.
- Repeat or extend runs when a result is conditional, anomalous, or not reproducible.

### Review

- Validate hashes, schemas, durations, negotiated profiles, timestamps, gaps, media readability, and stated limitations.
- Distinguish probe-source completion, automated tests, runtime integration, hardware-in-the-loop, and field workflow evidence.
- Classify every tested configuration as `PASS`, `CONDITIONAL`, `FAIL`, or `INCONCLUSIVE`.
- Do not generalize a result beyond the named hardware, OS, driver, connection, and operating conditions without reviewed evidence.

### Gate P0.5 / Phase 0 exit

- Compatibility matrix is reviewed and versioned.
- Mandatory Android and UVC profile decisions are recorded.
- Optional 1080p120 is explicitly supported, conditional, unsupported, or inconclusive per named phone.
- Android minimum version/device position is recorded.
- Timestamp and software-synchronization targets are confirmed or revised through an ADR when necessary.
- Dedicated-AP and laptop-hotspot conditions are recorded.
- Windows 10 compatibility status and warnings are recorded.
- Requirements, risks, known issues, roadmap, traceability, and project state reflect the evidence.
- Architect and independent QA reviewer sign off the Phase 0 evidence review.

## 9. Execution order and dependencies

```text
P0.1 evidence contract
  +-> P0.2 Windows/UVC probe ----+
  +-> P0.3 Android probe --------+-> P0.4 network integration
                                  -> P0.5 hardware campaign
                                  -> Phase 0 exit decision
```

P0.2 and P0.3 may proceed independently only after P0.1 is reviewed. P0.4
depends on the minimum usable Windows and Android probes. P0.5 uses frozen probe
versions and cannot begin with unreviewed evidence formats.

## 10. Deferred work

- Production coordinator and Android UI design.
- Production authentication, control, state-machine, preview, transfer, and package protocols.
- Subject, protocol, session, and trial workflows.
- Multi-source production synchronization.
- Automatic or manual production capture transfer and cleanup.
- HumTrack import or changes outside `src/HumCapture`.
- Clinical validation, regulatory classification, certification, or release claims.

## 11. Authorization gate

This plan is approved as the implementation sequence. P0.1 exploratory evidence
infrastructure was explicitly authorized on 2026-08-27. Later increments require
their preceding gates to pass. This authorization does not extend to production
application features.
