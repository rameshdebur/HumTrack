using System.Globalization;
using System.Text.Json;

namespace HumCapture.Coordinator.Repository;

internal sealed record DecodedFrame(int PresentationIndex, long PresentationTicks, int Width, int Height);
internal sealed record DecodedVideo(int StreamIndex, string Codec, int Width, int Height,
    long TimeBaseNumerator, long TimeBaseDenominator, IReadOnlyList<DecodedFrame> Frames);
internal sealed record DecodedMediaEvidence(string MediaSha256, long ByteLength, DecodedVideo Video);
internal sealed record MediaInspectionResult(DecoderExit Outcome, DecodedMediaEvidence? Evidence, string Reason);

internal static class DecodedMediaParser
{
    internal const int MaximumFrames = 100000;

    internal static DecodedVideo Parse(string json, CancellationToken cancellationToken = default)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
        RejectDuplicateProperties(document.RootElement, cancellationToken);
        var streams = document.RootElement.GetProperty("streams");
        if (streams.GetArrayLength() != 1) { throw new InvalidDataException("Exactly one video stream required."); }
        var stream = streams[0];
        var index = stream.GetProperty("index").GetInt32();
        var codec = stream.GetProperty("codec_name").GetString();
        if (index < 0 || string.IsNullOrWhiteSpace(codec) || codec.Length > 100
            || stream.GetProperty("codec_type").GetString() != "video"
            || stream.GetProperty("disposition").GetProperty("attached_pic").GetInt32() != 0)
        { throw new InvalidDataException("Unsupported video stream identity."); }
        var width = PositiveDimension(stream, "width");
        var height = PositiveDimension(stream, "height");
        var timeBase = stream.GetProperty("time_base").GetString()?.Split('/') ?? [];
        if (timeBase.Length != 2 || !long.TryParse(timeBase[0], NumberStyles.None, CultureInfo.InvariantCulture, out var numerator)
            || !long.TryParse(timeBase[1], NumberStyles.None, CultureInfo.InvariantCulture, out var denominator)
            || numerator <= 0 || denominator <= 0)
        { throw new InvalidDataException("Invalid exact presentation time base."); }
        var frames = document.RootElement.GetProperty("frames");
        if (frames.GetArrayLength() < 1 || frames.GetArrayLength() > MaximumFrames)
        { throw new InvalidDataException("Decoded frame count outside supported inspection envelope."); }
        var observed = new List<DecodedFrame>(frames.GetArrayLength());
        foreach (var frame in frames.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (frame.GetProperty("stream_index").GetInt32() != index)
            { throw new InvalidDataException("Frame belongs to another stream."); }
            // Do not use best_effort_timestamp or sort/normalize PTS.
            observed.Add(new(observed.Count, frame.GetProperty("pts").GetInt64(),
                PositiveDimension(frame, "width"), PositiveDimension(frame, "height")));
        }
        return new(index, codec, width, height, numerator, denominator, observed.AsReadOnly());
    }

    private static int PositiveDimension(JsonElement element, string name)
    {
        var value = element.GetProperty(name).GetInt32();
        return value > 0 ? value : throw new InvalidDataException("Missing positive image dimension.");
    }

    private static void RejectDuplicateProperties(JsonElement element, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (element.ValueKind == JsonValueKind.Object)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!keys.Add(property.Name)) { throw new InvalidDataException("Duplicate probe property."); }
                RejectDuplicateProperties(property.Value, token);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray()) { RejectDuplicateProperties(item, token); }
        }
    }
}
