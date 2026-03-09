using System.Drawing;
using Emgu.CV;

namespace HumTrack.Core.Tracking.Engines;

/// <summary>
/// Hybrid tracking engine — combines Blob Detection for initial marker discovery
/// and periodic re-detection with Optical Flow for fast frame-to-frame tracking.
///
/// <para><b>Strategy</b>:<br/>
/// Frame 0: Blob detection finds all markers.<br/>
/// Frames 1..N-1: Optical flow tracks all markers in one call.<br/>
/// Frame N: Blob detection re-detects all markers to correct drift.<br/>
/// Repeat.
/// </para>
///
/// <para><b>Best for</b>: Retroreflective tape markers in gait analysis.
/// Recommended default engine.</para>
///
/// <para><b>Deterministic</b>: Yes — both sub-engines are deterministic.</para>
/// </summary>
public sealed class HybridEngine : ITrackingEngine
{
    private readonly BlobDetectionEngine _detector = new();
    private readonly OpticalFlowEngine _tracker = new();
    private EngineSettings _settings = new();
    private int _framesSinceRedetection;
    private bool _initialized;

    /// <inheritdoc />
    public string Name => "Hybrid (Auto-Optimized)";

    /// <inheritdoc />
    public string Description =>
        "Combines Blob Detection (initial find + periodic re-detection) with " +
        "Optical Flow (fast frame-to-frame tracking). Recommended default for " +
        "reflective or colored markers.";

    /// <inheritdoc />
    public TrackingCapabilities Capabilities =>
        TrackingCapabilities.MultiPoint |
        TrackingCapabilities.ScaleInvariant |
        TrackingCapabilities.SubPixelAccuracy |
        TrackingCapabilities.ColorBased |
        TrackingCapabilities.TemplateFreee;

    /// <inheritdoc />
    public void Initialize(Mat referenceFrame, IReadOnlyList<RectangleF> markerRegions, EngineSettings settings)
    {
        _settings = settings;

        // Stage 1: Detect markers using blob detection
        _detector.Initialize(referenceFrame, markerRegions, settings);

        // Get initial positions from detection
        var detected = _detector.Track(referenceFrame);

        // Stage 2: Initialize optical flow with detected positions
        var detectedRegions = detected
            .Where(r => r.IsFound)
            .Select(r => r.BoundingBox)
            .ToList();

        if (detectedRegions.Count > 0)
        {
            _tracker.Initialize(referenceFrame, detectedRegions, settings);
        }

        _framesSinceRedetection = 0;
        _initialized = true;
    }

    /// <inheritdoc />
    public TrackingResult[] Track(Mat currentFrame)
    {
        if (!_initialized)
            throw new InvalidOperationException("Engine must be initialized before tracking.");

        _framesSinceRedetection++;

        // Periodic re-detection to correct optical flow drift
        if (_framesSinceRedetection >= _settings.RedetectIntervalFrames)
        {
            var redetected = _detector.Track(currentFrame);
            _framesSinceRedetection = 0;

            // Re-initialize optical flow with corrected positions
            var foundRegions = redetected
                .Where(r => r.IsFound)
                .Select(r => r.BoundingBox)
                .ToList();

            if (foundRegions.Count > 0)
            {
                _tracker.Initialize(currentFrame, foundRegions, _settings);
            }

            return redetected;
        }

        // Normal frame: use fast optical flow
        return _tracker.Track(currentFrame);
    }

    /// <inheritdoc />
    public bool Reinitialize(Mat currentFrame, int markerIndex, RectangleF searchRegion)
    {
        return _detector.Reinitialize(currentFrame, markerIndex, searchRegion);
    }

    /// <inheritdoc />
    public TrackingResult[] Detect(Mat frame, EngineSettings settings)
    {
        return _detector.Detect(frame, settings);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _detector.Dispose();
        _tracker.Dispose();
    }
}
