using System.Drawing;
using Emgu.CV;
using Emgu.CV.Aruco;
using Emgu.CV.Util;

namespace HumTrack.Core.Tracking.Engines;

/// <summary>
/// ArUco fiducial marker detection engine.
/// Detects printed ArUco/AprilTag markers with unique IDs — no templates needed.
///
/// <para><b>Best for</b>: Printed paper markers glued to the body. Each marker has a
/// unique binary code, so identity is maintained automatically even during occlusion.</para>
///
/// <para><b>Key advantage</b>: Zero marker swap confusion — impossible to mix up left/right
/// knee during gait crossover because each marker has a unique ID.</para>
///
/// <para><b>Deterministic</b>: Yes — pattern detection is a pure binary classification.</para>
/// </summary>
public sealed class ArucoDetectionEngine : ITrackingEngine
{
    private Dictionary? _dictionary;
    private DetectorParameters _detectorParameters = DetectorParameters.GetDefault();
    private int[]? _expectedMarkerIds;

    /// <inheritdoc />
    public string Name => "ArUco Marker Detection";

    /// <inheritdoc />
    public string Description =>
        "Printed fiducial marker detection. Each marker has a unique ID — " +
        "no marker identity confusion, even during limb crossover. Free to print.";

    /// <inheritdoc />
    public TrackingCapabilities Capabilities =>
        TrackingCapabilities.MultiPoint |
        TrackingCapabilities.ScaleInvariant |
        TrackingCapabilities.RotationInvariant |
        TrackingCapabilities.SubPixelAccuracy |
        TrackingCapabilities.SelfIdentifying |
        TrackingCapabilities.TemplateFreee;

    /// <inheritdoc />
    public void Initialize(Mat referenceFrame, IReadOnlyList<RectangleF> markerRegions, EngineSettings settings)
    {
        _dictionary = CreateDictionary(settings.ArucoDictionary);
        _detectorParameters = DetectorParameters.GetDefault();

        if (markerRegions.Count == 0)
        {
            var detected = Detect(referenceFrame, settings);
            _expectedMarkerIds = detected
                .Where(r => r.MarkerId.HasValue)
                .Select(r => r.MarkerId!.Value)
                .ToArray();
        }
    }

    /// <inheritdoc />
    public TrackingResult[] Track(Mat currentFrame)
    {
        var allDetected = DetectArUcoMarkers(currentFrame);

        if (_expectedMarkerIds is null || _expectedMarkerIds.Length == 0)
        {
            return allDetected.Select((d, i) => new TrackingResult(
                MarkerIndex: i,
                Position: d.Center,
                Confidence: 1.0f,
                IsFound: true,
                BoundingBox: d.BoundingRect,
                MarkerId: d.Id
            )).ToArray();
        }

        var results = new TrackingResult[_expectedMarkerIds.Length];

        for (int i = 0; i < _expectedMarkerIds.Length; i++)
        {
            var expectedId = _expectedMarkerIds[i];
            var found = false;

            foreach (var detected in allDetected)
            {
                if (detected.Id == expectedId)
                {
                    results[i] = new TrackingResult(
                        MarkerIndex: i,
                        Position: detected.Center,
                        Confidence: 1.0f,
                        IsFound: true,
                        BoundingBox: detected.BoundingRect,
                        MarkerId: detected.Id);
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                results[i] = new TrackingResult(
                    MarkerIndex: i,
                    Position: default,
                    Confidence: 0f,
                    IsFound: false,
                    BoundingBox: default,
                    MarkerId: expectedId);
            }
        }

        return results;
    }

    /// <inheritdoc />
    public TrackingResult[] Detect(Mat frame, EngineSettings settings)
    {
        _dictionary ??= CreateDictionary(settings.ArucoDictionary);

        var detected = DetectArUcoMarkers(frame);

        return detected.Select((d, i) => new TrackingResult(
            MarkerIndex: i,
            Position: d.Center,
            Confidence: 1.0f,
            IsFound: true,
            BoundingBox: d.BoundingRect,
            MarkerId: d.Id
        )).ToArray();
    }

    /// <inheritdoc />
    public bool Reinitialize(Mat currentFrame, int markerIndex, RectangleF searchRegion)
    {
        // ArUco markers auto-recover by ID — no manual reinitialization needed
        return true;
    }

    private List<DetectedArUco> DetectArUcoMarkers(Mat frame)
    {
        using var corners = new VectorOfVectorOfPointF();
        using var ids = new VectorOfInt();

        ArucoInvoke.DetectMarkers(frame, _dictionary, corners, ids, _detectorParameters);

        var results = new List<DetectedArUco>();
        var idsArray = ids.ToArray();

        for (int i = 0; i < idsArray.Length; i++)
        {
            var markerCorners = corners[i];
            var id = idsArray[i];

            float cx = 0, cy = 0;
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            for (int j = 0; j < markerCorners.Length; j++)
            {
                cx += markerCorners[j].X;
                cy += markerCorners[j].Y;
                minX = Math.Min(minX, markerCorners[j].X);
                minY = Math.Min(minY, markerCorners[j].Y);
                maxX = Math.Max(maxX, markerCorners[j].X);
                maxY = Math.Max(maxY, markerCorners[j].Y);
            }

            cx /= 4f;
            cy /= 4f;

            results.Add(new DetectedArUco(
                id,
                new PointF(cx, cy),
                new RectangleF(minX, minY, maxX - minX, maxY - minY)));
        }

        return results;
    }

    private static Dictionary CreateDictionary(ArucoDictionaryType type)
    {
        var predefined = type switch
        {
            ArucoDictionaryType.Dict4X4With50 => Dictionary.PredefinedDictionaryName.Dict4X4_50,
            ArucoDictionaryType.Dict4X4With100 => Dictionary.PredefinedDictionaryName.Dict4X4_100,
            ArucoDictionaryType.Dict5X5With100 => Dictionary.PredefinedDictionaryName.Dict5X5_100,
            ArucoDictionaryType.Dict5X5With250 => Dictionary.PredefinedDictionaryName.Dict5X5_250,
            ArucoDictionaryType.Dict6X6With250 => Dictionary.PredefinedDictionaryName.Dict6X6_250,
            _ => Dictionary.PredefinedDictionaryName.Dict4X4_50,
        };

        return new Dictionary(predefined);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _dictionary?.Dispose();
    }

    private readonly record struct DetectedArUco(int Id, PointF Center, RectangleF BoundingRect);
}
