using System.Globalization;
using System.Text.Json;

namespace HumCapture.Coordinator.Repository;

// Shared structural clock bindings; not a physical synchronization guarantee.
internal sealed class TimingClockBindings
{
    internal JsonElement Timing { get; }
    internal Dictionary<Guid, JsonElement> Clocks { get; }
    internal Dictionary<Guid, JsonElement> Streams { get; }
    private readonly Dictionary<uint, JsonElement> models;
    private readonly Dictionary<uint, (ExactClockScale Scale, long Offset)> exactModels;
    private readonly TimingMetadataVersion version;

    internal TimingClockBindings(JsonElement timing, TimingMetadataVersion version, CancellationToken token)
    {
        Timing = timing;
        this.version = version;
        Clocks = Unique(timing.GetProperty("clocks"), "clock_id");
        Streams = Unique(timing.GetProperty("streams"), "stream_id");
        Require(Clocks.ContainsKey(Id(timing, "session_clock_id")), "Unknown session clock.");
        foreach (var declared in Streams.Values)
        {
            foreach (var key in new[] { "native_clock_id", "host_arrival_clock_id", "presentation_clock_id" })
            {
                if (declared.TryGetProperty(key, out var clock))
                { Require(Clocks.ContainsKey(clock.GetGuid()), "Stream references unknown clock."); }
            }
        }
        models = new Dictionary<uint, JsonElement>();
        exactModels = new Dictionary<uint, (ExactClockScale Scale, long Offset)>();
        foreach (var model in timing.GetProperty("clock_models").EnumerateArray())
        {
            token.ThrowIfCancellationRequested();
            Require(models.TryAdd(model.GetProperty("model_id").GetUInt32(), model), "Duplicate clock model.");
            Require(Clocks.ContainsKey(Id(model, "source_clock_id")) && Clocks.ContainsKey(Id(model, "target_clock_id")), "Unknown model clock.");
            Require(U64(model, "valid_from_ticks") <= U64(model, "valid_through_ticks"), "Reversed model interval.");
            Require(long.TryParse(model.GetProperty("offset_ticks").GetString(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var offset), "Model offset overflow.");
            Require(double.IsFinite(model.GetProperty("scale").GetDouble()), "Nonfinite model scale.");
            var unitScale = model.GetProperty("scale").GetDouble().Equals(1d);
            if (version == TimingMetadataVersion.ExactV1_1)
            {
                var scale = ClockQuantization.ParseScale(model.GetProperty("scale").GetRawText());
                exactModels.Add(model.GetProperty("model_id").GetUInt32(), (scale, offset));
                unitScale = scale.IsOne;
                if (model.GetProperty("kind").GetString() == "OFFSET") { Require(unitScale, "OFFSET requires exact unit scale."); }
            }
            if (model.GetProperty("kind").GetString() == "IDENTITY")
            {
                var source = Clocks[Id(model, "source_clock_id")]; var target = Clocks[Id(model, "target_clock_id")];
                Require(Id(source, "epoch_id") == Id(target, "epoch_id")
                    && source.GetProperty("ticks_per_second").GetUInt64() == target.GetProperty("ticks_per_second").GetUInt64()
                    && unitScale && offset == 0, "Unproven identity model.");
            }
        }
    }

    internal JsonElement Stream(Guid id, string kind, string path)
    {
        Require(Streams.TryGetValue(id, out var stream)
            && stream.GetProperty("kind").GetString() == kind
            && stream.GetProperty("relative_path").GetString() == path, "Timing stream binding mismatch.");
        return stream;
    }

    internal bool Compare(JsonElement stream, ulong native, ulong? host, ulong? mapped, uint modelId, uint uncertainty)
    {
        if (host.HasValue)
        { Require(stream.TryGetProperty("host_arrival_clock_id", out _), "Host arrival lacks clock binding."); }
        if (!mapped.HasValue) { return false; }
        Require(models.TryGetValue(modelId, out var model), "Mapped sample references unknown model.");
        Require(Id(model, "source_clock_id") == Id(stream, "native_clock_id")
            && Id(model, "target_clock_id") == Id(Timing, "session_clock_id")
            && model.GetProperty("quality").GetString() != "UNAVAILABLE"
            && native >= U64(model, "valid_from_ticks") && native <= U64(model, "valid_through_ticks")
            && uncertainty >= model.GetProperty("uncertainty_ns").GetUInt32(), "Sample model binding/range/uncertainty mismatch.");
        if (version != TimingMetadataVersion.ExactV1_1) { return false; }
        var exact = exactModels[modelId];
        Require(ClockQuantization.Map(exact.Scale, native, exact.Offset) == mapped.Value,
            "Recorded mapped time differs from exact quantization.");
        return true;
    }

    internal static MetadataKind SchemaKind(TimingMetadataVersion version) => version switch
    {
        TimingMetadataVersion.LegacyV1 => MetadataKind.Timing,
        TimingMetadataVersion.ExactV1_1 => MetadataKind.TimingExact,
        _ => throw new InvalidDataException("Unsupported timing metadata version.")
    };

    private static Dictionary<Guid, JsonElement> Unique(JsonElement array, string key)
    {
        var result = new Dictionary<Guid, JsonElement>();
        foreach (var item in array.EnumerateArray()) { Require(result.TryAdd(Id(item, key), item), "Duplicate metadata identity."); }
        return result;
    }
    private static Guid Id(JsonElement item, string key) => item.GetProperty(key).GetGuid();
    private static ulong U64(JsonElement item, string key)
    {
        Require(ulong.TryParse(item.GetProperty(key).GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var value), "Unsigned metadata integer overflow.");
        return value;
    }
    private static void Require(bool value, string reason)
    { if (!value) { throw new InvalidDataException(reason); } }
}
