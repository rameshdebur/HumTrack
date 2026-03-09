using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Emgu.CV;
using Emgu.CV.CvEnum;
using HumTrack.App.Services;
using HumTrack.Core.Diagnostics;
using HumTrack.Core.Tracking;
using HumTrack.Core.Tracking.Engines;
using HumTrack.Core.VideoProcessing;
using HumTrack.Core.VideoProcessing.Filters;
using Serilog;

namespace HumTrack.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly ILogger _log = HumTrackLogger.ForContext<MainViewModel>();

#pragma warning disable CA1859
    private ITrackingEngine _trackingEngine;
#pragma warning restore CA1859

    private readonly VideoPipeline _pipeline;
    private readonly EngineSettings _settings;
    private VideoCapture? _capture;
    private CancellationTokenSource? _playbackCts;
    private double _fps;
    private int _totalFrames;

    // Scale factors from native video pixels → display pixels
    private double _scaleX = 1.0;
    private double _scaleY = 1.0;

    // Regions explicitly selected by the user on the canvas
    private readonly System.Collections.Generic.List<RectangleF> _userRegions = new();

    // ── Observable state ─────────────────────────────────────────────────────

    [ObservableProperty] private WriteableBitmap? _currentFrame;
    [ObservableProperty] private string _statusText = "Load a video file to begin.";
    [ObservableProperty] private string _metricsText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseText))]
    private bool _isPlaying;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVideo))]
    private bool _videoLoaded;

    [ObservableProperty] private int _currentFrameIndex;
    [ObservableProperty] private double _maximumFrameIndex = 1;
    [ObservableProperty] private int _markerCount;

    public bool HasVideo => VideoLoaded;
    public string PlayPauseText => IsPlaying ? "⏸ Pause" : "▶  Play";

    // Bounding boxes rendered on the overlay canvas
    public ObservableCollection<TrackedRect> TrackingBoxes { get; } = new();

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainViewModel()
    {
        // TemplateMatchingEngine: extracts an NCC patch from whatever the user clicked.
        // Works on ANY video appearance (body landmarks, markers, joints etc).
        // HybridEngine is better when physical bright-white blob markers are used.
        _trackingEngine = new TemplateMatchingEngine();
        _settings = new EngineSettings
        {
            // Blob detection params (used if switching to HybridEngine)
            BrightnessThreshold = 190,
            MinMarkerArea = 20,
            MaxMarkerArea = 8000,
            MinCircularity = 0.3,

            // TemplateMatchingEngine params:
            // Large search window for walking person motion (up to ~150px/frame at 30fps)
            SearchWindow      = new System.Drawing.Size(200, 200),
            // Lower confidence threshold: body landmarks are less distinctive than markers
            ConfidenceThreshold = 0.45,
            // Moderate template evolution: handles appearance changes (lighting, pose)
            TemplateEvolveRate  = 0.25
        };

        _pipeline = new VideoPipeline();
        _pipeline.AddProcessor(new SharpeningFilter());
        _pipeline.AddProcessor(new ContrastEnhancementFilter());
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>Opens a video file and shows the first frame.</summary>
    public void LoadVideo(string filePath)
    {
        StopVideo();
        TrackingBoxes.Clear();
        _userRegions.Clear();
        MarkerCount = 0;

        if (!File.Exists(filePath))
        {
            StatusText = $"File not found: {filePath}";
            return;
        }

        try
        {
            _capture?.Dispose();
            _capture = new VideoCapture(filePath);

            if (!_capture.IsOpened)
            {
                StatusText = "Failed to open video.";
                return;
            }

            _fps = _capture.Get(CapProp.Fps);
            if (_fps <= 0) _fps = 30;

            _totalFrames = (int)_capture.Get(CapProp.FrameCount);
            MaximumFrameIndex = Math.Max(1, _totalFrames - 1);
            CurrentFrameIndex = 0;
            VideoLoaded = true;

            StatusText = $"Loaded: {Path.GetFileName(filePath)} | {_fps:F1} FPS · {_totalFrames} frames · Click to mark";
            _log.Information("Video loaded: {File}, {Fps} fps, {Frames} frames", filePath, _fps, _totalFrames);

            ShowFirstFrame();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Load failed: {Path}", filePath);
            StatusText = $"Error: {ex.Message}";
        }
    }

    private void ShowFirstFrame()
    {
        if (_capture == null || !_capture.IsOpened) return;

        _capture.Set(CapProp.PosFrames, 0);
        using var mat = new Mat();
        if (_capture.Read(mat) && !mat.IsEmpty)
        {
            _pipeline.Process(mat);
            SetFrame(mat);
        }
        _capture.Set(CapProp.PosFrames, 0);
    }

    [RelayCommand]
    public void PlayPause()
    {
        if (_capture == null || !_capture.IsOpened)
        {
            StatusText = "Load a video first.";
            return;
        }

        if (IsPlaying)
        {
            StopVideo();
        }
        else
        {
            _log.Information("Play pressed — {MarkerCount} markers registered, starting playback", _userRegions.Count);
            // Fire-and-forget: necessary because RelayCommand doesn't properly await async Tasks
            _ = RunPlaybackLoopAsync();
        }
    }

    [RelayCommand]
    public void Stop()
    {
        StopVideo();
        _userRegions.Clear();
        MarkerCount = 0;
        TrackingBoxes.Clear();
        ShowFirstFrame();
        StatusText = "Stopped. Click markers on the video to reinitialise.";
    }

    // ── Playback loop ─────────────────────────────────────────────────────────

    private async Task RunPlaybackLoopAsync()
    {
        if (_capture == null || !_capture.IsOpened || IsPlaying) return;

        _playbackCts = new CancellationTokenSource();
        var token = _playbackCts.Token;
        IsPlaying = true;

        var msPerFrame = 1000.0 / _fps;
        var sw = new Stopwatch();
        bool engineSeeded = false;

        await Task.Run(async () =>
        {
            using var mat = new Mat();

            try
            {
                while (!token.IsCancellationRequested)
                {
                    sw.Restart();

                    if (!_capture.Read(mat) || mat.IsEmpty)
                        break; // EOF

                    // Pre-processing
                    _pipeline.Process(mat);

                    // Seed the engine once we have the first frame
                    if (!engineSeeded)
                    {
                        _trackingEngine.Initialize(mat, _userRegions, _settings);
                        engineSeeded = true;
                        _log.Information("Engine initialized with {N} regions", _userRegions.Count);
                    }

                    // Track
                    var results = _trackingEngine.Track(mat);

                    // Convert to display-space TrackedRect objects (no nulls)
                    var boxes = BuildOverlays(results);

                    // Render — create new bitmap before UI dispatch
                    var bmp = BitmapConverter.CreateWriteableBitmapFromMat(mat);
                    int frameIdx = (int)_capture.Get(CapProp.PosFrames);
                    int found = boxes.Length;
                    long elapsed = sw.ElapsedMilliseconds;

                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        // Swap frame — don't dispose old one immediately; let GC handle it
                        // to avoid disposing a bitmap Avalonia's renderer is still using
                        CurrentFrame = bmp;

                        CurrentFrameIndex = frameIdx;

                        TrackingBoxes.Clear();
                        foreach (var b in boxes) TrackingBoxes.Add(b);

                        MetricsText = $"{found} markers  |  {elapsed} ms/frame";
                    });

                    // Frame-rate limiter
                    var wait = (int)(msPerFrame - sw.ElapsedMilliseconds);
                    if (wait > 0) await Task.Delay(wait, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                // Normal stop — ignore
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Playback loop crashed");
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    StatusText = $"Playback error: {ex.Message}");
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsPlaying = false;
                StatusText = IsPlaying ? StatusText : "Playback complete — press Stop to reset markers.";
            });

        }, token).ConfigureAwait(false);
    }

    private void StopVideo()
    {
        IsPlaying = false;
        _playbackCts?.Cancel();
        _playbackCts?.Dispose();
        _playbackCts = null;
    }

    // ── Marker picking ────────────────────────────────────────────────────────

    /// <summary>
    /// Called by MainWindow when the user clicks the video image.
    /// x/y are in display pixels; we must reverse-scale them back to native video pixels.
    /// </summary>
    public void AddMarkerAt(double displayX, double displayY)
    {
        if (IsPlaying || !VideoLoaded) return;

        // Map display → native video pixel space
        float vx = (float)(displayX / _scaleX);
        float vy = (float)(displayY / _scaleY);
        // Larger seed box captures more texture context for template matching
        const float R = 40f;

        _userRegions.Add(new RectangleF(vx - R, vy - R, R * 2, R * 2));
        MarkerCount = _userRegions.Count;

        // Immediately show the click box so the user gets feedback before Play
        TrackingBoxes.Add(new TrackedRect
        {
            X = displayX - R * _scaleX,
            Y = displayY - R * _scaleY,
            W = R * 2 * _scaleX,
            H = R * 2 * _scaleY,
            Label = $"M{_userRegions.Count}"
        });

        StatusText = $"{_userRegions.Count} marker(s) selected — click more or press Play";
        _log.Information("Marker {N} placed at native ({X:F0},{Y:F0})", _userRegions.Count, vx, vy);
    }

    /// <summary>
    /// Called by code-behind when the display Image has been measured,
    /// so we can compute the scale between display pixels and native video pixels.
    /// </summary>
    public void UpdateScale(double displayW, double displayH)
    {
        if (_capture == null || !_capture.IsOpened) return;

        double nativeW = _capture.Get(CapProp.FrameWidth);
        double nativeH = _capture.Get(CapProp.FrameHeight);

        if (nativeW > 0 && nativeH > 0)
        {
            // Uniform scaling — whichever axis is the tighter constraint
            double scale = Math.Min(displayW / nativeW, displayH / nativeH);
            _scaleX = scale;
            _scaleY = scale;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private TrackedRect[] BuildOverlays(TrackingResult[] results)
    {
        // Use a list — skip lost markers entirely (no null entries)
        var boxes = new System.Collections.Generic.List<TrackedRect>(results.Length);
        for (int i = 0; i < results.Length; i++)
        {
            var r = results[i];
            if (!r.IsFound) continue;
            if (r.BoundingBox.Width <= 0 || r.BoundingBox.Height <= 0) continue;

            boxes.Add(new TrackedRect
            {
                X = r.BoundingBox.X * _scaleX,
                Y = r.BoundingBox.Y * _scaleY,
                W = r.BoundingBox.Width  * _scaleX,
                H = r.BoundingBox.Height * _scaleY,
                Label = $"M{i + 1}"
            });
        }
        return boxes.ToArray();
    }

    private void SetFrame(Mat mat)
    {
        var bmp = BitmapConverter.CreateWriteableBitmapFromMat(mat);
        var old = CurrentFrame;
        CurrentFrame = bmp;
        old?.Dispose();
    }

    public void Dispose()
    {
        StopVideo();
        _capture?.Dispose();
        _pipeline.Dispose();
        _trackingEngine.Dispose();
    }
}
