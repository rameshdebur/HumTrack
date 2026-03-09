# Risk Analysis
**Project**: HumTrack  
**Date**: 2026-03-09  
**Version**: 0.1 (Draft)  
**Standard Reference**: ISO 14971 / IEC 62304

## 1. Intended Use
HumTrack is a software tool used to analyze video recordings of human movement, track markers, and compute kinematic data (position, velocity, acceleration). The software is a measurement tool and does not provide diagnosis or control medical devices.

## 2. Risk Matrix

| Risk ID | Hazard | Consequence | Prob | Sev | Initial Risk | Mitigation | Final Prob | Final Sev | Final Risk |
|---------|--------|-------------|------|-----|--------------|------------|------------|-----------|------------|
| RSK-001 | Non-deterministic calculation | Clinician receives inconsistent data across runs | Occasional | Minor | Medium | All tracking/physics algorithms must pass automated byte-for-byte determinism tests on CI. | Rare | Minor | Low |
| RSK-002 | Incorrect calibration scale | Derived velocity/acceleration is incorrect | Probable | Mod | High | Prominent visual display of current scale unit; mandate verification step before export. | Rare | Mod | Low |
| RSK-003 | Video frame dropping | Timing data gets skewed | Occasional | Mod | Medium | Enforce frame-by-frame exact extraction via FFmpeg rather than real-time playback clocking. | Rare | Mod | Low |
