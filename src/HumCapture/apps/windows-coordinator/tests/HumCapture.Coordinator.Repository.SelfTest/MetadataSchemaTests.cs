using System.Text;
using System.Text.Json;
using HumCapture.Coordinator.Repository;

namespace HumCapture.Coordinator.Repository.SelfTest;

internal static class MetadataSchemaTests
{
    private static readonly MetadataSchemaValidator Validator = new();
    private static byte[] Camera => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TimingVectors", "valid-camera-metadata.json"));
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Invalid metadata accepted.");
    }
    internal static void SharedVectors()
    {
        using var vectors = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "TimingVectors", "metadata-schema-vectors.json")));
        foreach (var vector in vectors.RootElement.GetProperty("cases").EnumerateArray())
        {
            var kind = Enum.Parse<MetadataKind>(vector.GetProperty("kind").GetString()!);
            var data = Encoding.UTF8.GetBytes(vector.GetProperty("data").GetRawText());
            var accepted = true;
            try { Validator.Validate(kind, data); } catch (InvalidDataException) { accepted = false; }
            if (accepted != vector.GetProperty("expected").GetBoolean()) { throw new InvalidOperationException(vector.GetProperty("name").GetString()); }
        }
    }
    internal static void InputGuards()
    {
        Reject(() => Validator.Validate((MetadataKind)99, Camera));
        foreach (var bad in new[] { "", "{", "null", "{\"a\":1,\"a\":2}", "{\"a\":{\"b\":1,\"b\":2}}", new string('[', 65) + "0" + new string(']', 65) })
        { Reject(() => Validator.Validate(MetadataKind.Camera, Encoding.UTF8.GetBytes(bad))); }
        Reject(() => Validator.Validate(MetadataKind.Camera, new byte[MetadataSchemaValidator.MaximumBytes + 1]));
        var invalidUtf8 = Camera;
        invalidUtf8[Encoding.UTF8.GetString(invalidUtf8).IndexOf("Synthetic", StringComparison.Ordinal)] = 255;
        Reject(() => Validator.Validate(MetadataKind.Camera, invalidUtf8));
        var duplicate = Encoding.UTF8.GetString(Camera).Replace("{", "{\"schema_version\":\"9.0.0\",", StringComparison.Ordinal);
        Reject(() => Validator.Validate(MetadataKind.Camera, Encoding.UTF8.GetBytes(duplicate)));
        try { Validator.Validate(MetadataKind.Camera, Camera, new CancellationToken(true)); }
        catch (OperationCanceledException) { return; }
        throw new InvalidOperationException("Cancellation ignored.");
    }
    internal static void RepeatAndParallel()
    {
        var camera = Camera;
        Parallel.For(0, 20, _ => Validator.Validate(MetadataKind.Camera, camera));
        var value = Validator.Validate(MetadataKind.Camera, camera);
        if (value.GetProperty("schema_version").GetString() != "1.0.0") { throw new InvalidOperationException("Returned metadata lost its lifetime."); }
    }
}
