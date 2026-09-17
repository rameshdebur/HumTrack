using System.Globalization;
using System.Text.Json;
using HumCapture.Coordinator.Repository;

namespace HumCapture.Coordinator.Repository.SelfTest;

internal static class ClockQuantizationTests
{
    internal static void SharedVectors()
    {
        using var vectors = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TimingVectors", "clock-quantization-vectors.json")));
        foreach (var v in vectors.RootElement.GetProperty("cases").EnumerateArray())
        {
            string? result = null;
            try
            {
                result = ClockQuantization.Map(ClockQuantization.ParseScale(v.GetProperty("scale").GetString()!),
                    ulong.Parse(v.GetProperty("source").GetString()!, CultureInfo.InvariantCulture),
                    long.Parse(v.GetProperty("offset").GetString()!, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture);
            }
            catch (InvalidDataException) { /* Expected rejection is compared below, never silently accepted. */ }
            if (result != v.GetProperty("expected").GetString()) { throw new InvalidOperationException(v.GetProperty("name").GetString()); }
        }
    }
    internal static void HalfwayProperties()
    {
        var half = ClockQuantization.ParseScale("5e-1");
        for (ulong k = 0; k < 1000; k++)
        {
            var expected = k + (k % 2);
            if (ClockQuantization.Map(half, 2 * k + 1, 0) != expected)
            { throw new InvalidOperationException("Halfway parity changed."); }
        }
        if (!ClockQuantization.ParseScale("100e-2").IsOne || ClockQuantization.ParseScale("1.0000000000000000001").IsOne)
        { throw new InvalidOperationException("Exact unit scale lost precision."); }
    }
}
