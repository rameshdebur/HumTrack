namespace HumTrack.App.ViewModels;

/// <summary>
/// A UI-bindable representation of a tracked marker bounding box.
/// All coordinates are in the display-scaled pixel space.
/// </summary>
public sealed class TrackedRect
{
    /// <summary>Left edge of bounding box (display pixels).</summary>
    public double X { get; init; }

    /// <summary>Top edge of bounding box (display pixels).</summary>
    public double Y { get; init; }

    /// <summary>Width of bounding box (display pixels).</summary>
    public double W { get; init; }

    /// <summary>Height of bounding box (display pixels).</summary>
    public double H { get; init; }

    /// <summary>Centre X, for the crosshair dot.</summary>
    public double CX => X + W / 2 - 3;

    /// <summary>Centre Y, for the crosshair dot.</summary>
    public double CY => Y + H / 2 - 3;

    /// <summary>Y position for the text label, slightly above the box.</summary>
    public double LabelY => Y - 14 < 0 ? Y + 2 : Y - 14;

    /// <summary>Short label shown next to the box.</summary>
    public string Label { get; init; } = string.Empty;
}
