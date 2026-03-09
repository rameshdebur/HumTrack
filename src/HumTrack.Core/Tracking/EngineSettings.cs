using System.Drawing;

namespace HumTrack.Core.Tracking;

/// <summary>
/// Configuration settings for tracking engines.
/// Each engine reads only the properties it cares about and ignores the rest.
/// </summary>
public sealed class EngineSettings
{
    // ──────────────── General ────────────────

    /// <summary>
    /// The type of physical marker being tracked.
    /// Determines optimal detection strategy.
    /// </summary>
    public MarkerType MarkerType { get; init; } = MarkerType.Retroreflective;

    /// <summary>
    /// Confidence threshold below which a marker is considered "lost".
    /// Range: 0.0 to 1.0. Default: 0.6.
    /// </summary>
    public double ConfidenceThreshold { get; init; } = 0.6;

    // ──────────────── Template Matching ────────────────

    /// <summary>
    /// Size of the search window around the predicted position (pixels).
    /// Used by TemplateMatchingEngine and as fallback search area.
    /// </summary>
    public Size SearchWindow { get; init; } = new(50, 50);

    /// <summary>
    /// Rate at which the template evolves to match appearance changes.
    /// 0.0 = never update template, 1.0 = fully replace each frame.
    /// </summary>
    public double TemplateEvolveRate { get; init; } = 0.2;

    // ──────────────── Optical Flow (Lucas-Kanade) ────────────────

    /// <summary>
    /// Number of pyramid levels for pyramidal Lucas-Kanade.
    /// More levels handle larger frame-to-frame motion. Default: 3.
    /// </summary>
    public int PyramidLevels { get; init; } = 3;

    /// <summary>
    /// Size of the search window at each pyramid level.
    /// Default: 21x21 pixels.
    /// </summary>
    public Size LkWindowSize { get; init; } = new(21, 21);

    // ──────────────── Blob Detection ────────────────

    /// <summary>
    /// Brightness threshold for retroreflective marker detection.
    /// Pixels above this value are candidate markers. Range: 0–255.
    /// </summary>
    public int BrightnessThreshold { get; init; } = 200;

    /// <summary>
    /// HSV color range for colored sticker detection.
    /// Null when using brightness-only (retroreflective) mode.
    /// </summary>
    public HsvRange? ColorRange { get; init; }

    /// <summary>
    /// Minimum marker area in pixels². Blobs smaller than this are rejected.
    /// </summary>
    public double MinMarkerArea { get; init; } = 20.0;

    /// <summary>
    /// Maximum marker area in pixels². Blobs larger than this are rejected.
    /// </summary>
    public double MaxMarkerArea { get; init; } = 5000.0;

    /// <summary>
    /// Minimum circularity (0.0–1.0) for blob shape filtering.
    /// 1.0 = perfect circle. Default: 0.5.
    /// </summary>
    public double MinCircularity { get; init; } = 0.5;

    // ──────────────── ArUco Detection ────────────────

    /// <summary>
    /// ArUco dictionary type (e.g., 4x4_50, 5x5_100, 6x6_250).
    /// Determines which pattern set the detector recognizes.
    /// </summary>
    public ArucoDictionaryType ArucoDictionary { get; init; } = ArucoDictionaryType.Dict4X4With50;

    // ──────────────── Hybrid Engine ────────────────

    /// <summary>
    /// Number of frames between full re-detection passes in the Hybrid engine.
    /// Lower values reduce drift but cost more CPU. Default: 30.
    /// </summary>
    public int RedetectIntervalFrames { get; init; } = 30;
}

/// <summary>
/// Supported ArUco dictionary types matching OpenCV's predefined dictionaries.
/// </summary>
public enum ArucoDictionaryType
{
    /// <summary>4×4 bit markers, 50 unique IDs. Smallest, fastest detection.</summary>
    Dict4X4With50,

    /// <summary>4×4 bit markers, 100 unique IDs.</summary>
    Dict4X4With100,

    /// <summary>5×5 bit markers, 100 unique IDs. Good balance.</summary>
    Dict5X5With100,

    /// <summary>5×5 bit markers, 250 unique IDs.</summary>
    Dict5X5With250,

    /// <summary>6×6 bit markers, 250 unique IDs. Most robust, largest markers.</summary>
    Dict6X6With250,
}
