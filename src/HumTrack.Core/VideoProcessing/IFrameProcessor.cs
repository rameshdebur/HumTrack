using Emgu.CV;

namespace HumTrack.Core.VideoProcessing;

/// <summary>
/// Represents a stage in the video processing pipeline that modifies a frame in-place.
/// Allows modular, pluggable filters to be applied dynamically before tracking.
/// </summary>
public interface IFrameProcessor : IDisposable
{
    /// <summary>Display name of the processor for UI selection.</summary>
    string Name { get; }

    /// <summary>Whether this filter should be actively applied to frames.</summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Processes and modifies the given frame in-place.
    /// </summary>
    /// <param name="frame">The BGR frame to be modified.</param>
    void Process(Mat frame);
}
