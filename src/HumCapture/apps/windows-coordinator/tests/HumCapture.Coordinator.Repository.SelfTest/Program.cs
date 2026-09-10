using System.Security.Cryptography;
using System.Diagnostics;
using System.Text.Json;
using HumCapture.Coordinator.Repository;
using Microsoft.Data.Sqlite;

var tests = new (string Name, Action Body)[]
{
    ("HC-REP-RUNTIME-001 initialize creates exact repository surface", InitializeCreatesSurface),
    ("HC-REP-RUNTIME-002 supported repository reopens for mutation", SupportedRepositoryOpens),
    ("HC-REP-RUNTIME-003 descriptor and catalog metadata agree", MetadataAgrees),
    ("HC-REP-RUNTIME-004 initialization never overwrites", ReinitializeRefuses),
    ("HC-REP-RUNTIME-005 nonempty root is refused", NonemptyRootRefused),
    ("HC-REP-RUNTIME-006 relative root is refused", RelativeRootRefused),
    ("HC-REP-RUNTIME-007 unsupported major is read-only without writes", UnsupportedMajorIsReadOnly),
    ("HC-REP-RUNTIME-008 older version requires explicit migration", OlderVersionIsReadOnly),
    ("HC-REP-RUNTIME-009 unknown required feature is read-only", UnknownFeatureIsReadOnly),
    ("HC-REP-RUNTIME-010 unknown field on supported descriptor is invalid", UnknownFieldIsInvalid),
    ("HC-REP-RUNTIME-011 missing catalog is refused", MissingCatalogRefused),
    ("HC-REP-RUNTIME-012 corrupt catalog is refused", CorruptCatalogRefused),
    ("HC-REP-RUNTIME-013 metadata mismatch is refused", MetadataMismatchRefused),
    ("HC-REP-RUNTIME-014 descriptor publication leaves no temporary file", NoTemporaryDescriptorRemains),
    ("HC-REP-RUNTIME-015 empty repository UUID is refused", EmptyRepositoryIdRefused),
    ("HC-REP-RUNTIME-016 hard-linked catalog is refused", HardLinkedCatalogRefused),
    ("HC-REP-RUNTIME-017 retained initialization lock is not removed", RetainedInitializationLockRefused)
};

var failures = 0;
foreach (var (name, body) in tests)
{
    try
    {
        body();
        await Console.Out.WriteLineAsync($"PASS {name}");
    }
    catch (Exception exception)
    {
        failures++;
        await Console.Error.WriteLineAsync($"FAIL {name}: {exception}");
    }
}

await Console.Out.WriteLineAsync($"SUMMARY total={tests.Length} passed={tests.Length - failures} failed={failures}");
return failures == 0 ? 0 : 1;

static void InitializeCreatesSurface()
{
    WithRepository((root, _) =>
    {
        True(new[] { "catalog", "quarantine", "repository.json", "staging", "subjects" }.SequenceEqual(
            Directory.EnumerateFileSystemEntries(root).Select(Path.GetFileName).Order()));
        True(File.Exists(Path.Combine(root, "catalog", "humcapture.sqlite3")));
        using var json = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "repository.json")));
        Equal(9, json.RootElement.EnumerateObject().Count());
        Equal("1.3.0", json.RootElement.GetProperty("interface_version").GetString());
    });
}

static void SupportedRepositoryOpens()
{
    WithRepository((root, service) =>
    {
        var opened = service.Open(root);
        Equal(RepositoryAccessMode.MutationAllowed, opened.AccessMode);
        Equal("SUPPORTED", opened.ReasonCode);
    });
}

static void MetadataAgrees()
{
    WithRepository((root, _) =>
    {
        using var descriptor = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "repository.json")));
        using var connection = OpenCatalog(root, SqliteOpenMode.ReadOnly);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT repository_id, created_by_windows_account FROM repository_metadata WHERE singleton_id = 1";
        using var reader = command.ExecuteReader();
        True(reader.Read());
        Equal(descriptor.RootElement.GetProperty("repository_id").GetString(), reader.GetString(0));
        Equal("TEST\\operator", reader.GetString(1));
    });
}

static void ReinitializeRefuses()
{
    WithRepository((root, service) =>
    {
        var before = Hash(Path.Combine(root, "repository.json"));
        Throws(RepositoryErrorCode.AlreadyInitialized, () => service.Initialize(root, "TEST\\operator"));
        Equal(before, Hash(Path.Combine(root, "repository.json")));
    });
}

static void NonemptyRootRefused()
{
    WithTemporaryRoot(root =>
    {
        File.WriteAllText(Path.Combine(root, "unrelated.txt"), "retain");
        Throws(RepositoryErrorCode.RootNotEmpty, () => new RepositoryService().Initialize(root, "TEST\\operator"));
        Equal("retain", File.ReadAllText(Path.Combine(root, "unrelated.txt")));
    });
}

static void RelativeRootRefused() =>
    Throws(RepositoryErrorCode.RootPathNotAbsolute, () => new RepositoryService().Initialize("relative-repository", "TEST\\operator"));

static void UnsupportedMajorIsReadOnly()
{
    WithRepository((root, service) =>
    {
        EditDescriptor(root, descriptor => descriptor["interface_version"] = "2.0.0");
        AssertReadOnlyWithoutCatalogWrite(root, service, "UNSUPPORTED_INTERFACE_MAJOR");
    });
}

static void OlderVersionIsReadOnly()
{
    WithRepository((root, service) =>
    {
        EditDescriptor(root, descriptor => descriptor["interface_version"] = "1.2.0");
        AssertReadOnlyWithoutCatalogWrite(root, service, "MIGRATION_REQUIRED");
    });
}

static void UnknownFeatureIsReadOnly()
{
    WithRepository((root, service) =>
    {
        EditDescriptor(root, descriptor =>
        {
            var features = (List<object?>)descriptor["required_features"]!;
            features.Add("FUTURE_REQUIRED_FEATURE");
        });
        AssertReadOnlyWithoutCatalogWrite(root, service, "UNKNOWN_REQUIRED_FEATURE");
    });
}

static void UnknownFieldIsInvalid()
{
    WithRepository((root, service) =>
    {
        EditDescriptor(root, descriptor => descriptor["unexpected"] = true);
        Throws(RepositoryErrorCode.DescriptorInvalid, () => service.Open(root));
    });
}

static void MissingCatalogRefused()
{
    WithRepository((root, service) =>
    {
        File.Delete(Path.Combine(root, "catalog", "humcapture.sqlite3"));
        Throws(RepositoryErrorCode.CatalogMissing, () => service.Open(root));
    });
}

static void CorruptCatalogRefused()
{
    WithRepository((root, service) =>
    {
        File.WriteAllText(Path.Combine(root, "catalog", "humcapture.sqlite3"), "not sqlite");
        Throws(RepositoryErrorCode.CatalogInvalid, () => service.Open(root));
    });
}

static void MetadataMismatchRefused()
{
    WithRepository((root, service) =>
    {
        using (var connection = OpenCatalog(root, SqliteOpenMode.ReadWrite))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "UPDATE repository_metadata SET created_by_windows_account = 'TEST\\other' WHERE singleton_id = 1";
            command.ExecuteNonQuery();
        }
        Throws(RepositoryErrorCode.CatalogMetadataMismatch, () => service.Open(root));
    });
}

static void NoTemporaryDescriptorRemains()
{
    WithRepository((root, _) =>
    {
        True(!Directory.EnumerateFiles(root, ".repository-*.tmp").Any());
        True(!File.Exists(Path.Combine(root, ".humcapture-initialize.lock")));
    });
}

static void EmptyRepositoryIdRefused()
{
    WithTemporaryRoot(root =>
    {
        try
        {
            _ = new RepositoryService().Initialize(root, "TEST\\operator", repositoryId: Guid.Empty);
            throw new InvalidOperationException("Expected ArgumentException.");
        }
        catch (ArgumentException exception)
        {
            True(exception.ParamName == "repositoryId");
        }
    });
}

static void HardLinkedCatalogRefused()
{
    WithRepository((root, service) =>
    {
        var catalog = Path.Combine(root, "catalog", "humcapture.sqlite3");
        CreateHardLink(Path.Combine(root, "catalog", "catalog-hardlink.sqlite3"), catalog);
        Throws(RepositoryErrorCode.UnsafePath, () => service.Open(root));
    });
}

static void RetainedInitializationLockRefused()
{
    WithTemporaryRoot(root =>
    {
        var lockPath = Path.Combine(root, ".humcapture-initialize.lock");
        File.WriteAllText(lockPath, "retained-for-inspection");
        Throws(RepositoryErrorCode.InitializationInProgress, () => new RepositoryService().Initialize(root, "TEST\\operator"));
        Equal("retained-for-inspection", File.ReadAllText(lockPath));
    });
}

static void WithRepository(Action<string, RepositoryService> action)
{
    WithTemporaryRoot(root =>
    {
        var service = new RepositoryService();
        var result = service.Initialize(
            root,
            "TEST\\operator",
            new DateTimeOffset(2026, 9, 10, 10, 30, 0, TimeSpan.Zero),
            Guid.Parse("10000000-0000-4000-8000-000000000001"));
        Equal(RepositoryAccessMode.MutationAllowed, result.AccessMode);
        action(root, service);
    });
}

static void WithTemporaryRoot(Action<string> action)
{
    var root = Path.Combine(Path.GetTempPath(), $"humcapture-repository-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(root);
    try
    {
        action(root);
    }
    finally
    {
        Directory.Delete(root, recursive: true);
    }
}

static SqliteConnection OpenCatalog(string root, SqliteOpenMode mode)
{
    var connection = new SqliteConnection(new SqliteConnectionStringBuilder
    {
        DataSource = Path.Combine(root, "catalog", "humcapture.sqlite3"),
        Mode = mode,
        Pooling = false
    }.ConnectionString);
    connection.Open();
    return connection;
}

static void CreateHardLink(string linkPath, string existingPath)
{
    var startInfo = new ProcessStartInfo("fsutil.exe")
    {
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };
    startInfo.ArgumentList.Add("hardlink");
    startInfo.ArgumentList.Add("create");
    startInfo.ArgumentList.Add(linkPath);
    startInfo.ArgumentList.Add(existingPath);
    using var process = Process.Start(startInfo)
        ?? throw new InvalidOperationException("Could not start fsutil hardlink test helper.");
    process.WaitForExit();
    if (process.ExitCode != 0)
    {
        throw new InvalidOperationException($"Could not create test hard link: {process.StandardError.ReadToEnd()}");
    }
}

static void AssertReadOnlyWithoutCatalogWrite(string root, RepositoryService service, string reason)
{
    var catalog = Path.Combine(root, "catalog", "humcapture.sqlite3");
    var hash = Hash(catalog);
    var lastWrite = File.GetLastWriteTimeUtc(catalog);
    var result = service.Open(root);
    Equal(RepositoryAccessMode.ReadOnlyInspection, result.AccessMode);
    Equal(reason, result.ReasonCode);
    Equal(hash, Hash(catalog));
    Equal(lastWrite, File.GetLastWriteTimeUtc(catalog));
}

static void EditDescriptor(string root, Action<Dictionary<string, object?>> edit)
{
    var path = Path.Combine(root, "repository.json");
    var values = JsonSerializer.Deserialize<Dictionary<string, object?>>(File.ReadAllBytes(path))!;
    foreach (var key in values.Keys.ToArray())
    {
        if (values[key] is JsonElement element)
        {
            values[key] = ConvertElement(element);
        }
    }
    edit(values);
    File.WriteAllText(path, JsonSerializer.Serialize(values, new JsonSerializerOptions { WriteIndented = true }));
}

static object? ConvertElement(JsonElement element) => element.ValueKind switch
{
    JsonValueKind.Array => element.EnumerateArray().Select(ConvertElement).ToList(),
    JsonValueKind.String => element.GetString(),
    JsonValueKind.Number => element.GetInt64(),
    JsonValueKind.True => true,
    JsonValueKind.False => false,
    JsonValueKind.Null => null,
    _ => throw new InvalidOperationException($"Unexpected fixture value kind {element.ValueKind}.")
};

static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

static void Throws(RepositoryErrorCode expected, Action action)
{
    try
    {
        action();
    }
    catch (RepositoryException exception) when (exception.Code == expected)
    {
        return;
    }

    throw new InvalidOperationException($"Expected RepositoryException code {expected}.");
}

static void True(bool value)
{
    if (!value)
    {
        throw new InvalidOperationException("Expected true.");
    }
}

static void Equal<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', actual '{actual}'.");
    }
}
