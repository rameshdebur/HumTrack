using System.Diagnostics;
using Serilog;

namespace HumTrack.Core.Diagnostics;

/// <summary>
/// Performance metrics tracker for tracking engine operations.
/// Logs per-frame timing, markers-per-second, and drift warnings.
/// </summary>
public sealed class PerformanceMonitor
{
    private readonly ILogger _log;
    private readonly Stopwatch _frameStopwatch = new();
    private readonly List<double> _frameTimes = new();
    private int _totalFramesProcessed;
    private int _totalMarkersLost;

    /// <summary>
    /// Initializes the performance monitor for a specific engine.
    /// </summary>
    /// <param name="engineName">Display name of the tracking engine.</param>
    public PerformanceMonitor(string engineName)
    {
        _log = HumTrackLogger.ForEngine(engineName);
    }

    /// <summary>Call before processing each frame.</summary>
    public void BeginFrame()
    {
        _frameStopwatch.Restart();
    }

    /// <summary>
    /// Call after processing each frame. Logs timing and detects slowdowns.
    /// </summary>
    /// <param name="markersFound">Number of markers successfully tracked this frame.</param>
    /// <param name="totalMarkers">Total number of markers being tracked.</param>
    public void EndFrame(int markersFound, int totalMarkers)
    {
        _frameStopwatch.Stop();
        var elapsed = _frameStopwatch.Elapsed.TotalMilliseconds;
        _frameTimes.Add(elapsed);
        _totalFramesProcessed++;

        var lost = totalMarkers - markersFound;
        _totalMarkersLost += lost;

        // Log every 100 frames with summary stats
        if (_totalFramesProcessed % 100 == 0)
        {
            var avgMs = _frameTimes.Average();
            var maxMs = _frameTimes.Max();
            var fps = avgMs > 0 ? 1000.0 / avgMs : 0;

            _log.Information(
                "Perf summary — Frames: {Total}, Avg: {AvgMs:F2}ms, Max: {MaxMs:F2}ms, " +
                "FPS: {FPS:F1}, Markers lost: {Lost}",
                _totalFramesProcessed, avgMs, maxMs, fps, _totalMarkersLost);

            _frameTimes.Clear();
        }

        // Warn if a single frame took too long (>50ms = <20 FPS)
        if (elapsed > 50)
        {
            _log.Warning("Slow frame: {ElapsedMs:F2}ms (markers found: {Found}/{Total})",
                elapsed, markersFound, totalMarkers);
        }

        // Error if markers are being lost
        if (lost > 0)
        {
            _log.Warning("Markers lost this frame: {Lost}/{Total}", lost, totalMarkers);
        }
    }

    /// <summary>Gets the total number of frames processed.</summary>
    public int TotalFrames => _totalFramesProcessed;

    /// <summary>Gets the total number of marker-loss events.</summary>
    public int TotalMarkersLost => _totalMarkersLost;
}
