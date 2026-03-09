using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using HumTrack.Core.Diagnostics;
using HumTrack.Core.Tracking;
using HumTrack.Core.Tracking.Engines;
using Serilog;

namespace HumTrack.Core.Tests.Tracking;

/// <summary>
/// Test harness for running tracking engines against real video files.
/// Place test videos in the "TestData" folder next to the test assembly.
///
/// To use: drop a video file with visible markers and set the path below.
/// The harness will run each engine and report results to the test output AND log files.
/// </summary>
public class VideoFileTestHarness : IDisposable
{
    // ──── SET THIS TO YOUR VIDEO FILE PATH ────
    // Can also be a relative path in the TestData folder
    private const string TestVideoPath = @"C:\Users\rams2\code projects\HumTrack\test videos\VID_20260307_150203282.mp4";

    private readonly ILogger _log;

    public VideoFileTestHarness()
    {
        HumTrackLogger.Initialize(Path.Combine(Path.GetTempPath(), "HumTrack_TestLogs"));
        _log = HumTrackLogger.ForContext<VideoFileTestHarness>();
    }

    [Fact]
    public void RunBlobDetectionOnRealVideo()
    {
        RunEngineOnVideo(
            new BlobDetectionEngine(),
            new EngineSettings
            {
                BrightnessThreshold = 200,
                MinMarkerArea = 20,
                MaxMarkerArea = 5000,
                MinCircularity = 0.4,
            },
            maxFrames: 300,
            requireManualInit: true);
    }

    [Fact]
    public void RunHybridOnRealVideo()
    {
        RunEngineOnVideo(
            new HybridEngine(),
            new EngineSettings
            {
                BrightnessThreshold = 200,
                MinMarkerArea = 20,
                MaxMarkerArea = 5000,
                MinCircularity = 0.4,
                RedetectIntervalFrames = 30,
            },
            maxFrames: 300,
            requireManualInit: true);
    }

    [Fact]
    public void RunOpticalFlowOnRealVideo()
    {
        RunEngineOnVideo(
            new OpticalFlowEngine(),
            new EngineSettings(),
            maxFrames: 300,
            requireManualInit: true);
    }

    private void RunEngineOnVideo(
        ITrackingEngine engine,
        EngineSettings settings,
        int maxFrames = 300,
        bool requireManualInit = false)
    {
        var videoPath = Path.IsPathRooted(TestVideoPath)
            ? TestVideoPath
            : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, TestVideoPath);

        if (!File.Exists(videoPath))
        {
            _log.Warning("Test video not found at {Path} — skipping", videoPath);
            return;
        }

        using var capture = new VideoCapture(videoPath);
        if (!capture.IsOpened)
        {
            _log.Error("Failed to open video: {Path}", videoPath);
            return;
        }

        var fps = capture.Get(CapProp.Fps);
        var totalFrames = (int)capture.Get(CapProp.FrameCount);
        _log.Information("Video: {Path}, FPS: {FPS}, Total frames: {Total}",
            videoPath, fps, totalFrames);

        var perf = new PerformanceMonitor(engine.Name);
        using var frame = new Mat();
        int frameIndex = 0;
        bool initialized = false;

        while (capture.Read(frame) && frameIndex < maxFrames)
        {
            if (frame.IsEmpty) break;

            if (!initialized)
            {
                if (requireManualInit)
                {
                    // For non-template-free engines, detect markers in first frame
                    // using blob detection, then hand those regions to the engine
                    using var detector = new BlobDetectionEngine();
                    var detected = detector.Detect(frame, settings);
                    var regions = detected
                        .Select(r => r.BoundingBox)
                        .OrderBy(r => r.Y) // Sort top-to-bottom
                        .Skip(2) // Skip overhead lights/noise if any
                        .Take(3) // SIMULATE UI: User clicks exactly 3 markers
                        .ToList();

                    _log.Information("Simulating UI workflow: initialized {Engine} with exactly {Count} markers: {Positions}",
                        engine.Name, regions.Count, string.Join(", ", regions.Select(r => $"({r.X:F1},{r.Y:F1})")));

                    engine.Initialize(frame, regions, settings);
                }
                else
                {
                    engine.Initialize(frame, Array.Empty<RectangleF>(), settings);
                }

                initialized = true;
                frameIndex++;
                continue;
            }

            perf.BeginFrame();
            var results = engine.Track(frame);
            var found = results.Count(r => r.IsFound);
            perf.EndFrame(found, results.Length);

            if (frameIndex % 50 == 0)
            {
                _log.Information(
                    "Frame {Frame}: {Found}/{Total} markers, positions: [{Positions}]",
                    frameIndex, found, results.Length,
                    string.Join(", ", results.Where(r => r.IsFound)
                        .Select(r => $"({r.Position.X:F1},{r.Position.Y:F1})")));
            }

            frameIndex++;
        }

        _log.Information(
            "Video test complete — {Engine}: {Frames} frames, {Lost} total markers lost",
            engine.Name, perf.TotalFrames, perf.TotalMarkersLost);

        engine.Dispose();
    }

    public void Dispose()
    {
        HumTrackLogger.Shutdown();
    }
}
