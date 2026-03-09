namespace HumTrack.Core.Tracking;

/// <summary>
/// Defines an HSV color range for color-based marker detection.
/// Used by <see cref="Engines.BlobDetectionEngine"/> with colored sticker markers.
/// </summary>
/// <param name="HueLow">Lower bound of Hue channel (0–180 in OpenCV convention).</param>
/// <param name="HueHigh">Upper bound of Hue channel.</param>
/// <param name="SaturationLow">Lower bound of Saturation (0–255).</param>
/// <param name="SaturationHigh">Upper bound of Saturation.</param>
/// <param name="ValueLow">Lower bound of Value/brightness (0–255).</param>
/// <param name="ValueHigh">Upper bound of Value/brightness.</param>
public readonly record struct HsvRange(
    int HueLow,
    int HueHigh,
    int SaturationLow = 80,
    int SaturationHigh = 255,
    int ValueLow = 80,
    int ValueHigh = 255
)
{
    /// <summary>Predefined range for red markers (wraps around H=0/180).</summary>
    public static readonly HsvRange Red = new(0, 10, 100, 255, 100, 255);

    /// <summary>Predefined range for green markers.</summary>
    public static readonly HsvRange Green = new(35, 85, 80, 255, 80, 255);

    /// <summary>Predefined range for blue markers.</summary>
    public static readonly HsvRange Blue = new(100, 130, 80, 255, 80, 255);

    /// <summary>Predefined range for yellow markers.</summary>
    public static readonly HsvRange Yellow = new(20, 35, 100, 255, 100, 255);
}
