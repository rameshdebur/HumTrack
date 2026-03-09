using System.Drawing;
using Emgu.CV;

namespace HumTrack.Core.Tracking;

/// <summary>
/// Common interface for all tracking engines in HumTrack.
/// Implements the Strategy pattern: the application selects an engine at runtime
/// and all downstream pipelines (physics, export, UI) consume <see cref="TrackingResult"/>
/// identically regardless of which engine produced it.
///
/// <para><b>Contract</b>: Every implementation must be fully deterministic —
/// identical inputs must always produce byte-identical outputs.</para>
/// </summary>
public interface ITrackingEngine : IDisposable
{
    /// <summary>
    /// Display name shown in the UI engine dropdown (e.g., "Optical Flow (Lucas-Kanade)").
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Short human-readable description for tooltips.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Declares what this engine can and cannot do.
    /// Used by the UI to show/hide relevant settings.
    /// </summary>
    TrackingCapabilities Capabilities { get; }

    /// <summary>
    /// Initialize the engine for tracking on a reference frame.
    /// Called once when the user defines key frames or initial marker positions.
    /// </summary>
    /// <param name="referenceFrame">
    /// The video frame (BGR, 8-bit) on which markers are first defined.
    /// </param>
    /// <param name="markerRegions">
    /// List of initial marker bounding rectangles in image coordinates.
    /// For template-free engines (ArUco, Blob), this may be empty — the engine
    /// will auto-detect markers in the reference frame.
    /// </param>
    /// <param name="settings">
    /// Engine-specific configuration. Each engine reads only its relevant properties.
    /// </param>
    void Initialize(Mat referenceFrame, IReadOnlyList<RectangleF> markerRegions, EngineSettings settings);

    /// <summary>
    /// Track all initialized markers in the given frame.
    /// Returns one <see cref="TrackingResult"/> per marker.
    /// </summary>
    /// <param name="currentFrame">The current video frame (BGR, 8-bit).</param>
    /// <returns>
    /// Array of results, one per marker. Results with <c>IsFound = false</c>
    /// indicate a lost marker. The array length must equal the number of
    /// initialized markers (or the number of auto-detected markers for
    /// template-free engines).
    /// </returns>
    TrackingResult[] Track(Mat currentFrame);

    /// <summary>
    /// Attempt to re-detect a specific marker that was lost.
    /// Called by the pipeline when a marker's confidence drops below threshold.
    /// </summary>
    /// <param name="currentFrame">The current frame.</param>
    /// <param name="markerIndex">Index of the lost marker.</param>
    /// <param name="searchRegion">Region to search within (may be larger than the original ROI).</param>
    /// <returns>True if the marker was successfully re-acquired.</returns>
    bool Reinitialize(Mat currentFrame, int markerIndex, RectangleF searchRegion);

    /// <summary>
    /// Auto-detect all markers in a frame without prior initialization.
    /// Only supported by engines with <see cref="TrackingCapabilities.TemplateFreee"/>.
    /// </summary>
    /// <param name="frame">The frame to scan.</param>
    /// <param name="settings">Detection settings (brightness threshold, color range, ArUco dictionary).</param>
    /// <returns>
    /// Detected markers with positions and optional IDs.
    /// Returns an empty array if no markers are found.
    /// </returns>
    TrackingResult[] Detect(Mat frame, EngineSettings settings);
}
