using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace HumTrack.Core.Tracking.Engines;

/// <summary>
/// Lucas-Kanade pyramidal sparse optical flow tracking engine.
/// Tracks ALL markers simultaneously in a single OpenCV call.
///
/// <para><b>Best for</b>: Multi-point gait analysis with retroreflective or sticker markers.</para>
/// <para><b>Key advantage</b>: O(1) calls regardless of marker count — all points tracked at once.</para>
/// <para><b>Deterministic</b>: Yes — pyramidal LK uses fixed-point gradient computation.</para>
/// </summary>
public sealed class OpticalFlowEngine : ITrackingEngine
{
    private Mat? _previousGray;
    private PointF[]? _previousPoints;
    private EngineSettings _settings = new();

    /// <inheritdoc />
    public string Name => "Optical Flow (Lucas-Kanade)";

    /// <inheritdoc />
    public string Description =>
        "Pyramidal sparse optical flow. Tracks all markers simultaneously in a " +
        "single call. Best performance for multi-point gait analysis.";

    /// <inheritdoc />
    public TrackingCapabilities Capabilities =>
        TrackingCapabilities.MultiPoint | TrackingCapabilities.SubPixelAccuracy;

    /// <inheritdoc />
    public void Initialize(Mat referenceFrame, IReadOnlyList<RectangleF> markerRegions, EngineSettings settings)
    {
        _settings = settings;

        // Convert to grayscale for optical flow
        _previousGray = new Mat();
        CvInvoke.CvtColor(referenceFrame, _previousGray, ColorConversion.Bgr2Gray);

        // Extract marker center points
        _previousPoints = new PointF[markerRegions.Count];
        for (int i = 0; i < markerRegions.Count; i++)
        {
            _previousPoints[i] = new PointF(
                markerRegions[i].X + markerRegions[i].Width / 2f,
                markerRegions[i].Y + markerRegions[i].Height / 2f);
        }
    }

    /// <inheritdoc />
    public TrackingResult[] Track(Mat currentFrame)
    {
        if (_previousGray is null || _previousPoints is null || _previousPoints.Length == 0)
            throw new InvalidOperationException("Engine must be initialized before tracking.");

        using var currentGray = new Mat();
        CvInvoke.CvtColor(currentFrame, currentGray, ColorConversion.Bgr2Gray);

        // Wrap points in VectorOfPointF for EmguCV API
        using var prevPtsVec = new VectorOfPointF(_previousPoints);
        using var nextPtsVec = new VectorOfPointF();
        using var statusVec = new VectorOfByte();
        using var errVec = new VectorOfFloat();

        // Track ALL points in a single call
        CvInvoke.CalcOpticalFlowPyrLK(
            _previousGray,
            currentGray,
            prevPtsVec,
            nextPtsVec,
            statusVec,
            errVec,
            _settings.LkWindowSize,
            _settings.PyramidLevels,
            new MCvTermCriteria(30, 0.01));

        var nextPoints = nextPtsVec.ToArray();
        var status = statusVec.ToArray();
        var trackError = errVec.ToArray();

        var results = new TrackingResult[_previousPoints.Length];

        for (int i = 0; i < _previousPoints.Length; i++)
        {
            var isFound = i < status.Length && status[i] == 1;
            var err = i < trackError.Length ? trackError[i] : 50f;
            var confidence = isFound ? Math.Max(0f, 1f - err / 50f) : 0f;
            var halfSize = 10f;
            var pos = i < nextPoints.Length ? nextPoints[i] : _previousPoints[i];

            results[i] = new TrackingResult(
                MarkerIndex: i,
                Position: isFound ? pos : _previousPoints[i],
                Confidence: confidence,
                IsFound: isFound && confidence >= _settings.ConfidenceThreshold,
                BoundingBox: new RectangleF(
                    pos.X - halfSize,
                    pos.Y - halfSize,
                    halfSize * 2, halfSize * 2));
        }

        // Swap for next frame
        _previousGray.Dispose();
        _previousGray = currentGray.Clone();
        _previousPoints = nextPoints;

        return results;
    }

    /// <inheritdoc />
    public bool Reinitialize(Mat currentFrame, int markerIndex, RectangleF searchRegion)
    {
        if (_previousPoints is null || _previousGray is null)
            return false;

        _previousPoints[markerIndex] = new PointF(
            searchRegion.X + searchRegion.Width / 2f,
            searchRegion.Y + searchRegion.Height / 2f);

        _previousGray.Dispose();
        _previousGray = new Mat();
        CvInvoke.CvtColor(currentFrame, _previousGray, ColorConversion.Bgr2Gray);

        return true;
    }

    /// <inheritdoc />
    public TrackingResult[] Detect(Mat frame, EngineSettings settings)
    {
        // Optical flow does not support template-free detection
        return Array.Empty<TrackingResult>();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _previousGray?.Dispose();
    }
}
