using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;

namespace HumTrack.Core.VideoProcessing.Filters;

/// <summary>
/// Applies a sharpening kernel to mitigate minor motion blur.
/// Useful for fast-moving subjects on lower-framerate cameras.
/// </summary>
public sealed class SharpeningFilter : IFrameProcessor
{
    private readonly Mat _kernel;

    /// <inheritdoc/>
    public string Name => "Sharpening (Motion Blur Compensation)";

    /// <inheritdoc/>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Defines how aggressive the unsharp mask is (0.0 to 1.0+)</summary>
    public double Intensity { get; set; } = 1.0;

    /// <summary>Creates a new sharpening filter using an unsharp mask approximation.</summary>
    public SharpeningFilter()
    {
        // Simple horizontal/vertical 3x3 sharpening kernel
        _kernel = new Mat(3, 3, DepthType.Cv32F, 1);
        float[] kernelValues = { 
             0, -1,  0, 
            -1,  5, -1, 
             0, -1,  0 
        };
        _kernel.SetTo(kernelValues);
    }

    /// <inheritdoc/>
    public void Process(Mat frame)
    {
        using var temp = new Mat();
        CvInvoke.Filter2D(frame, temp, _kernel, new Point(-1, -1));
        
        // Blend back based on intensity
        CvInvoke.AddWeighted(frame, 1.0 - Intensity, temp, Intensity, 0, frame);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _kernel.Dispose();
    }
}
