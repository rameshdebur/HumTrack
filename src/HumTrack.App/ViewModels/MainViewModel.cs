using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
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
    private VideoCapture? _capture;
#pragma warning disable CA1859 // Change type of field for improved performance
    private ITrackingEngine _trackingEngine;
#pragma warning restore CA1859
    private readonly VideoPipeline _pipeline;
    private readonly EngineSettings _settings;
    
    private CancellationTokenSource? _playbackCts;
    private double _fps;
    private int _totalFrames;

    public MainViewModel()
    {
        // Setup initial default tracking strategy (Hybrid handles motion blur & stops best)
        _trackingEngine = new HybridEngine();
        _settings = new EngineSettings {
            BrightnessThreshold = 200,
            MinMarkerArea = 20,
            MaxMarkerArea = 5000,
            MinCircularity = 0.4
        };

        // Initialize core video filter pipeline 
        _pipeline = new VideoPipeline();
        _pipeline.AddProcessor(new SharpeningFilter()); 
        // Note: For lens undistortion we'd slot the UndistortionFilter in here

        StatusText = "Ready to load video.";
    }

    [ObservableProperty]
    private WriteableBitmap? _currentFrame;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _metricsText = string.Empty;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private int _currentFrameIndex;

    [ObservableProperty]
    private double _maximumFrameIndex = 1;

    // A collection of rectangles representing the tracking visualization
    // We bind these mapped ROIs visually out onto the Avalonia canvas
    public ObservableCollection<Avalonia.Rect> TrackingBoxes { get; } = new();

    // Represents markers explicitly requested by user clicks in UI
    private readonly System.Collections.Generic.List<RectangleF> _userInitializedRegions = new();

    /// <summary>Open a video file and prepare it for reading.</summary>
    public void LoadVideo(string filePath)
    {
        StopVideo();

        if (!File.Exists(filePath))
        {
            StatusText = $"File not found: {filePath}";
            return;
        }

        try
        {
            _capture = new VideoCapture(filePath);
            _fps = _capture.Get(CapProp.Fps);
            _totalFrames = (int)_capture.Get(CapProp.FrameCount);
            MaximumFrameIndex = _totalFrames > 0 ? _totalFrames - 1 : 1;
            CurrentFrameIndex = 0;

            StatusText = $"Loaded: {Path.GetFileName(filePath)} ({_fps:F1} FPS, {_totalFrames} frames)";
            _log.Information("Video loaded successfully: {FilePath}", filePath);

            // Extract the first frame to display
            SeekToStart();
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Failed to load video: {Path}", filePath);
            StatusText = $"Error Loading: {ex.Message}";
        }
    }

    /// <summary>Grabs a specific frame, bypassing tracking.</summary>
    private void SeekToStart()
    {
        if (_capture == null || !_capture.IsOpened) return;
        
        _capture.Set(CapProp.PosFrames, 0);
        CurrentFrameIndex = 0;
        using var previewMat = new Mat();
        if (_capture.Read(previewMat))
        {
            _pipeline.Process(previewMat);
            UpdateCurrentFrame(previewMat);
        }
        _capture.Set(CapProp.PosFrames, 0); // Put the cursor back
    }

    [RelayCommand]
    public async Task PlayPause()
    {
        if (_capture == null || !_capture.IsOpened) return;

        if (IsPlaying)
        {
            StopVideo();
        }
        else
        {
            await StartVideoAsync();
        }
    }

    [RelayCommand]
    public void Stop()
    {
        StopVideo();
        SeekToStart();
        TrackingBoxes.Clear();
        _userInitializedRegions.Clear();
        StatusText = "Stopped. Click markers to re-initialize.";
    }

    /// <summary>Main playback and tracking loop.</summary>
    private async Task StartVideoAsync()
    {
        if (_capture == null || !_capture.IsOpened || IsPlaying) return;

        _playbackCts = new CancellationTokenSource();
        var token = _playbackCts.Token;
        IsPlaying = true;
        StatusText = $"Playing and Tracking with {_trackingEngine.Name}...";

        var intervalMs = (int)(1000.0 / _fps);
        var sw = new Stopwatch();

        // Read and process the video exactly like our VideoFileTestHarness 
        // but piped into the UI threading model
        await Task.Run(async () =>
        {
            using var frameMat = new Mat();
            bool engineInitialized = false;

            while (IsPlaying && !token.IsCancellationRequested)
            {
                sw.Restart();

                if (!_capture.Read(frameMat) || frameMat.IsEmpty)
                {
                    // Reached EOF
                    break;
                }

                // Stage 1: Pre-process frame (Contrast, Sharpening, Undistortion)
                _pipeline.Process(frameMat);

                // Stage 2: Initialize engine if necessary (only once per play button press)
                if (!engineInitialized)
                {
                    // If user manually clicked some ROI points on the canvas, force those
                    _trackingEngine.Initialize(frameMat, _userInitializedRegions, _settings);
                    engineInitialized = true;
                }

                // Stage 3: Actually execute the tracking engine calculations 
                var results = _trackingEngine.Track(frameMat);

                // Stage 4: Draw UI 
                var bitmap = BitmapConverter.CreateWriteableBitmapFromMat(frameMat);
                
                // We dispatch the visual updates so we don't crash Avalonia reading off-thread
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    // Render image surface
                    var oldFrame = CurrentFrame;
                    CurrentFrame = bitmap;
                    oldFrame?.Dispose(); // Discard the old unmanaged buffer immediately

                    // Update Playhead slider
                    CurrentFrameIndex = (int)_capture.Get(CapProp.PosFrames);

                    // Rebuild bounding boxes on the UI overlay mapped 1:1 on the pixel plane
                    TrackingBoxes.Clear();
                    int foundCount = 0;
                    foreach (var result in results)
                    {
                        if (result.IsFound)
                        {
                            foundCount++;
                            // C# drawing rect to Avalonia rect mapper
                            TrackingBoxes.Add(new Avalonia.Rect(
                                result.BoundingBox.X, 
                                result.BoundingBox.Y, 
                                result.BoundingBox.Width, 
                                result.BoundingBox.Height));
                        }
                    }

                    MetricsText = $"{foundCount}/{results.Length} Markers Tracked | CPU Frame: {sw.ElapsedMilliseconds}ms";
                });

                // Frame-rate limiting (if the tracking runs at 250 FPS, we don't want to fast-forward the video 10x speed)
                var timeRemaining = intervalMs - (int)sw.ElapsedMilliseconds;
                if (timeRemaining > 0)
                {
                    await Task.Delay(timeRemaining, token);
                }
            }

            // End of line
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                IsPlaying = false;
                StatusText = "Playback finished.";
            });

        }, token);
    }

    private void StopVideo()
    {
        IsPlaying = false;
        _playbackCts?.Cancel();
        _playbackCts?.Dispose();
        _playbackCts = null;
    }

    /// <summary>
    /// Invoked directly by the View code-behind when the user clicks the Video Canvas 
    /// dropping a new explicit marker. 
    /// </summary>
    public void AddManualMarker(float x, float y)
    {
        if (IsPlaying) return; // Disallow manual marking while video is playing

        // Roughly a 20x20 window around the click
        _userInitializedRegions.Add(new RectangleF(x - 10, y - 10, 20, 20));
        
        // Add it to the visual box representation immediately so the user sees it
        TrackingBoxes.Add(new Avalonia.Rect(x - 10, y - 10, 20, 20));
        
        StatusText = $"Total Locked Markers: {_userInitializedRegions.Count}";
    }

    private void UpdateCurrentFrame(Mat mat)
    {
        var bitmap = BitmapConverter.CreateWriteableBitmapFromMat(mat);
        var old = CurrentFrame;
        CurrentFrame = bitmap;
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
