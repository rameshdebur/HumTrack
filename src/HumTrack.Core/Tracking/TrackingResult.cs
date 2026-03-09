using System.Drawing;

namespace HumTrack.Core.Tracking;

/// <summary>
/// Result of tracking a single marker in one frame.
/// Immutable record — all physics and data pipelines consume this directly.
/// </summary>
/// <param name="MarkerIndex">Zero-based index of the marker in the track's marker list.</param>
/// <param name="Position">Sub-pixel center position in image coordinates (pixels).</param>
/// <param name="Confidence">Tracking confidence, 0.0 (lost) to 1.0 (perfect match).</param>
/// <param name="IsFound">False if the marker was not detected in this frame.</param>
/// <param name="BoundingBox">Updated bounding rectangle around the marker.</param>
/// <param name="MarkerId">
/// Optional unique marker identifier (e.g., ArUco ID).
/// Null for engines that do not provide self-identification.
/// </param>
public readonly record struct TrackingResult(
    int MarkerIndex,
    PointF Position,
    float Confidence,
    bool IsFound,
    RectangleF BoundingBox,
    int? MarkerId = null
);
