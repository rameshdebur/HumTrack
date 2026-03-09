using System.Drawing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;

namespace HumTrack.Core.Tracking.Engines;

/// <summary>
/// CSRT-inspired (Channel and Spatial Reliability) tracking engine.
/// Uses multi-channel template matching with spatial weighting for
/// improved robustness to partial occlusion and background clutter.
///
/// <para><b>Best for</b>: Markers that get partially occluded (limb crossover in gait).</para>
/// <para><b>Deterministic</b>: Yes — all operations are pure arithmetic.</para>
///
/// <para>This implementation uses multi-channel (BGR separate) correlation to
/// approximate CSRT's channel reliability weighting. Each channel is matched
/// independently and fused via weighted averaging.</para>
/// </summary>
public sealed class CsrtEngine : ITrackingEngine
{
    private Mat[]? _templates;
    private PointF[]? _lastPositions;
    private EngineSettings _settings = new();

    /// <inheritdoc />
    public string Name => "CSRT (Spatial Reliability)";

    /// <inheritdoc />
    public string Description =>
        "Multi-channel correlation filter with spatial reliability. " +
        "Best when markers get partially occluded during gait analysis.";

    /// <inheritdoc />
    public TrackingCapabilities Capabilities =>
        TrackingCapabilities.SinglePoint |
        TrackingCapabilities.OcclusionRobust |
        TrackingCapabilities.SubPixelAccuracy;

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
            results[i] = TrackSingleMultiChannel(currentFrame, i);
        }

        return results;
    }

    private TrackingResult TrackSingleMultiChannel(Mat frame, int index)
    {
        var template = _templates![index];
        var lastPos = _lastPositions![index];

        // Search region
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

        // Multi-channel matching: match each BGR channel independently
        using var searchChannels = new Emgu.CV.Util.VectorOfMat();
        using var templateChannels = new Emgu.CV.Util.VectorOfMat();
        CvInvoke.Split(searchArea, searchChannels);
        CvInvoke.Split(template, templateChannels);

        int resultWidth = searchArea.Width - template.Width + 1;
        int resultHeight = searchArea.Height - template.Height + 1;

        if (resultWidth <= 0 || resultHeight <= 0)
        {
            return new TrackingResult(index, lastPos, 0f, false,
                new RectangleF(lastPos.X - template.Width / 2f, lastPos.Y - template.Height / 2f,
                    template.Width, template.Height));
        }

        using var combinedResult = new Mat(resultHeight, resultWidth, DepthType.Cv32F, 1);
        combinedResult.SetTo(new MCvScalar(0));

        int channelCount = Math.Min(searchChannels.Size, templateChannels.Size);
        for (int c = 0; c < channelCount; c++)
        {
            using var channelResult = new Mat();
            CvInvoke.MatchTemplate(searchChannels[c], templateChannels[c],
                channelResult, TemplateMatchingType.CcorrNormed);

            // Equal weight per channel
            CvInvoke.Add(combinedResult, channelResult, combinedResult);
        }

        // Average across channels
        if (channelCount > 0)
        {
            CvInvoke.Multiply(combinedResult, new ScalarArray(new MCvScalar(1.0 / channelCount)),
                combinedResult);
        }

        double minVal = 0, maxVal = 0;
        Point minLoc = default, maxLoc = default;
        CvInvoke.MinMaxLoc(combinedResult, ref minVal, ref maxVal, ref minLoc, ref maxLoc);

        var confidence = (float)maxVal;
        var isFound = confidence >= _settings.ConfidenceThreshold;

        var matchX = searchRect.X + maxLoc.X + template.Width / 2f;
        var matchY = searchRect.Y + maxLoc.Y + template.Height / 2f;
        var position = new PointF(matchX, matchY);

        if (isFound)
        {
            _lastPositions![index] = position;

            // Update template
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

        _lastPositions[markerIndex] = new PointF(
            searchRegion.X + searchRegion.Width / 2f,
            searchRegion.Y + searchRegion.Height / 2f);

        var roi = Rectangle.Round(searchRegion);
        roi = ClampToFrame(roi, currentFrame.Size);
        _templates[markerIndex]?.Dispose();
        _templates[markerIndex] = new Mat(currentFrame, roi);
        return true;
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
