using System.Text.Json.Serialization;

namespace HumCapture.Coordinator.Repository;

/// <summary>Describes the non-PII identity and compatibility surface of one HumCapture repository.</summary>
public sealed record RepositoryDescriptor
{
    /// <summary>Gets the repository descriptor schema version.</summary>
    [JsonPropertyName("schema_version")]
    [JsonPropertyOrder(0)]
    public required string SchemaVersion { get; init; }

    /// <summary>Gets the canonical repository UUID.</summary>
    [JsonPropertyName("repository_id")]
    [JsonPropertyOrder(1)]
    public required string RepositoryId { get; init; }

    /// <summary>Gets the HC-IF-REP-001 interface version.</summary>
    [JsonPropertyName("interface_version")]
    [JsonPropertyOrder(2)]
    public required string InterfaceVersion { get; init; }

    /// <summary>Gets the repository namespace version.</summary>
    [JsonPropertyName("namespace_version")]
    [JsonPropertyOrder(3)]
    public required string NamespaceVersion { get; init; }

    /// <summary>Gets the SQLite catalog schema version.</summary>
    [JsonPropertyName("catalog_schema_version")]
    [JsonPropertyOrder(4)]
    public required string CatalogSchemaVersion { get; init; }

    /// <summary>Gets the repository creation time as an RFC 3339 value.</summary>
    [JsonPropertyName("created_utc")]
    [JsonPropertyOrder(5)]
    public required string CreatedUtc { get; init; }

    /// <summary>Gets the signed-in Windows account that created the repository.</summary>
    [JsonPropertyName("created_by_windows_account")]
    [JsonPropertyOrder(6)]
    public required string CreatedByWindowsAccount { get; init; }

    /// <summary>Gets the repository-relative catalog path.</summary>
    [JsonPropertyName("catalog_relative_path")]
    [JsonPropertyOrder(7)]
    public required string CatalogRelativePath { get; init; }

    /// <summary>Gets the features a reader must support before mutation.</summary>
    [JsonPropertyName("required_features")]
    [JsonPropertyOrder(8)]
    public required IReadOnlyList<string> RequiredFeatures { get; init; }
}
