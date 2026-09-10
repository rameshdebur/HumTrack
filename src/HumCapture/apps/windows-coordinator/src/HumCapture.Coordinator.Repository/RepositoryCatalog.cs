using System.Reflection;
using Microsoft.Data.Sqlite;

namespace HumCapture.Coordinator.Repository;

internal static class RepositoryCatalog
{
    public static void Create(string databasePath, RepositoryDescriptor descriptor)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        };

        using (var connection = new SqliteConnection(builder.ConnectionString))
        {
            connection.Open();
            Execute(connection, "PRAGMA foreign_keys = ON; PRAGMA journal_mode = DELETE; PRAGMA synchronous = FULL;");
            using var transaction = connection.BeginTransaction();
            using var schema = connection.CreateCommand();
            schema.Transaction = transaction;
            schema.CommandText = LoadSchema();
            schema.ExecuteNonQuery();

            using var metadata = connection.CreateCommand();
            metadata.Transaction = transaction;
            metadata.CommandText = """
                INSERT INTO repository_metadata (
                  singleton_id, schema_version, repository_id, interface_version,
                  namespace_version, catalog_schema_version, created_utc,
                  created_by_windows_account
                ) VALUES (
                  1, $schema_version, $repository_id, $interface_version,
                  $namespace_version, $catalog_schema_version, $created_utc,
                  $created_by_windows_account
                );
                """;
            metadata.Parameters.AddWithValue("$schema_version", descriptor.SchemaVersion);
            metadata.Parameters.AddWithValue("$repository_id", descriptor.RepositoryId);
            metadata.Parameters.AddWithValue("$interface_version", descriptor.InterfaceVersion);
            metadata.Parameters.AddWithValue("$namespace_version", descriptor.NamespaceVersion);
            metadata.Parameters.AddWithValue("$catalog_schema_version", descriptor.CatalogSchemaVersion);
            metadata.Parameters.AddWithValue("$created_utc", descriptor.CreatedUtc);
            metadata.Parameters.AddWithValue("$created_by_windows_account", descriptor.CreatedByWindowsAccount);
            metadata.ExecuteNonQuery();
            transaction.Commit();
            RequireIntegrity(connection);
        }

        using var file = new FileStream(databasePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read, 4096, FileOptions.WriteThrough);
        file.Flush(flushToDisk: true);
    }

    public static void Verify(string databasePath, RepositoryDescriptor descriptor)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        };

        try
        {
            using var connection = new SqliteConnection(builder.ConnectionString);
            connection.Open();
            Execute(connection, "PRAGMA foreign_keys = ON;");
            RequireIntegrity(connection);

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT schema_version, repository_id, interface_version,
                       namespace_version, catalog_schema_version, created_utc,
                       created_by_windows_account
                  FROM repository_metadata
                 WHERE singleton_id = 1;
                """;
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                throw MetadataMismatch("Catalog repository metadata is missing.");
            }

            var actual = new[]
            {
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4), reader.GetString(5),
                reader.GetString(6)
            };
            var expected = new[]
            {
                descriptor.SchemaVersion, descriptor.RepositoryId, descriptor.InterfaceVersion,
                descriptor.NamespaceVersion, descriptor.CatalogSchemaVersion, descriptor.CreatedUtc,
                descriptor.CreatedByWindowsAccount
            };
            if (!actual.SequenceEqual(expected, StringComparer.Ordinal) || reader.Read())
            {
                throw MetadataMismatch("Catalog repository metadata does not exactly match repository.json.");
            }
        }
        catch (SqliteException exception)
        {
            throw new RepositoryException(RepositoryErrorCode.CatalogInvalid, "Repository catalog could not be verified.", exception);
        }
    }

    private static string LoadSchema()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("HumCapture.Contracts.repository-v1.sql")
            ?? throw new InvalidOperationException("Embedded repository-v1.sql contract is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void RequireIntegrity(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        var result = command.ExecuteScalar() as string;
        if (!string.Equals(result, "ok", StringComparison.Ordinal))
        {
            throw new RepositoryException(RepositoryErrorCode.CatalogInvalid, $"Repository catalog integrity check failed: {result ?? "no result"}");
        }
    }

    private static RepositoryException MetadataMismatch(string message) =>
        new(RepositoryErrorCode.CatalogMetadataMismatch, message);
}
