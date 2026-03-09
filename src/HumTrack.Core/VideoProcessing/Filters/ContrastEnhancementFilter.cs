using Emgu.CV;
using Emgu.CV.CvEnum;

namespace HumTrack.Core.VideoProcessing.Filters;

/// <summary>
/// Applies Contrast Limited Adaptive Histogram Equalization (CLAHE) on the luminance
/// channel. Substantially improves tracking in poorly-lit or highly variable lighting environments
/// without destroying the color data (which Blob/HSV tracking needs).
/// </summary>
public sealed class ContrastEnhancementFilter : IFrameProcessor
{
    private readonly uint _clipLimit;
    private readonly System.Drawing.Size _tileGridSize;

    /// <inheritdoc/>
    public string Name => "Adaptive Contrast (CLAHE)";

    /// <inheritdoc/>
    public bool IsEnabled { get; set; } = true;

    /// <param name="clipLimit">Higher values = more contrast. (Usually 2 to 4)</param>
    /// <param name="gridSize">Tile size for adaptive histogram (usually 8x8)</param>
    public ContrastEnhancementFilter(uint clipLimit = 2, int gridSize = 8)
    {
        _clipLimit = clipLimit;
        _tileGridSize = new System.Drawing.Size(gridSize, gridSize);
    }

    /// <inheritdoc/>
    public void Process(Mat frame)
    {
        using var lab = new Mat();
        // Convert to LAB color space to isolate luminance (L channel)
        CvInvoke.CvtColor(frame, lab, ColorConversion.Bgr2Lab);

        using var mvLab = new Emgu.CV.Util.VectorOfMat();
        CvInvoke.Split(lab, mvLab);

        // Apply CLAHE only to the L channel
        using var sourceL = mvLab[0];
        using var destL = new Mat();
        CvInvoke.CLAHE(sourceL, _clipLimit, _tileGridSize, destL);

        // Replace L channel
        using var mvMerged = new Emgu.CV.Util.VectorOfMat();
        mvMerged.Push(destL);
        mvMerged.Push(mvLab[1]);
        mvMerged.Push(mvLab[2]);

        CvInvoke.Merge(mvMerged, lab);

        // Convert back to BGR in-place
        CvInvoke.CvtColor(lab, frame, ColorConversion.Lab2Bgr);
    }

    /// <inheritdoc/>
    public void Dispose() { }
}
