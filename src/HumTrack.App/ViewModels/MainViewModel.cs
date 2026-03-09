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
        _trackingEngine = new HybridEngine();
        _settings = new EngineSettings
        {
            BrightnessThreshold = 190,
            MinMarkerArea = 20,
            MaxMarkerArea = 6000,
            MinCircularity = 0.35
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
    public async Task PlayPause()
    {
        if (_capture == null || !_capture.IsOpened) return;

        if (IsPlaying)
            StopVideo();
        else
            await RunPlaybackLoopAsync();
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
                }

                // Track
                var results = _trackingEngine.Track(mat);

                // Convert to display-space TrackedRect objects
                var boxes = BuildOverlays(results);

                // Render
                var bmp = BitmapConverter.CreateWriteableBitmapFromMat(mat);
                int frameIdx = (int)_capture.Get(CapProp.PosFrames);
                int found = 0;
                foreach (var r in results) if (r.IsFound) found++;
                long elapsed = sw.ElapsedMilliseconds;

                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var old = CurrentFrame;
                    CurrentFrame = bmp;
                    old?.Dispose();

                    CurrentFrameIndex = frameIdx;

                    TrackingBoxes.Clear();
                    foreach (var b in boxes) TrackingBoxes.Add(b);

                    MetricsText = $"{found}/{results.Length} markers  |  {elapsed} ms/frame  |  ~{(elapsed > 0 ? 1000 / elapsed : 0)} FPS";
                });

                // Frame-rate limiter
                var wait = (int)(msPerFrame - sw.ElapsedMilliseconds);
                if (wait > 0) await Task.Delay(wait, token).ConfigureAwait(false);
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsPlaying = false;
                StatusText = "Playback complete.";
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
        const float R = 12f; // half-size of the seed box

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
        var boxes = new TrackedRect[results.Length];
        for (int i = 0; i < results.Length; i++)
        {
            var r = results[i];
            if (!r.IsFound) continue;

            boxes[i] = new TrackedRect
            {
                X = r.BoundingBox.X * _scaleX,
                Y = r.BoundingBox.Y * _scaleY,
                W = r.BoundingBox.Width * _scaleX,
                H = r.BoundingBox.Height * _scaleY,
                Label = $"M{i + 1}"
            };
        }
        return boxes;
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
