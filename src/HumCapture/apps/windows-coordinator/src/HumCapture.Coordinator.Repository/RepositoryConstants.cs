namespace HumCapture.Coordinator.Repository;

internal static class RepositoryConstants
{
    public const string DescriptorFileName = "repository.json";
    public const string CatalogRelativePath = "catalog/humcapture.sqlite3";
    public const string DescriptorSchemaVersion = "1.0.0";
    public const string InterfaceVersion = "1.3.0";
    public const string NamespaceVersion = "1.0.0";
    public const string CatalogSchemaVersion = "1.0.0";

    public static IReadOnlyList<string> RequiredFeatures { get; } =
    [
        "JOURNAL_HISTORY",
        "IMMUTABLE_RECORD_INDEX",
        "BOUNDED_RECONCILIATION"
    ];
}
