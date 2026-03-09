# HumTrack Calibration Guide

**Version**: 0.1 (Living Document — updated with every calibration-related feature)  
**Standard**: IEC 62304 §5.2; Referenced by SRS REQ-CAL-001..010

---

## Overview

Calibration in HumTrack is a **mandatory, layered process**. Scientific accuracy of all position, velocity, and acceleration outputs depends entirely on calibration quality. The workflow must always be performed in this exact order:

```
Step 1: Lens Intrinsic Calibration (remove lens distortion)
Step 2: World Calibration (establish real-world scale and axes)
Step 3: Multi-Camera Extrinsic Calibration (3D setups only)
```

---

## Step 1: Lens Intrinsic Calibration

HumTrack uses the **full Brown-Conrady model** (the same as OpenCV) which corrects:
- **Radial distortion** (barrel/pincushion): k1, k2, k3, k4, k5, k6
- **Tangential distortion** (off-center lens): p1, p2
- **Thin-prism distortion** (sensor tilt): s1, s2, s3, s4

### 1A: Using a Built-in Lens Profile (Quick Setup)

HumTrack ships with pre-computed profiles for common cameras. Select your camera from the profile library.

> **Warning**: Built-in profiles are approximations. For scientific lab work, always perform your own calibration from Step 1B.

### 1B: ChArUco Board Calibration (Recommended)

1. Print the ChArUco calibration board from `docs/calibration_board_charuco.pdf` on A4/Letter paper (do NOT resize — actual size matters)
2. Measure the actual printed square size in mm and enter it in the calibration dialog
3. Open `Calibration → Lens Calibration → ChArUco Wizard`
4. Hold the board in front of the camera. Capture ≥20 frames from different angles and positions
5. HumTrack computes intrinsic parameters. Review the **Re-projection Error**:
   - **< 0.5 px RMS**: Excellent (lab standard)
   - **0.5–1.5 px RMS**: Acceptable for field use
   - **> 1.5 px RMS**: Poor — recapture frames, check board print accuracy

### 1C: Accepting Calibration

After reviewing the re-projection overlay and RMS error, you must explicitly click **"Accept Calibration"**. Calibration is stored per-project and applied to all subsequent video analysis.

---

## Step 2: World Calibration

World calibration maps pixel coordinates to real-world units (meters, cm, etc.).

### 2A: Calibration Stick (Scale Only)

Place an object of known length in the scene. Draw a stick between its endpoints in the UI and enter the known length.

### 2B: Known World Points (Scale + Origin + Angle)

Mark 2 or more points whose real-world coordinates you know (measured with a tape or survey tool). The software will solve for scale, origin, and axis angle simultaneously.

### 2C: Quality Indicator

After world calibration, HumTrack displays:
- The physical scale in the status bar (e.g., "1 px = 3.2 mm")
- A world grid overlay on the video
- Axis labels showing X/Y directions

> **Critical**: Always verify by measuring a known distance on video after calibration. If the derived distance does not match the physical measurement to within your required accuracy, recalibrate.

---

## Step 3: Multi-Camera Extrinsic Calibration

[To be populated in Phase 5 implementation]

---

## Troubleshooting

| Problem | Likely Cause | Resolution |
|---------|-------------|------------|
| RMS error > 1.5 px | Blurry calibration frames or wrong board size | Recapture with sharper focus; verify printed square size |
| World scale incorrect | Known-length reference measured incorrectly | Re-measure reference physically |
| Distortion not fully removed | Using a built-in profile | Perform manual ChArUco calibration |
