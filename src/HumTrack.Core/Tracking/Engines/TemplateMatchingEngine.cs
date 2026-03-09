using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace HumTrack.Core.Tracking.Engines;

/// <summary>
/// Normalized cross-correlation (NCC) template matching engine.
/// Slides a template image across a search window to find the best match.
///
/// <para><b>Compatible with</b>: Legacy Tracker behavior.</para>
/// <para><b>Best for</b>: Natural features, any marker when no better engine is available.</para>
/// <para><b>Deterministic</b>: Yes — NCC is a pure arithmetic operation.</para>
/// </summary>
public sealed class TemplateMatchingEngine : ITrackingEngine
{
    private Mat? _previousFrame;
    private Mat[]? _templates;
    private PointF[]? _lastPositions;
    private EngineSettings _settings = new();

    /// <inheritdoc />
    public string Name => "Template Matching (NCC)";

    /// <inheritdoc />
    public string Description =>
        "Classic normalized cross-correlation. Searches for each marker's " +
        "template within a local window. Compatible with legacy Tracker.";

    /// <inheritdoc />
    public TrackingCapabilities Capabilities =>
        TrackingCapabilities.SinglePoint | TrackingCapabilities.SubPixelAccuracy;

    /// <inheritdoc />
    public void Initialize(Mat referenceFrame, IReadOnlyList<RectangleF> markerRegions, EngineSettings settings)
    {
        _settings = settings;

        // Extract initial templates from the reference frame
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

        _previousFrame = referenceFrame.Clone();
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

        _previousFrame?.Dispose();
        _previousFrame = currentFrame.Clone();

        return results;
    }

    /// <inheritdoc />
    public bool Reinitialize(Mat currentFrame, int markerIndex, RectangleF searchRegion)
    {
        if (_templates is null || _lastPositions is null)
            return false;

        var result = TrackSingle(currentFrame, markerIndex, Rectangle.Round(searchRegion));

        if (result.IsFound)
        {
            _lastPositions[markerIndex] = result.Position;
            // Update template with new appearance
            var roi = Rectangle.Round(result.BoundingBox);
            roi = ClampToFrame(roi, currentFrame.Size);
            _templates[markerIndex]?.Dispose();
            _templates[markerIndex] = new Mat(currentFrame, roi);
            return true;
        }

        return false;
    }

    /// <inheritdoc />
    public TrackingResult[] Detect(Mat frame, EngineSettings settings)
    {
        // Template matching does not support template-free detection
        return Array.Empty<TrackingResult>();
    }

    private TrackingResult TrackSingle(Mat frame, int index, Rectangle? overrideSearchRegion = null)
    {
        var template = _templates![index];
        var lastPos = _lastPositions![index];

        // Define the search region centered on last known position
        var searchSize = overrideSearchRegion?.Size ?? _settings.SearchWindow;
        var searchRect = overrideSearchRegion ?? new Rectangle(
            (int)(lastPos.X - searchSize.Width / 2),
            (int)(lastPos.Y - searchSize.Height / 2),
            searchSize.Width + template.Width,
            searchSize.Height + template.Height);

        searchRect = ClampToFrame(searchRect, frame.Size);

        // Ensure search region is at least as large as the template
        if (searchRect.Width < template.Width || searchRect.Height < template.Height)
        {
            return new TrackingResult(index, lastPos, 0f, false,
                new RectangleF(lastPos.X - template.Width / 2f, lastPos.Y - template.Height / 2f,
                    template.Width, template.Height));
        }

        using var searchArea = new Mat(frame, searchRect);
        using var matchResult = new Mat();

        CvInvoke.MatchTemplate(searchArea, template, matchResult, TemplateMatchingType.CcorrNormed);

        double minVal = 0, maxVal = 0;
        Point minLoc = default, maxLoc = default;
        CvInvoke.MinMaxLoc(matchResult, ref minVal, ref maxVal, ref minLoc, ref maxLoc);

        var confidence = (float)maxVal;
        var isFound = confidence >= _settings.ConfidenceThreshold;

        // Convert match location back to full-frame coordinates
        var matchX = searchRect.X + maxLoc.X + template.Width / 2f;
        var matchY = searchRect.Y + maxLoc.Y + template.Height / 2f;
        var position = new PointF(matchX, matchY);

        if (isFound)
        {
            _lastPositions![index] = position;

            // Optionally evolve the template
            if (_settings.TemplateEvolveRate > 0)
            {
                EvolveTemplate(frame, index, position, template.Size);
            }
        }

        return new TrackingResult(
            index, position, confidence, isFound,
            new RectangleF(matchX - template.Width / 2f, matchY - template.Height / 2f,
                template.Width, template.Height));
    }

    private void EvolveTemplate(Mat frame, int index, PointF center, Size templateSize)
    {
        var roi = new Rectangle(
            (int)(center.X - templateSize.Width / 2),
            (int)(center.Y - templateSize.Height / 2),
            templateSize.Width, templateSize.Height);
        roi = ClampToFrame(roi, frame.Size);

        if (roi.Width != templateSize.Width || roi.Height != templateSize.Height)
            return;

        using var newTemplate = new Mat(frame, roi);
        var rate = _settings.TemplateEvolveRate;

        // Weighted blend: template = (1 - rate) * old + rate * new
        CvInvoke.AddWeighted(_templates![index], 1.0 - rate, newTemplate, rate, 0, _templates[index]);
    }

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
        _previousFrame?.Dispose();
        if (_templates is not null)
        {
            foreach (var t in _templates)
                t?.Dispose();
        }
    }
}
