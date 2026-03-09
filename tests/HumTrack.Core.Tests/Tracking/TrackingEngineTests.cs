using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using FluentAssertions;
using HumTrack.Core.Tracking;
using HumTrack.Core.Tracking.Engines;

namespace HumTrack.Core.Tests.Tracking;

/// <summary>
/// Functional tests for all tracking engines using synthetic images.
/// These tests create real images with known marker positions and verify
/// that each engine actually detects/tracks them correctly — not just compiles.
/// </summary>
public class TrackingEngineTests : IDisposable
{
    private const int FrameWidth = 640;
    private const int FrameHeight = 480;

    // Known marker positions — we draw bright circles here
    private static readonly PointF[] MarkerPositions =
    [
        new(200, 150),
        new(400, 300),
        new(100, 350),
    ];

    private readonly Mat _syntheticFrame;
    private readonly Mat _syntheticFrameShifted;

    public TrackingEngineTests()
    {
        // Create a dark frame with bright white circles as "markers"
        _syntheticFrame = CreateFrameWithMarkers(MarkerPositions);

        // Create a slightly shifted frame to simulate motion
        var shiftedPositions = MarkerPositions
            .Select(p => new PointF(p.X + 5, p.Y + 3))
            .ToArray();
        _syntheticFrameShifted = CreateFrameWithMarkers(shiftedPositions);
    }

    // ──────────── Template Matching ────────────

    [Fact]
    public void TemplateMatching_TracksWhiteCircles()
    {
        using var engine = new TemplateMatchingEngine();
        var regions = MarkerPositions.Select(p =>
            new RectangleF(p.X - 12, p.Y - 12, 24, 24)).ToList();

        engine.Initialize(_syntheticFrame, regions, new EngineSettings());
        var results = engine.Track(_syntheticFrameShifted);

        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.IsFound.Should().BeTrue(
            $"marker {r.MarkerIndex} should be found"));

        // Should detect the ~5px shift
        for (int i = 0; i < results.Length; i++)
        {
            var expected = new PointF(MarkerPositions[i].X + 5, MarkerPositions[i].Y + 3);
            var dist = Distance(results[i].Position, expected);
            dist.Should().BeLessThan(10f,
                $"marker {i} should track within 10px of actual (was {dist:F1}px off)");
        }
    }

    // ──────────── Optical Flow (Lucas-Kanade) ────────────

    [Fact]
    public void OpticalFlow_TracksAllPointsSimultaneously()
    {
        using var engine = new OpticalFlowEngine();
        var regions = MarkerPositions.Select(p =>
            new RectangleF(p.X - 10, p.Y - 10, 20, 20)).ToList();

        engine.Initialize(_syntheticFrame, regions, new EngineSettings());
        var results = engine.Track(_syntheticFrameShifted);

        results.Should().HaveCount(3);

        int found = results.Count(r => r.IsFound);
        found.Should().BeGreaterOrEqualTo(2,
            "at least 2 of 3 markers should survive optical flow on synthetic data");
    }

    // ──────────── KCF (Correlation Filter) ────────────

    [Fact]
    public void Kcf_TracksHighContrastMarkers()
    {
        using var engine = new KcfEngine();
        var regions = MarkerPositions.Select(p =>
            new RectangleF(p.X - 12, p.Y - 12, 24, 24)).ToList();

        engine.Initialize(_syntheticFrame, regions, new EngineSettings());
        var results = engine.Track(_syntheticFrameShifted);

        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.IsFound.Should().BeTrue());
    }

    // ──────────── CSRT (Spatial Reliability) ────────────

    [Fact]
    public void Csrt_TracksWithMultiChannelCorrelation()
    {
        using var engine = new CsrtEngine();
        var regions = MarkerPositions.Select(p =>
            new RectangleF(p.X - 12, p.Y - 12, 24, 24)).ToList();

        engine.Initialize(_syntheticFrame, regions, new EngineSettings());
        var results = engine.Track(_syntheticFrameShifted);

        results.Should().HaveCount(3);
        results.Should().AllSatisfy(r => r.IsFound.Should().BeTrue());
    }

    // ──────────── Blob Detection (Brightness) ────────────

    [Fact]
    public void BlobDetection_FindsBrightMarkersWithoutTemplate()
    {
        using var engine = new BlobDetectionEngine();
        var settings = new EngineSettings
        {
            BrightnessThreshold = 200,
            MinMarkerArea = 10,
            MaxMarkerArea = 2000,
            MinCircularity = 0.3,
        };

        // Initialize with empty regions — should auto-detect
        engine.Initialize(_syntheticFrame, Array.Empty<RectangleF>(), settings);
        var results = engine.Track(_syntheticFrame);

        results.Length.Should().BeGreaterOrEqualTo(3,
            "blob detection should find at least 3 bright circles");

        results.Should().AllSatisfy(r => r.IsFound.Should().BeTrue());
    }

    [Fact]
    public void BlobDetection_DetectMethodFindsMarkersInSinglePass()
    {
        using var engine = new BlobDetectionEngine();
        var settings = new EngineSettings
        {
            BrightnessThreshold = 200,
            MinMarkerArea = 10,
            MaxMarkerArea = 2000,
            MinCircularity = 0.3,
        };

        var results = engine.Detect(_syntheticFrame, settings);

        results.Length.Should().BeGreaterOrEqualTo(3);

        // Verify positions are near our known marker locations
        foreach (var marker in MarkerPositions)
        {
            var nearest = results.MinBy(r => Distance(r.Position, marker));
            Distance(nearest.Position, marker).Should().BeLessThan(5f,
                $"detected marker should be within 5px of ({marker.X}, {marker.Y})");
        }
    }

    // ──────────── Blob Detection (Color / HSV) ────────────

    [Fact]
    public void BlobDetection_FindsColoredMarkers()
    {
        // Create a frame with GREEN circles on dark background
        using var colorFrame = new Mat(FrameHeight, FrameWidth, DepthType.Cv8U, 3);
        colorFrame.SetTo(new MCvScalar(30, 30, 30));

        var greenPositions = new PointF[] { new(300, 200), new(100, 400) };
        foreach (var pos in greenPositions)
        {
            CvInvoke.Circle(colorFrame, Point.Round(pos), 12,
                new MCvScalar(0, 255, 0), -1); // BGR green
        }

        using var engine = new BlobDetectionEngine();
        var settings = new EngineSettings
        {
            ColorRange = HsvRange.Green,
            MinMarkerArea = 10,
            MaxMarkerArea = 2000,
            MinCircularity = 0.3,
        };

        var results = engine.Detect(colorFrame, settings);

        results.Length.Should().BeGreaterOrEqualTo(2,
            "should detect both green circles via HSV filtering");
    }

    // ──────────── Hybrid Engine ────────────

    [Fact]
    public void Hybrid_CombinesBlobAndOpticalFlow()
    {
        using var engine = new HybridEngine();
        var settings = new EngineSettings
        {
            BrightnessThreshold = 200,
            MinMarkerArea = 10,
            MaxMarkerArea = 2000,
            MinCircularity = 0.3,
            RedetectIntervalFrames = 5,
        };

        engine.Initialize(_syntheticFrame, Array.Empty<RectangleF>(), settings);

        // Track across multiple frames
        var r1 = engine.Track(_syntheticFrame);
        r1.Length.Should().BeGreaterOrEqualTo(3);

        var r2 = engine.Track(_syntheticFrameShifted);
        r2.Length.Should().BeGreaterOrEqualTo(3);
    }

    // ──────────── Registry ────────────

    [Fact]
    public void Registry_ListsAllSevenEngines()
    {
        TrackingEngineRegistry.AvailableEngines.Should().HaveCount(7);
    }

    [Fact]
    public void Registry_CreatesEngineByName()
    {
        foreach (var name in TrackingEngineRegistry.AvailableEngines)
        {
            using var engine = TrackingEngineRegistry.Create(name);
            engine.Should().NotBeNull();
            engine.Name.Should().Be(name);
        }
    }

    [Fact]
    public void Registry_CreatesOptimalEngineForMarkerType()
    {
        using var aruco = TrackingEngineRegistry.CreateForMarkerType(MarkerType.ArUco);
        aruco.Should().BeOfType<ArucoDetectionEngine>();

        using var retro = TrackingEngineRegistry.CreateForMarkerType(MarkerType.Retroreflective);
        retro.Should().BeOfType<HybridEngine>();

        using var color = TrackingEngineRegistry.CreateForMarkerType(MarkerType.ColoredSticker);
        color.Should().BeOfType<BlobDetectionEngine>();

        using var natural = TrackingEngineRegistry.CreateForMarkerType(MarkerType.NaturalFeature);
        natural.Should().BeOfType<KcfEngine>();
    }

    // ──────────── Helpers ────────────

    private static Mat CreateFrameWithMarkers(PointF[] positions)
    {
        var frame = new Mat(FrameHeight, FrameWidth, DepthType.Cv8U, 3);
        frame.SetTo(new MCvScalar(30, 30, 30)); // Dark background

        foreach (var pos in positions)
        {
            // Draw bright white circle (simulates retroreflective marker)
            CvInvoke.Circle(frame, Point.Round(pos), 10,
                new MCvScalar(255, 255, 255), -1);
        }

        return frame;
    }

    private static float Distance(PointF a, PointF b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    public void Dispose()
    {
        _syntheticFrame.Dispose();
        _syntheticFrameShifted.Dispose();
    }
}
