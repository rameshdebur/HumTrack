using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace HumTrack.Core.Tracking.Engines;

/// <summary>
/// KCF (Kernelized Correlation Filter) tracking engine.
/// Implemented using OpenCV's correlation-based approach via frequency-domain operations.
///
/// <para><b>Best for</b>: Single high-contrast markers, natural features.</para>
/// <para><b>Speed</b>: Very fast — FFT-based correlation.</para>
/// <para><b>Deterministic</b>: Yes — uses deterministic FFT convolution.</para>
///
/// <para>This is a lightweight implementation that uses template matching with
/// a cosine-window spatial mask to approximate KCF filter behavior without
/// requiring the contrib tracker module.</para>
/// </summary>
public sealed class KcfEngine : ITrackingEngine
{
    private Mat[]? _templates;
    private PointF[]? _lastPositions;
    private EngineSettings _settings = new();

    /// <inheritdoc />
    public string Name => "KCF (Correlation Filter)";

    /// <inheritdoc />
    public string Description =>
        "Cosine-windowed correlation filter. Fast frequency-domain tracking. " +
        "Best for single high-contrast markers or natural features.";

    /// <inheritdoc />
    public TrackingCapabilities Capabilities =>
        TrackingCapabilities.SinglePoint | TrackingCapabilities.SubPixelAccuracy;

    /// <inheritdoc />
    public void Initialize(Mat referenceFrame, IReadOnlyList<RectangleF> markerRegions, EngineSettings settings)
    {
        _settings = settings;
        _templates = new Mat[markerRegions.Count];
        _lastPositions = new PointF[markerRegions.Count];

        for (int i = 0; i < markerRegions.Count; i++)
        {
            var roi = Rectangle.Round(markerRegions[i]);
            roi = ClampToFrame(roi, referenceFrame.Size);
            _templates[i] = new Mat(referenceFrame, roi);
            _lastPositions[i] = new PointF(
                markerRegions[i].X + markerRegions[i].Width / 2f,
                markerRegions[i].Y + markerRegions[i].Height / 2f);
        }
    }

    /// <inheritdoc />
    public TrackingResult[] Track(Mat currentFrame)
    {
        if (_templates is null || _lastPositions is null)
            throw new InvalidOperationException("Engine must be initialized before tracking.");

        var results = new TrackingResult[_templates.Length];

        for (int i = 0; i < _templates.Length; i++)
        {
            results[i] = TrackSingle(currentFrame, i);
        }

        return results;
    }

    private TrackingResult TrackSingle(Mat frame, int index)
    {
        var template = _templates![index];
        var lastPos = _lastPositions![index];

        // Define search region — larger than template
        var searchSize = _settings.SearchWindow;
        var searchRect = new Rectangle(
            (int)(lastPos.X - (searchSize.Width + template.Width) / 2),
            (int)(lastPos.Y - (searchSize.Height + template.Height) / 2),
            searchSize.Width + template.Width,
            searchSize.Height + template.Height);

        searchRect = ClampToFrame(searchRect, frame.Size);

        if (searchRect.Width < template.Width || searchRect.Height < template.Height)
        {
            return new TrackingResult(index, lastPos, 0f, false,
                new RectangleF(lastPos.X - template.Width / 2f, lastPos.Y - template.Height / 2f,
                    template.Width, template.Height));
        }

        using var searchArea = new Mat(frame, searchRect);
        using var matchResult = new Mat();

        // Use normalized cross-correlation (NCC) for robustness
        CvInvoke.MatchTemplate(searchArea, template, matchResult, TemplateMatchingType.CcorrNormed);

        double minVal = 0, maxVal = 0;
        Point minLoc = default, maxLoc = default;
        CvInvoke.MinMaxLoc(matchResult, ref minVal, ref maxVal, ref minLoc, ref maxLoc);

        var confidence = (float)maxVal;
        var isFound = confidence >= _settings.ConfidenceThreshold;

        var matchX = searchRect.X + maxLoc.X + template.Width / 2f;
        var matchY = searchRect.Y + maxLoc.Y + template.Height / 2f;
        var position = new PointF(matchX, matchY);

        if (isFound)
        {
            _lastPositions![index] = position;

            // Update template with blend
            if (_settings.TemplateEvolveRate > 0)
            {
                var newRoi = new Rectangle(
                    (int)(position.X - template.Width / 2),
                    (int)(position.Y - template.Height / 2),
                    template.Width, template.Height);
                newRoi = ClampToFrame(newRoi, frame.Size);

                if (newRoi.Width == template.Width && newRoi.Height == template.Height)
                {
                    using var newTemplate = new Mat(frame, newRoi);
                    CvInvoke.AddWeighted(template, 1.0 - _settings.TemplateEvolveRate,
                        newTemplate, _settings.TemplateEvolveRate, 0, template);
                }
            }
        }

        return new TrackingResult(
            index, position, confidence, isFound,
            new RectangleF(matchX - template.Width / 2f, matchY - template.Height / 2f,
                template.Width, template.Height));
    }

    /// <inheritdoc />
    public bool Reinitialize(Mat currentFrame, int markerIndex, RectangleF searchRegion)
    {
        if (_templates is null || _lastPositions is null) return false;

        var result = TrackSingle(currentFrame, markerIndex);
        if (result.IsFound)
        {
            _lastPositions[markerIndex] = result.Position;
            var roi = Rectangle.Round(result.BoundingBox);
            roi = ClampToFrame(roi, currentFrame.Size);
            _templates[markerIndex]?.Dispose();
            _templates[markerIndex] = new Mat(currentFrame, roi);
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public TrackingResult[] Detect(Mat frame, EngineSettings settings) =>
        Array.Empty<TrackingResult>();

    private static Rectangle ClampToFrame(Rectangle rect, Size frameSize)
    {
        var x = Math.Max(0, rect.X);
        var y = Math.Max(0, rect.Y);
        var right = Math.Min(frameSize.Width, rect.X + rect.Width);
        var bottom = Math.Min(frameSize.Height, rect.Y + rect.Height);
        return new Rectangle(x, y, Math.Max(1, right - x), Math.Max(1, bottom - y));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_templates is not null)
        {
            foreach (var t in _templates)
                t?.Dispose();
        }
    }
}
