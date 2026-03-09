namespace HumTrack.Core.Tracking;

/// <summary>
/// Defines the capabilities of a tracking engine.
/// Used to determine UI options and pipeline compatibility.
/// </summary>
[Flags]
public enum TrackingCapabilities
{
    /// <summary>No specific capabilities.</summary>
    None = 0,

    /// <summary>Can track a single point per instance.</summary>
    SinglePoint = 1 << 0,

    /// <summary>Can track multiple points simultaneously in a single call.</summary>
    MultiPoint = 1 << 1,

    /// <summary>Handles changes in marker apparent size.</summary>
    ScaleInvariant = 1 << 2,

    /// <summary>Handles marker rotation.</summary>
    RotationInvariant = 1 << 3,

    /// <summary>Robust to partial marker occlusion.</summary>
    OcclusionRobust = 1 << 4,

    /// <summary>Returns positions with sub-pixel precision.</summary>
    SubPixelAccuracy = 1 << 5,

    /// <summary>Uses color information for detection.</summary>
    ColorBased = 1 << 6,

    /// <summary>Each marker is uniquely identified by its pattern (e.g., ArUco).</summary>
    SelfIdentifying = 1 << 7,

    /// <summary>No template or ROI definition needed — detects markers by physical properties.</summary>
    TemplateFreee = 1 << 8,
}
