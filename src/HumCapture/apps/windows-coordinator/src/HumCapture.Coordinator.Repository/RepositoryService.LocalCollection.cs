using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;

namespace HumCapture.Coordinator.Repository;

/// <summary>Byte collection result; not scientific verification or repository admission.</summary>
public sealed record LocalCollectionResult(string State, string StagingRelativePath, int CopiedFiles, int ReusedFiles);

public sealed partial class RepositoryService
{
    /// <summary>Copies a finalized ordinary folder into staging; never modifies the source.</summary>
    public LocalCollectionResult CollectLocalFolder(string rootPath, string sourcePath, Guid collectionAttemptId,
        CancellationToken cancellationToken = default)
    {
        if (collectionAttemptId == Guid.Empty) { throw new ArgumentException("Collection attempt is required."); }
        var opened = Open(rootPath);
        if (!opened.CanMutate) { throw new RepositoryException(RepositoryErrorCode.MutationNotAllowed, "Collection requires a writable repository."); }
        var source = RepositoryPathSafety.NormalizeAbsoluteRoot(sourcePath);
        var root = opened.RootPath;
        if (IsWithin(source, root) || IsWithin(root, source)) { throw CollectionConflict("Source and repository overlap."); }
        lock (_commitLocks.GetOrAdd(root, static _ => new object()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourceManifest = Path.Combine(source, "package-manifest.json");
            RepositoryPathSafety.RejectReparsePointsInExistingPath(sourceManifest);
            RepositoryPathSafety.RequireSingleLinkFile(sourceManifest);
            using var manifestGuard = new FileStream(sourceManifest, FileMode.Open, FileAccess.Read, FileShare.Read);
            StagedPackageEvidenceValidator.ValidateCollectedBytes(source);
            var manifestBytes = new byte[checked((int)manifestGuard.Length)];
            manifestGuard.ReadExactly(manifestBytes);
            using var document = JsonDocument.Parse(manifestBytes);
            var manifest = document.RootElement;
            var packageId = manifest.GetProperty("package_id").GetString()!;
            CheckCollectionIdentity(root, manifest);
            var relative = $"staging/{collectionAttemptId:D}/{packageId}";
            var attempt = RepositoryPathSafety.ResolveRelativePath(root, $"staging/{collectionAttemptId:D}");
            RepositoryPathSafety.RejectReparsePointsInExistingPath(attempt);
            var binding = Path.Combine(attempt, "collection-manifest.json");
            if (Directory.Exists(attempt))
            {
                RepositoryPathSafety.RequireSafeDirectoryTree(attempt);
                if (!File.Exists(binding)) { throw CollectionConflict("Existing collection has no manifest binding."); }
                RequireExactFile(binding, manifestBytes);
            }
            else
            {
                Directory.CreateDirectory(attempt);
                WriteNewDurable(binding, manifestBytes);
            }
            var payload = RepositoryPathSafety.ResolveRelativePath(root, relative);
            Directory.CreateDirectory(payload);
            var scratch = Path.Combine(attempt, "partials");
            Directory.CreateDirectory(scratch);
            var checkpointPath = Path.Combine(attempt, "collection-checkpoint.json");
            var checkpoint = LoadCollectionCheckpoint(checkpointPath, manifest, collectionAttemptId);
            var entries = checkpoint["artifacts"]!.AsArray();
            var artifacts = manifest.GetProperty("artifacts").EnumerateArray().ToArray();
            var copied = 0;
            var reused = 0;
            SaveCollectionCheckpoint(checkpointPath, checkpoint);
            for (var index = 0; index < artifacts.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var artifact = artifacts[index];
                var entry = entries[index]!.AsObject();
                var path = artifact.GetProperty("relative_path").GetString()!;
                var target = RepositoryPathSafety.ResolveRelativePath(payload, path);
                var origin = RepositoryPathSafety.ResolveRelativePath(source, path);
                var length = ulong.Parse(artifact.GetProperty("byte_length").GetString()!, CultureInfo.InvariantCulture);
                var hash = artifact.GetProperty("sha256").GetString()!;
                if (File.Exists(target))
                {
                    RequireCollectionFile(target, length, hash);
                    reused++;
                }
                else
                {
                    var partial = Path.Combine(scratch, artifact.GetProperty("artifact_id").GetString()!);
                    RepositoryPathSafety.RejectReparsePointsInExistingPath(partial);
                    if (File.Exists(partial))
                    {
                        RepositoryPathSafety.RequireSingleLinkFile(partial);
                        entry["restart_count"] = checked(entry["restart_count"]!.GetValue<int>() + 1);
                        entry["last_restart_reason"] = "OPERATOR_RETRY";
                    }
                    entry["attempt_count"] = checked(entry["attempt_count"]!.GetValue<int>() + 1);
                    entry["state"] = "RECEIVING";
                    entry["received_ranges"] = new JsonArray();
                    entry["staged_byte_length"] = "0";
                    SaveCollectionCheckpoint(checkpointPath, checkpoint);
                    RepositoryPathSafety.RejectReparsePointsInExistingPath(origin);
                    RepositoryPathSafety.RequireSingleLinkFile(origin);
                    using (var input = new FileStream(origin, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var buffer = new byte[1024 * 1024];
                        int count;
                        while ((count = input.Read(buffer)) != 0)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            output.Write(buffer, 0, count);
                        }
                        output.Flush(true);
                    }
                    RequireCollectionFile(partial, length, hash);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                    RepositoryPathSafety.RejectReparsePointsInExistingPath(target);
                    File.Move(partial, target, false);
                    copied++;
                }
                entry["state"] = "STAGED";
                entry["staged_byte_length"] = length.ToString(CultureInfo.InvariantCulture);
                entry["received_ranges"] = length == 0 ? new JsonArray() : new JsonArray(new JsonObject
                { ["start"] = "0", ["end_exclusive"] = length.ToString(CultureInfo.InvariantCulture) });
                SaveCollectionCheckpoint(checkpointPath, checkpoint);
            }
            cancellationToken.ThrowIfCancellationRequested();
            RequireExactFile(sourceManifest, manifestBytes);
            var finalManifest = Path.Combine(payload, "package-manifest.json");
            if (File.Exists(finalManifest)) { RequireExactFile(finalManifest, manifestBytes); }
            else
            {
                var ready = Path.Combine(scratch, "manifest.ready");
                RepositoryPathSafety.RejectReparsePointsInExistingPath(ready);
                if (File.Exists(ready)) { RepositoryPathSafety.RequireSingleLinkFile(ready); }
                using (var output = new FileStream(ready, FileMode.Create, FileAccess.Write, FileShare.None))
                { output.Write(manifestBytes); output.Flush(true); }
                File.Move(ready, finalManifest, false);
            }
            StagedPackageEvidenceValidator.ValidateCollectedBytes(payload);
            return new LocalCollectionResult("COLLECTED_UNVERIFIED", relative, copied, reused);
        }
    }

    private static bool IsWithin(string path, string parent) => path.Equals(parent, StringComparison.OrdinalIgnoreCase)
        || path.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static void CheckCollectionIdentity(string root, JsonElement manifest)
    {
        var id = manifest.GetProperty("package_id").GetString()!;
        var hash = manifest.GetProperty("package_content_sha256").GetString()!;
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = RepositoryPathSafety.ResolveRelativePath(root, RepositoryConstants.CatalogRelativePath),
            Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM repository_transactions WHERE package_id = $id";
        command.Parameters.AddWithValue("$id", id);
        if (Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) != 0)
        { throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Package already admitted; use repository recovery, not collection."); }
        var staging = Path.Combine(root, "staging");
        foreach (var directory in Directory.EnumerateDirectories(staging))
        {
            var binding = Path.Combine(directory, "collection-manifest.json");
            RepositoryPathSafety.RejectReparsePointsInExistingPath(binding);
            if (!File.Exists(binding)) { continue; }
            RepositoryPathSafety.RequireSingleLinkFile(binding);
            try
            {
            using var existing = JsonDocument.Parse(File.ReadAllBytes(binding));
            if (existing.RootElement.GetProperty("package_id").GetString() == id
                && existing.RootElement.GetProperty("package_content_sha256").GetString() != hash)
            { throw CollectionConflict("Package identity conflicts with retained collection; operator inspection required."); }
            }
            catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
            { throw CollectionConflict("Retained collection binding is invalid; operator inspection required."); }
        }
    }

    private static void RequireExactFile(string path, byte[] expected)
    {
        RepositoryPathSafety.RejectReparsePointsInExistingPath(path);
        RepositoryPathSafety.RequireSingleLinkFile(path);
        if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(expected)) { throw CollectionConflict("Collection manifest binding changed."); }
    }

    private static void RequireCollectionFile(string path, ulong length, string hash)
    {
        RepositoryPathSafety.RejectReparsePointsInExistingPath(path);
        RepositoryPathSafety.RequireSingleLinkFile(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if ((ulong)input.Length != length || Convert.ToHexStringLower(SHA256.HashData(input)) != hash)
        { throw CollectionConflict("Collected artifact differs; retained for inspection."); }
    }

    private static void WriteNewDurable(string path, byte[] bytes)
    {
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(bytes);
        output.Flush(true);
    }

    private static JsonObject LoadCollectionCheckpoint(string path, JsonElement manifest, Guid attempt)
    {
        var now = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var entries = new JsonArray();
        foreach (var artifact in manifest.GetProperty("artifacts").EnumerateArray())
        {
            entries.Add(new JsonObject
            {
                ["artifact_id"] = artifact.GetProperty("artifact_id").GetString(),
                ["relative_path"] = artifact.GetProperty("relative_path").GetString(),
                ["expected_byte_length"] = artifact.GetProperty("byte_length").GetString(),
                ["expected_sha256"] = artifact.GetProperty("sha256").GetString(),
                ["state"] = "PENDING", ["received_ranges"] = new JsonArray(),
                ["staged_byte_length"] = "0", ["attempt_count"] = 0, ["restart_count"] = 0
            });
        }
        var result = new JsonObject
        {
            ["schema_version"] = "1.0.0", ["checkpoint_id"] = attempt.ToString("D"), ["revision"] = 0,
            ["package_id"] = manifest.GetProperty("package_id").GetString(),
            ["package_content_sha256"] = manifest.GetProperty("package_content_sha256").GetString(),
            ["source_id"] = manifest.GetProperty("source_id").GetString(), ["collection_method"] = "COORDINATOR_LOCAL",
            ["created_utc"] = now, ["updated_utc"] = now, ["artifacts"] = entries
        };
        if (!File.Exists(path)) { return result; }
        RepositoryPathSafety.RequireSingleLinkFile(path);
        var prior = JsonNode.Parse(File.ReadAllBytes(path))!.AsObject();
        foreach (var key in new[] { "schema_version", "checkpoint_id", "package_id", "package_content_sha256", "source_id", "collection_method" })
        {
            if (!JsonNode.DeepEquals(prior[key], result[key])) { throw CollectionConflict("Checkpoint identity differs."); }
        }
        var oldEntries = prior["artifacts"]!.AsArray();
        if (oldEntries.Count != entries.Count) { throw CollectionConflict("Checkpoint inventory differs."); }
        for (var i = 0; i < entries.Count; i++)
        {
            foreach (var key in new[] { "artifact_id", "relative_path", "expected_byte_length", "expected_sha256" })
            {
                if (!JsonNode.DeepEquals(oldEntries[i]![key], entries[i]![key])) { throw CollectionConflict("Checkpoint artifact differs."); }
            }
            foreach (var key in new[] { "attempt_count", "restart_count" })
            {
                var count = oldEntries[i]![key]!.GetValue<int>();
                if (count < 0) { throw CollectionConflict("Checkpoint counter is invalid."); }
                entries[i]![key] = count;
            }
        }
        var revision = prior["revision"]!.GetValue<int>();
        var created = prior["created_utc"]!.GetValue<string>();
        if (revision < 1 || !DateTimeOffset.TryParse(created, CultureInfo.InvariantCulture, DateTimeStyles.None, out var createdTime)
            || createdTime.Offset != TimeSpan.Zero) { throw CollectionConflict("Checkpoint revision or timestamp is invalid."); }
        result["revision"] = revision;
        result["created_utc"] = created;
        return result;
    }

    private static void SaveCollectionCheckpoint(string path, JsonObject checkpoint)
    {
        checkpoint["revision"] = checked(checkpoint["revision"]!.GetValue<int>() + 1);
        checkpoint["updated_utc"] = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var temporary = path + ".tmp";
        RepositoryPathSafety.RejectReparsePointsInExistingPath(temporary);
        if (File.Exists(temporary)) { RepositoryPathSafety.RequireSingleLinkFile(temporary); }
        using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            output.Write(JsonSerializer.SerializeToUtf8Bytes(checkpoint));
            output.Flush(true);
        }
        File.Move(temporary, path, true);
    }

    private static RepositoryException CollectionConflict(string message) =>
        new(RepositoryErrorCode.EvidenceMismatch, message);
}
