using Emgu.CV;
using HumTrack.Core.Diagnostics;

namespace HumTrack.Core.VideoProcessing;

/// <summary>
/// A modular processing pipeline that executes multiple frame processors sequentially.
/// Used to chain together lens undistortion, motion blur Sharpening, contrast adjustment, etc.
/// </summary>
public sealed class VideoPipeline : IDisposable
{
    private readonly List<IFrameProcessor> _processors = new();
    private readonly Serilog.ILogger _log = HumTrackLogger.ForContext<VideoPipeline>();

    /// <summary>Gets the ordered list of currently active processors.</summary>
    public IReadOnlyList<IFrameProcessor> Processors => _processors;

    /// <summary>Adds a processor to the end of the pipeline.</summary>
    public void AddProcessor(IFrameProcessor processor)
    {
        _processors.Add(processor);
        _log.Information("Added {ProcessorName} to the video pipeline.", processor.Name);
    }

    /// <summary>Removes a specific processor from the pipeline.</summary>
    public void RemoveProcessor(IFrameProcessor processor)
    {
        if (_processors.Remove(processor))
        {
            _log.Information("Removed {ProcessorName} from the video pipeline.", processor.Name);
        }
    }

    /// <summary>Clears and disposes all processors.</summary>
    public void Clear()
    {
        foreach (var processor in _processors)
        {
            processor.Dispose();
        }
        _processors.Clear();
        _log.Information("Cleared the video pipeline.");
    }

    /// <summary>
    /// Passes the frame through all enabled processors sequentially.
    /// Frame is modified in-place to minimize allocations.
    /// </summary>
    /// <param name="frame">The unmanaged EmguCV Mat frame.</param>
    public void Process(Mat frame)
    {
        if (frame.IsEmpty) return;

        foreach (var processor in _processors)
        {
            if (processor.IsEnabled)
            {
                processor.Process(frame);
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Clear();
    }
}
