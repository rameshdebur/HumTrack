using System.Text.Json;
using System.Text;
using Json.Schema;

namespace HumCapture.Coordinator.Repository;

internal enum MetadataKind { Timing, Camera, Imu, CaptureEvents, Finalization, TimingExact, PackageManifest }

// Structural gate only. Does not establish canonical bytes, identities or scientific validity.
internal sealed class MetadataSchemaValidator
{
    internal const int MaximumBytes = 16 * 1024 * 1024;
    private readonly Dictionary<MetadataKind, JsonSchema> schemas = new();
    private readonly object evaluationGate = new();

    internal MetadataSchemaValidator()
    {
        var options = new BuildOptions
        {
            Dialect = Dialect.Draft202012,
            SchemaRegistry = new SchemaRegistry { Fetch = (_, _) => throw new InvalidDataException("Unregistered metadata schema reference.") }
        };
        var assembly = typeof(MetadataSchemaValidator).Assembly;
        JsonSchema Load(string name)
        {
            using var stream = assembly.GetManifestResourceStream("HumCapture.Metadata." + name + ".schema.json")
                ?? throw new InvalidDataException("Missing embedded metadata schema.");
            using var document = JsonDocument.Parse(stream);
            // The compiled schema retains JsonElements beyond this document's lifetime.
            return JsonSchema.Build(document.RootElement.Clone(), options);
        }
        Load("common");
        schemas.Add(MetadataKind.Timing, Load("timing-metadata"));
        schemas.Add(MetadataKind.TimingExact, Load("timing-metadata-v1.1"));
        schemas.Add(MetadataKind.Camera, Load("camera-metadata"));
        schemas.Add(MetadataKind.Imu, Load("imu-metadata"));
        schemas.Add(MetadataKind.CaptureEvents, Load("capture-event-archive"));
        schemas.Add(MetadataKind.Finalization, Load("finalization-summary"));
        schemas.Add(MetadataKind.PackageManifest, Load("package-manifest"));
    }

    internal JsonElement Validate(MetadataKind kind, ReadOnlyMemory<byte> utf8, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!schemas.TryGetValue(kind, out var schema)) { throw new InvalidDataException("Unsupported metadata kind."); }
        if (utf8.Length == 0 || utf8.Length > MaximumBytes) { throw new InvalidDataException("Metadata exceeds bounded input envelope."); }
        try
        {
            _ = new UTF8Encoding(false, true).GetCharCount(utf8.Span);
            using var document = JsonDocument.Parse(utf8, new JsonDocumentOptions { MaxDepth = 64 });
            CheckKeys(document.RootElement, cancellationToken);
            // Serialize calls because third-party evaluation can lazily resolve references.
            lock (evaluationGate)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = schema.Evaluate(document.RootElement, new EvaluationOptions
                { RequireFormatValidation = true, OutputFormat = OutputFormat.Flag });
                if (!result.IsValid) { throw new InvalidDataException("Metadata does not match its versioned schema."); }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return document.RootElement.Clone();
        }
        catch (JsonException error) { throw new InvalidDataException("Malformed metadata JSON.", error); }
        catch (DecoderFallbackException error) { throw new InvalidDataException("Malformed metadata UTF-8.", error); }
    }

    private static void CheckKeys(JsonElement value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name)) { throw new InvalidDataException("Duplicate metadata property."); }
                CheckKeys(property.Value, cancellationToken);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray()) { CheckKeys(item, cancellationToken); }
        }
    }
}
