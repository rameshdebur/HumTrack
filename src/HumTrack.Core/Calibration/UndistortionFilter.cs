using System.Collections.Concurrent;
using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using HumTrack.Core.VideoProcessing;
using HumTrack.Core.Diagnostics;

namespace HumTrack.Core.Calibration;

/// <summary>
/// Automatically corrects both Barrel and Pincushion lens distortion in real-time.
/// Uses pre-calculated rectification maps mapping 2D curved pixels back to pure Euclidean planes
/// for scientifically-valid spatial measurements.
///
/// MUST be added to the VideoPipeline BEFORE any Tracking engines run.
/// </summary>
public sealed class UndistortionFilter : IFrameProcessor
{
    private readonly CameraIntrinsics _intrinsics;
    private readonly Mat _map1;
    private readonly Mat _map2;
    private readonly Serilog.ILogger _log = HumTrackLogger.ForContext<UndistortionFilter>();
    
    // We pool output matrices to prevent heavy garbage collection on every frame
    private readonly ConcurrentStack<Mat> _bufferPool = new();

    /// <inheritdoc/>
    public string Name => "Lens Distortion Correction";

    /// <inheritdoc/>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Creates the filter utilizing pre-calculated mathematical constants from calibration.</summary>
    public UndistortionFilter(CameraIntrinsics intrinsics)
    {
        _intrinsics = intrinsics ?? throw new ArgumentNullException(nameof(intrinsics));
        
        // Caches the heavy transformation maths up front
        _intrinsics.GenerateRectificationMaps(out _map1, out _map2);
        
        // Pre-allocate a couple of buffers for the video pipeline
        _bufferPool.Push(new Mat());
        _bufferPool.Push(new Mat());
    }

    /// <inheritdoc/>
    public void Process(Mat frame)
    {
        if (frame.Width != _intrinsics.ImageWidth || frame.Height != _intrinsics.ImageHeight)
        {
            _log.Warning("Frame dims ({W}x{H}) do not match Intrinsics ({IW}x{IH})! Disabling Undistortion.", 
                frame.Width, frame.Height, _intrinsics.ImageWidth, _intrinsics.ImageHeight);
            IsEnabled = false;
            return;
        }

        // Fast remapping of curved pixels -> flat plane
        if (!_bufferPool.TryPop(out var temp))
        {
            temp = new Mat();
        }

        // Must remap off a clone or secondary buffer, cannot remap directly in-place
        frame.CopyTo(temp);
        
        // Execute unwarp over entire image
        CvInvoke.Remap(
            temp,
            frame, // Write the flattened result directly back onto the original frame passed down the pipeline
            _map1,
            _map2,
            Inter.Linear, // Fast enough for 60fps+, clean enough for tracking
            BorderType.Constant);

        _bufferPool.Push(temp);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _map1.Dispose();
        _map2.Dispose();
        
        while (_bufferPool.TryPop(out var mat))
        {
            mat.Dispose();
        }
    }
}
