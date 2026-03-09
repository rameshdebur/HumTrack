namespace HumTrack.Core.Tracking;

/// <summary>
/// Defines the type of physical marker being tracked.
/// Determines which detection strategy is optimal.
/// </summary>
public enum MarkerType
{
    /// <summary>No physical marker — tracking a natural visual feature via template/ROI.</summary>
    NaturalFeature,

    /// <summary>Retroreflective tape marker — detected by brightness thresholding.</summary>
    Retroreflective,

    /// <summary>Colored sticker marker — detected by HSV color thresholding.</summary>
    ColoredSticker,

    /// <summary>Printed ArUco/AprilTag fiducial marker — detected by pattern recognition.</summary>
    ArUco,
}
