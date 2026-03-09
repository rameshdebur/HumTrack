using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace HumTrack.Core.Tracking.Engines;

/// <summary>
/// Blob detection engine for physical markers (retroreflective tape or colored stickers).
/// Detects ALL markers in a single frame pass — no templates or ROIs needed.
///
/// <para><b>Modes</b>:<br/>
/// • Brightness mode (retroreflective): threshold grayscale → find contours<br/>
/// • Color mode (stickers): HSV threshold → find contours
/// </para>
///
/// <para><b>Marker identity</b>: Maintained by nearest-neighbor association across frames.
/// For more robust identity, use colored stickers (color = ID) or ArUco markers.</para>
///
/// <para><b>Deterministic</b>: Yes — thresholding and contour detection are purely deterministic.</para>
/// </summary>
public sealed class BlobDetectionEngine : ITrackingEngine
{
    private PointF[]? _lastPositions;
    private EngineSettings _settings = new();

    /// <inheritdoc />
    public string Name => "Blob Detection (Reflective/Color)";

    /// <inheritdoc />
    public string Description =>
        "Brightness or HSV color thresholding with contour detection. " +
        "Fastest method for retroreflective tape or colored sticker markers. " +
        "No template needed.";

    /// <inheritdoc />
    public TrackingCapabilities Capabilities =>
        TrackingCapabilities.MultiPoint |
        TrackingCapabilities.ScaleInvariant |
        TrackingCapabilities.ColorBased |
        TrackingCapabilities.TemplateFreee;

    /// <inheritdoc />
    public void Initialize(Mat referenceFrame, IReadOnlyList<RectangleF> markerRegions, EngineSettings settings)
    {
        _settings = settings;

        if (markerRegions.Count > 0)
        {
            // Use provided positions as initial marker locations
            _lastPositions = new PointF[markerRegions.Count];
            for (int i = 0; i < markerRegions.Count; i++)
            {
                _lastPositions[i] = new PointF(
                    markerRegions[i].X + markerRegions[i].Width / 2f,
                    markerRegions[i].Y + markerRegions[i].Height / 2f);
            }
        }
        else
        {
            // Auto-detect markers in the reference frame
            var detected = Detect(referenceFrame, settings);
            _lastPositions = detected.Select(r => r.Position).ToArray();
        }
    }

    /// <inheritdoc />
    public TrackingResult[] Track(Mat currentFrame)
    {
        if (_lastPositions is null)
            throw new InvalidOperationException("Engine must be initialized before tracking.");

        // Detect all blobs in the current frame
        var detected = DetectBlobs(currentFrame );

        // Associate detected blobs to known markers via nearest-neighbor
        var results = new TrackingResult[_lastPositions.Length];
        var used = new bool[detected.Count];

        for (int i = 0; i < _lastPositions.Length; i++)
        {
            int bestIdx = -1;
            float bestDist = float.MaxValue;

            for (int j = 0; j < detected.Count; j++)
            {
                if (used[j]) continue;
                var dist = Distance(_lastPositions[i], detected[j].Center);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIdx = j;
                }
            }

            // Maximum association distance: 2x the search window diagonal
            var maxDist = Math.Sqrt(
                _settings.SearchWindow.Width * _settings.SearchWindow.Width +
                _settings.SearchWindow.Height * _settings.SearchWindow.Height) * 2;

            if (bestIdx >= 0 && bestDist < maxDist)
            {
                used[bestIdx] = true;
                var blob = detected[bestIdx];
                _lastPositions[i] = blob.Center;

                results[i] = new TrackingResult(
                    MarkerIndex: i,
                    Position: blob.Center,
                    Confidence: Math.Max(0f, 1f - (float)(bestDist / maxDist)),
                    IsFound: true,
                    BoundingBox: blob.BoundingRect);
            }
            else
            {
                results[i] = new TrackingResult(
                    MarkerIndex: i,
                    Position: _lastPositions[i],
                    Confidence: 0f,
                    IsFound: false,
                    BoundingBox: new RectangleF(
                        _lastPositions[i].X - 10, _lastPositions[i].Y - 10, 20, 20));
            }
        }

        return results;
    }

    /// <inheritdoc />
    public TrackingResult[] Detect(Mat frame, EngineSettings settings)
    {
        _settings = settings;
        var blobs = DetectBlobs(frame);

        return blobs.Select((b, i) => new TrackingResult(
            MarkerIndex: i,
            Position: b.Center,
            Confidence: 1.0f,
            IsFound: true,
            BoundingBox: b.BoundingRect
        )).ToArray();
    }

    /// <inheritdoc />
    public bool Reinitialize(Mat currentFrame, int markerIndex, RectangleF searchRegion)
    {
        if (_lastPositions is null) return false;

        _lastPositions[markerIndex] = new PointF(
            searchRegion.X + searchRegion.Width / 2f,
            searchRegion.Y + searchRegion.Height / 2f);
        return true;
    }

    private List<DetectedBlob> DetectBlobs(Mat frame)
    {
        using var mask = new Mat();

        if (_settings.ColorRange.HasValue)
        {
            // HSV color-based detection (colored stickers)
            using var hsv = new Mat();
            CvInvoke.CvtColor(frame, hsv, ColorConversion.Bgr2Hsv);

            var range = _settings.ColorRange.Value;
            var lower = new ScalarArray(new MCvScalar(range.HueLow, range.SaturationLow, range.ValueLow));
            var upper = new ScalarArray(new MCvScalar(range.HueHigh, range.SaturationHigh, range.ValueHigh));
            CvInvoke.InRange(hsv, lower, upper, mask);
        }
        else
        {
            // Brightness-based detection (retroreflective)
            using var gray = new Mat();
            CvInvoke.CvtColor(frame, gray, ColorConversion.Bgr2Gray);
            CvInvoke.Threshold(gray, mask, _settings.BrightnessThreshold, 255, ThresholdType.Binary);
        }

        // Morphological cleanup
        using var kernel = CvInvoke.GetStructuringElement(ElementShape.Ellipse, new Size(3, 3), new Point(-1, -1));
        CvInvoke.MorphologyEx(mask, mask, MorphOp.Open, kernel, new Point(-1, -1), 1, BorderType.Default, default);

        // Find contours
        using var contours = new VectorOfVectorOfPoint();
        CvInvoke.FindContours(mask, contours, null, RetrType.External, ChainApproxMethod.ChainApproxSimple);

        var blobs = new List<DetectedBlob>();

        for (int i = 0; i < contours.Size; i++)
        {
            var area = CvInvoke.ContourArea(contours[i]);

            // Filter by area
            if (area < _settings.MinMarkerArea || area > _settings.MaxMarkerArea)
                continue;

            // Filter by circularity
            var perimeter = CvInvoke.ArcLength(contours[i], true);
            var circularity = perimeter > 0 ? (4 * Math.PI * area) / (perimeter * perimeter) : 0;
            if (circularity < _settings.MinCircularity)
                continue;

            // Compute centroid using image moments
            var moments = CvInvoke.Moments(contours[i]);
            if (moments.M00 < 1e-6) continue;

            var cx = (float)(moments.M10 / moments.M00);
            var cy = (float)(moments.M01 / moments.M00);

            var bbox = CvInvoke.BoundingRectangle(contours[i]);

            blobs.Add(new DetectedBlob(
                new PointF(cx, cy),
                new RectangleF(bbox.X, bbox.Y, bbox.Width, bbox.Height),
                area));
        }

        return blobs;
    }

    private static float Distance(PointF a, PointF b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // No unmanaged resources
    }

    private readonly record struct DetectedBlob(PointF Center, RectangleF BoundingRect, double Area);
}
