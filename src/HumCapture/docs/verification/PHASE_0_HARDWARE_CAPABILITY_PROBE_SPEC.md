# Phase 0 Hardware-Capability Probe Specification

**Document ID:** HC-P0-SPEC-001  
**Status:** Accepted  
**Accepted:** 2026-08-27  
**Scope:** MVP engineering qualification; not production application or clinical validation

Changes to the scope, mandatory profiles, evidence boundary, qualification rules,
or exit gate require recorded Architect and QA review. The implementation sequence
is defined in `PHASE_0_IMPLEMENTATION_PLAN.md`.

## 1. Purpose

Phase 0 shall establish, using named hardware and reproducible evidence, which Android, Windows UVC, USB, and local-network configurations can support the proposed HumCapture MVP profiles.

The work answers hardware-dependent questions before production components depend on them. A product datasheet, API capability flag, successful build, or short visual preview is not qualification evidence.

## 2. Boundary

Phase 0 consists of isolated diagnostic utilities and controlled test runs:

```text
Android camera and sensors
  -> Android capability probe
  -> versioned capability and run evidence
  -> Windows probe console

UVC camera
  -> Windows Media Foundation capability probe
  -> versioned capability and run evidence
  -> Windows probe console
```

The Android probe owns the internal phone camera through Android platform APIs. The coordinator does not access that camera as a directly attached Windows device. The phone reports capabilities and measured test results to the Windows probe over the local network.

A UVC camera is attached directly to Windows by USB and is enumerated and exercised by the Windows probe.

The probes shall remain isolated from production application paths and clearly marked as exploratory. Evidence-backed logic may later be deliberately promoted into:

- the Android app's Device Diagnostics capability;
- the coordinator's Device Setup and Qualification workflow; and
- UVC worker preflight checks.

Phase 0 does not implement subject, protocol, trial, master-transfer, repository, cleanup, or downstream HumTrack workflows.

## 3. Safety and data rules

- Use synthetic scenes and test identifiers only.
- Do not collect subject names, demographics, clinical recordings, or other participant data.
- A probe result shall not claim clinical accuracy, medical-device compliance, CDSCO approval, or hardware synchronization.
- Preview and network activity shall never be reported as proof of scientific-master integrity.
- Timestamp origin and uncertainty shall be reported; nominal FPS and host-arrival time shall not be substituted for source evidence.

## 4. Phase 0 deliverables

1. A minimal Android capability-probe application.
2. A minimal Windows probe console for Android discovery/report collection, UVC enumeration, and test control.
3. A versioned, machine-readable capability report and run manifest.
4. Frame, timestamp, sensor, thermal, storage, USB, and network measurements as applicable.
5. A human-readable report for every test run.
6. A named hardware/OS/driver/network compatibility matrix.
7. A decision record confirming, restricting, or revising each provisional MVP profile and threshold.

These are engineering tools and evidence, not production feature completion.

## 5. Minimum test inventory

The initial matrix shall include:

- two different physical Android phone models;
- one supported Windows 11 x64 laptop;
- one Windows 10 22H2 x64 technical-compatibility laptop or controlled installation;
- at least one physical UVC camera;
- the laptop's named USB controllers, ports, hubs, storage target, Wi-Fi adapter, and driver versions;
- one named dedicated access point; and
- the named Windows laptop-hotspot configuration.

Additional phones, cameras, hubs, adapters, and storage devices are unqualified until tested. Qualification applies to the recorded configuration, not automatically to an entire product family.

## 6. Android probe requirements

### 6.1 Static capability inventory

The Android probe shall record:

- manufacturer, model, Android version, build, application version, and stable test-device identifier;
- logical and physical Camera2 identifiers and lens facing;
- reported normal and constrained high-speed resolution/frame-rate combinations;
- hardware-support level and relevant stream-combination limits;
- supported H.264 encoder profiles, levels, resolutions, rates, and bitrate ranges;
- exposure, focus, zoom/crop, stabilization, orientation, and rolling-shutter metadata availability;
- focal length, sensor size, active array, intrinsic/calibration metadata, and provenance where reported;
- available rotation vector, gravity, gyroscope, accelerometer, linear-acceleration, and magnetometer sensors;
- reported sensor sampling limits and timestamp sources; and
- available storage, battery state, and temperature/thermal-status sources.

An API-reported combination shall be labelled `reported`, not `qualified`, until a sustained run passes.

### 6.2 Measured capture runs

The probe shall attempt:

- mandatory candidate: H.264 1920 x 1080 at 60 fps scientific-master path;
- concurrent candidate: the mandatory master plus H.264 1280 x 720 at 15 fps disposable preview;
- optional candidate: H.264 1920 x 1080 at 120 fps, only when reported by the complete camera/encoder stream combination; and
- IMU recording concurrent with each relevant master run.

For each run it shall record requested and negotiated configuration, actual frame sequence and source timestamps, encoder/output timestamps, discontinuities, file finalization, bitrate and size, queue pressure, storage write behavior, battery, and thermal status.

The mandatory soak duration is 30 continuous minutes. A shorter diagnostic run may identify an early failure but cannot qualify a profile.

## 7. Windows UVC probe requirements

The Windows probe shall record:

- Windows edition/build, probe version, computer identity used for testing, and power mode;
- UVC device identity, USB vendor/product identifiers where available, driver/provider/version, Media Foundation identity, and physical port path where obtainable;
- every reported resolution, frame rate, pixel/encoded format, and relevant exposure/focus/control range;
- requested and negotiated capture configuration;
- timestamp provenance as device/sensor, host sample, host arrival, synthesized, or unknown;
- sample sequence, timestamps, discontinuities, queue pressure, output size, and finalization result;
- USB controller/hub topology, negotiated USB speed where obtainable, and contention indicators; and
- disconnect/reconnect behavior without treating reconnection as continuation of an uninterrupted master.
- applicability of a physical in-use indicator and, only when documented or
  present, operator observation before acquisition, during fresh frame delivery,
  and after source release, following `UVC_IN_USE_INDICATOR_TEST_PROTOCOL.md`.

The mandatory UVC candidate is 1920 x 1080 at 60 fps where the camera and Windows stack report it. The mandatory qualification run is 30 continuous minutes. Unsupported cameras shall be reported truthfully; the probe shall not silently substitute another resolution, frame rate, or format.

A documented activity indicator that does not correspond to actual stream use
blocks qualification of the named camera/configuration until reconciled or
rejected. Indicator observation is privacy/operator evidence only and shall not
be used as a timestamp or synchronization signal.

A camera model with no available/documented activity indicator is not failed or
rejected for that absence. Its indicator result is `NOT_APPLICABLE`; normal API
state, acquisition-health, finalization, and privacy controls still apply.

Multi-UVC capture is not an MVP requirement unless a selected protocol later requires it. USB contention testing shall nevertheless record the named topology and any concurrent storage or device load used during qualification.

## 8. Discovery and network probe requirements

The Android probe shall advertise a non-production probe service on the local network. The Windows probe shall browse and resolve it and shall also permit manual IP/endpoint entry when multicast discovery is unavailable.

The network test shall cover the named dedicated access point and named Windows laptop hotspot and record:

- adapter, driver, band/channel, network profile, firewall state relevant to the probe, and client count;
- mDNS advertisement, discovery, resolution, loss, and recovery;
- manual endpoint fallback;
- round-trip latency, jitter, packet loss, and monotonic clock-exchange observations;
- one and two concurrent 720p15 preview-like test streams;
- disconnect/reconnect behavior; and
- post-capture-like transfer throughput, interruption, and byte-range resume behavior using synthetic data.

Discovery success does not qualify preview, timing, or transfer, and preview success does not qualify the scientific master.

## 9. Evidence format

Every run shall have a unique `run_id` and an immutable evidence directory outside the production subject repository. The logical package is:

```text
<run_id>/
  run-manifest.json
  capability-report.json
  measurements/
    frames.csv
    sensors.csv                 # Android when applicable
    thermal-power-storage.csv
    network.csv                 # network runs only
  logs/
  media/                        # synthetic test media only
  hashes.sha256
  summary.md
```

The JSON documents shall include at least:

- evidence-format version;
- run and parent campaign identifiers;
- UTC and monotonic test timing;
- probe software versions and source revision;
- exact hardware, OS, firmware, driver, connection, power, and network identity;
- requested, reported, negotiated, and measured configurations as separate fields;
- test procedure identifier and duration;
- artifact paths, byte lengths, media properties, and SHA-256 hashes;
- measured results, discontinuities, warnings, and raw provenance;
- disposition: `PASS`, `CONDITIONAL`, `FAIL`, or `INCONCLUSIVE`;
- operator/reviewer identifiers using the signed-in Windows account where applicable; and
- limitations and deviations.

Large media and raw evidence shall not be committed to source control. The repository may contain redacted summaries, hashes, and the compatibility matrix. Evidence retention location and access shall be documented for each campaign.

## 10. Qualification rules

A configuration may be marked `PASS` only when all applicable conditions hold:

1. The exact hardware/software/driver combination is identified.
2. The requested configuration is the negotiated and encoded configuration; no silent fallback occurred.
3. The required 30-minute run completes and finalizes a readable artifact.
4. Frame and sample timestamps are monotonic within their declared domains.
5. Every observed gap, duplicate, discontinuity, fallback, or source change is detectable and recorded.
6. Preview, sensor recording, probe UI, and network activity do not cause an unreported master change or failure.
7. Thermal, battery, storage, encoder, and USB conditions do not terminate the run or silently alter the qualified profile.
8. Evidence is complete, hashed, reviewable, and contains no subject data.

`CONDITIONAL` requires an explicit limitation such as a named port, power mode, disabled stabilization mode, dedicated adapter, or maximum duration. `FAIL` means the tested configuration is not supported. `INCONCLUSIVE` means evidence was incomplete or the procedure was not followed and shall not be presented as support.

The following quantitative values remain provisional until the first campaign is reviewed:

- acceptable measured frame-delivery deviation and discontinuity rate;
- software clock-fit uncertainty and first-frame alignment thresholds;
- preview packet-loss and latency limits;
- minimum resumable-transfer throughput; and
- thermal, battery, storage, and USB operating margins.

Phase 0 shall measure these values and end with an explicit architecture/product decision. It shall not hide an unfavorable result by weakening a threshold inside probe code.

## 11. Required qualification scenarios

| ID | Scenario | Minimum evidence | Decision unlocked |
|---|---|---|---|
| HC-P0-AND-001 | Android 1080p60 master | Static inventory plus 30-minute master run | Android mandatory profile |
| HC-P0-AND-002 | Android master + 720p15 preview + IMU | 30-minute concurrent run | Concurrent preview/sensor profile |
| HC-P0-AND-003 | Android 1080p120 | 30-minute concurrent run when reported | Optional profile only |
| HC-P0-UVC-001 | Windows UVC 1080p60 | Enumeration, provenance, indicator applicability and observation when available, 30-minute run | Named UVC profile |
| HC-P0-UVC-002 | UVC disconnect/reconnect | Separate pre/post artifacts and state evidence | Recovery behavior |
| HC-P0-UVC-003 | UVC in-use indicator when available | Applicability classification; when documented/present, two consistent baseline/active/released observations correlated to fresh frames and exact physical identity | Indicator compatibility or `NOT_APPLICABLE`; reconciliation/rejection only for mismatching available indicators |
| HC-P0-NET-001 | Dedicated AP | Discovery, two previews, clock exchanges, resumable synthetic transfer | Preferred network profile |
| HC-P0-NET-002 | Laptop hotspot | Same evidence plus manual fallback | Qualified field profile |
| HC-P0-COMP-001 | Windows 10 22H2 x64 | UVC/network probe execution on named hardware | Technical compatibility status |

The MVP does not require the 1080p120 scenario to pass. It does require an explicit supported/unsupported result. Two cameras are exercised only for scenarios whose selected protocol requires two sources; Phase 0 does not create a global two-camera requirement.

## 12. Review and exit gate

Phase 0 is complete only when the Architect and QA reviewer have reviewed the evidence and the project records:

- minimum Android version and named qualified devices;
- qualified Android and UVC capture profiles;
- timestamp provenance and supported timing claims;
- dedicated-AP and laptop-hotspot support conditions;
- Windows 11 primary and Windows 10 compatibility results;
- operating limits and required preflight warnings;
- revised requirements, risks, ADRs, and compatibility matrix; and
- every unresolved result as an explicit known issue.

Hardware-in-the-loop, field workflow, and regulatory/clinical review remain distinct evidence levels. Completion of Phase 0 alone does not authorize a product or medical claim.
