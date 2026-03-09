using Emgu.CV;
using Emgu.CV.CvEnum;
using System.Drawing;

namespace HumTrack.Core.VideoProcessing.Filters;

/// <summary>
/// Applies an unsharp mask to mitigate minor motion blur.
/// Uses a safe two-pass approach: blur → subtract → blend.
/// Useful for fast-moving subjects on lower-framerate cameras.
/// </summary>
public sealed class SharpeningFilter : IFrameProcessor
{
    /// <inheritdoc/>
    public string Name => "Sharpening (Motion Blur Compensation)";

    /// <inheritdoc/>
    public bool IsEnabled { get; set; } = true;

    /// <summary>Sigma for the Gaussian blur step (higher = stronger sharpening effect).</summary>
    public double BlurSigma { get; set; } = 1.0;

    /// <summary>How much sharpening to blend in (0=none, 1=full, 1.5=aggressive).</summary>
    public double Intensity { get; set; } = 0.6;

    /// <summary>Creates a new sharpening filter using an unsharp mask approximation.</summary>
    public SharpeningFilter() { }

    /// <inheritdoc/>
    public void Process(Mat frame)
    {
        // Unsharp mask: sharpened = original + (original - blurred) * intensity
        // This is numerically equivalent to: sharpened = (1+intensity)*original - intensity*blurred
        // We do it safely without in-place read/write conflicts.
        using var blurred = new Mat();

        // Gaussian blur as the low-frequency component
        CvInvoke.GaussianBlur(frame, blurred, new Size(0, 0), BlurSigma);

        // AddWeighted: dst = srcA*alpha + srcB*beta + gamma
        // dst must be a separate Mat from both srcs to avoid aliasing
        using var sharpened = new Mat();
        CvInvoke.AddWeighted(frame, 1.0 + Intensity, blurred, -Intensity, 0, sharpened);

        // Copy back into the original frame buffer
        sharpened.CopyTo(frame);
    }

    /// <inheritdoc/>
    public void Dispose() { }
}
