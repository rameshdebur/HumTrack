using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HumCapture.Coordinator.Repository;

internal static partial class RepositoryDescriptorCodec
{
    private static readonly HashSet<string> AllowedProperties =
    [
        "schema_version",
        "repository_id",
        "interface_version",
        "namespace_version",
        "catalog_schema_version",
        "created_utc",
        "created_by_windows_account",
        "catalog_relative_path",
        "required_features"
    ];

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        WriteIndented = true
    };

    public static byte[] Serialize(RepositoryDescriptor descriptor) =>
        JsonSerializer.SerializeToUtf8Bytes(descriptor, WriteOptions);

    public static RepositoryDescriptor Parse(ReadOnlySpan<byte> json, out IReadOnlySet<string> encounteredProperties)
    {
        try
        {
            using var document = JsonDocument.Parse(json.ToArray());
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw Invalid("Repository descriptor must be a JSON object.");
            }

            var propertyNames = document.RootElement.EnumerateObject().Select(property => property.Name).ToArray();
            var properties = propertyNames.ToHashSet(StringComparer.Ordinal);
            if (properties.Count != propertyNames.Length)
            {
                throw Invalid("Repository descriptor contains a duplicate property.");
            }

            var root = document.RootElement;
            var descriptor = new RepositoryDescriptor
            {
                SchemaVersion = RequiredString(root, "schema_version"),
                RepositoryId = RequiredString(root, "repository_id"),
                InterfaceVersion = RequiredString(root, "interface_version"),
                NamespaceVersion = RequiredString(root, "namespace_version"),
                CatalogSchemaVersion = RequiredString(root, "catalog_schema_version"),
                CreatedUtc = RequiredString(root, "created_utc"),
                CreatedByWindowsAccount = RequiredString(root, "created_by_windows_account"),
                CatalogRelativePath = RequiredString(root, "catalog_relative_path"),
                RequiredFeatures = RequiredStrings(root, "required_features")
            };

            ValidateCommon(descriptor);
            encounteredProperties = properties;
            return descriptor;
        }
        catch (JsonException exception)
        {
            throw new RepositoryException(RepositoryErrorCode.DescriptorInvalid, "Repository descriptor is not valid JSON.", exception);
        }
    }

    public static bool HasOnlyVersionOneProperties(IReadOnlySet<string> properties) =>
        properties.SetEquals(AllowedProperties);

    private static string RequiredString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw Invalid($"Repository descriptor property '{name}' must be a string.");
        }

        return value.GetString()!;
    }

    private static IReadOnlyList<string> RequiredStrings(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            throw Invalid($"Repository descriptor property '{name}' must be an array.");
        }

        var result = new List<string>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || !unique.Add(item.GetString()!))
            {
                throw Invalid($"Repository descriptor property '{name}' must contain unique strings.");
            }

            result.Add(item.GetString()!);
        }

        return result;
    }

    private static void ValidateCommon(RepositoryDescriptor descriptor)
    {
        if (!Guid.TryParseExact(descriptor.RepositoryId, "D", out var id)
            || id == Guid.Empty
            || descriptor.RepositoryId != id.ToString("D"))
        {
            throw Invalid("Repository ID must be a non-empty canonical lowercase UUID.");
        }

        if (!Rfc3339Pattern().IsMatch(descriptor.CreatedUtc)
            || !DateTimeOffset.TryParse(
                descriptor.CreatedUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _))
        {
            throw Invalid("Repository creation time must be an RFC 3339 date-time.");
        }

        if (descriptor.CreatedByWindowsAccount.Length is < 1 or > 256)
        {
            throw Invalid("Creating Windows account must contain 1 to 256 characters.");
        }

        if (descriptor.RequiredFeatures.Count == 0)
        {
            throw Invalid("Repository descriptor must declare at least one required feature.");
        }
    }

    private static RepositoryException Invalid(string message) =>
        new(RepositoryErrorCode.DescriptorInvalid, message);

    [GeneratedRegex(
        @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:Z|[+-]\d{2}:\d{2})$",
        RegexOptions.CultureInvariant)]
    private static partial Regex Rfc3339Pattern();
}
