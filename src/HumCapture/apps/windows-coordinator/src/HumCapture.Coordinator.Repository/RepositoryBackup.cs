using System.Globalization;
using System.Security.Cryptography;
using System.Security.Principal;
using Microsoft.Data.Sqlite;
using Microsoft.Win32.SafeHandles;

namespace HumCapture.Coordinator.Repository;

internal sealed record BackupFile(string Path, long Length, string Sha256);
internal sealed record BackupManifest(string RepositoryId, DateTimeOffset CreatedUtc, string WindowsAccount,
    string[] Directories, BackupFile[] Files, string SchemaVersion = "1.0.0");

// Quiescent maintenance only. The caller holds the cooperating host mutex.
internal static class RepositoryBackup
{
    internal static BackupManifest Create(string root, string destination, CancellationToken token = default,
        Action<string>? testBoundary = null)
    {
        root = SnapshotLease.Local(root); destination = Destination(root, destination);
        using var guard = VerificationWorkflowStore.Lock(root);
        using var source = SnapshotLease.Open(root, true, token);
        var repository = ValidateRepository(root);
        var temporary = destination + ".partial-" + Guid.NewGuid().ToString("N");
        Directory.CreateDirectory(temporary);
        var payload = Path.Combine(temporary, "payload");
        var files = source.Copy(payload, token, testBoundary);
        using var actor = WindowsIdentity.GetCurrent();
        var manifest = new BackupManifest(repository, DateTimeOffset.UtcNow, actor.Name, source.Directories, files);
        WriteNew(Path.Combine(temporary, "backup-manifest.json"), WorkflowJson.Encode(manifest));
        source.Check(token);
        using (var copied = SnapshotLease.Open(payload, false, token)) { Verify(copied, manifest, token); }
        testBoundary?.Invoke("BACKUP_READY"); token.ThrowIfCancellationRequested();
        Directory.Move(temporary, destination);
        return manifest;
    }
    internal static BackupManifest Restore(string source, string destination, CancellationToken token = default,
        Action<string>? testBoundary = null)
    {
        source = SnapshotLease.Local(source); destination = Destination(source, destination);
        PackageInputManifest.Need(!Path.GetFileName(source).Contains(".partial-", StringComparison.OrdinalIgnoreCase), "Incomplete backup cannot be restored.");
        var manifest = WorkflowJson.ReadFile<BackupManifest>(Path.Combine(source, "backup-manifest.json"));
        var names = Directory.GetFileSystemEntries(source).Select(Path.GetFileName).ToHashSet(StringComparer.Ordinal);
        PackageInputManifest.Need(names.SetEquals(["payload", "backup-manifest.json"]), "Backup envelope has unexpected material.");
        using var snapshot = SnapshotLease.Open(Path.Combine(source, "payload"), false, token);
        Verify(snapshot, manifest, token);
        var temporary = destination + ".partial-" + Guid.NewGuid().ToString("N");
        var copied = snapshot.Copy(temporary, token, testBoundary);
        PackageInputManifest.Need(copied.SequenceEqual(manifest.Files), "Restore copy differs from inventory.");
        PackageInputManifest.Need(ValidateRepository(temporary) == manifest.RepositoryId, "Restored repository identity differs.");
        snapshot.Check(token); testBoundary?.Invoke("RESTORE_READY"); token.ThrowIfCancellationRequested();
        Directory.Move(temporary, destination); return manifest;
    }
    private static void Verify(SnapshotLease source, BackupManifest manifest, CancellationToken token)
    {
        if (manifest.Files is null || manifest.Directories is null || Array.Exists(manifest.Files, file => file is null))
        { throw new InvalidDataException("Missing backup inventory."); }
        PackageInputManifest.Need(manifest.SchemaVersion == "1.0.0" && Guid.TryParseExact(manifest.RepositoryId, "D", out var id)
            && id != Guid.Empty && manifest.CreatedUtc != default && !string.IsNullOrWhiteSpace(manifest.WindowsAccount)
            && manifest.Files.Length <= 10000 && manifest.Directories.Length <= 10000, "Unsupported backup manifest.");
        PackageInputManifest.Need(source.Directories.SequenceEqual(manifest.Directories)
            && source.Files.Keys.SequenceEqual(manifest.Files.Select(f => f.Path)), "Backup inventory differs.");
        foreach (var item in manifest.Files)
        {
            token.ThrowIfCancellationRequested(); var input = source.Files[item.Path];
            PackageInputManifest.Need(input.Length == item.Length && Hash(input, token) == item.Sha256, "Backup bytes differ.");
        }
        PackageInputManifest.Need(ValidateRepository(source.Root) == manifest.RepositoryId, "Backup repository identity differs.");
        source.Check(token);
    }
    private static string Destination(string source, string destination)
    {
        destination = SnapshotLease.Local(destination);
        var left = source.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var right = destination.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        PackageInputManifest.Need(!left.StartsWith(right, StringComparison.OrdinalIgnoreCase)
            && !right.StartsWith(left, StringComparison.OrdinalIgnoreCase) && !Path.Exists(destination), "Destination exists or overlaps source.");
        return destination;
    }
    private static string ValidateRepository(string root)
    {
        var opened = new RepositoryService().Open(root);
        PackageInputManifest.Need(opened.CanMutate, "Backup requires compatible repository.");
        using var database = new SqliteConnection(new SqliteConnectionStringBuilder
        { DataSource = Path.Combine(root, VerificationWorkflowStore.RelativePath), Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        database.Open(); using var command = database.CreateCommand();
        command.CommandText = "PRAGMA integrity_check";
        PackageInputManifest.Need((string?)command.ExecuteScalar() == "ok", "Workflow integrity check failed.");
        command.CommandText = "PRAGMA foreign_key_check";
        PackageInputManifest.Need(command.ExecuteScalar() is null, "Workflow foreign-key check failed.");
        command.CommandText = "PRAGMA user_version";
        PackageInputManifest.Need(Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1, "Unknown workflow version.");
        command.CommandText = "SELECT repository_id FROM workflow_metadata WHERE singleton=1 AND schema_version='1.0.0'";
        PackageInputManifest.Need((string?)command.ExecuteScalar() == opened.Descriptor.RepositoryId, "Workflow repository mismatch.");
        command.CommandText = "SELECT payload,sha256 FROM capture_assignments UNION ALL SELECT request,sha256 FROM verification_attempts UNION ALL SELECT payload,sha256 FROM verification_events";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            PackageInputManifest.Need(reader.GetBytes(0, 0, null, 0, 0) <= WorkflowJson.MaximumBytes, "Workflow payload exceeds backup envelope.");
            PackageInputManifest.Need(PackageInputManifest.Hash((byte[])reader[0]) == reader.GetString(1), "Workflow payload hash mismatch.");
        }
        return opened.Descriptor.RepositoryId;
    }
    private static void WriteNew(string path, byte[] bytes)
    {
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough);
        output.Write(bytes); output.Flush(true);
    }
    internal static string Hash(Stream source, CancellationToken token)
    {
        source.Position = 0; using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[65536];
        int count;
        while ((count = source.Read(buffer)) != 0) { token.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, count); }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}

internal sealed class SnapshotLease : IDisposable
{
    internal string Root { get; }
    internal SortedDictionary<string, FileStream> Files { get; } = new(StringComparer.Ordinal);
    internal string[] Directories { get; private set; } = [];
    private readonly List<SafeFileHandle> handles = [];
    private readonly bool omitLock;
    private SnapshotLease(string root, bool omitLock) { Root = root; this.omitLock = omitLock; }
    internal static string Local(string root)
    {
        root = RepositoryPathSafety.NormalizeAbsoluteRoot(root);
        PackageInputManifest.Need(OperatingSystem.IsWindows() && Path.GetPathRoot(root) is { Length: 3 } drive
            && drive[1] == ':' && new DriveInfo(drive).DriveType != DriveType.Network, "Local Windows volume required.");
        RepositoryPathSafety.RejectReparsePointsInExistingPath(root); return root;
    }
    internal static SnapshotLease Open(string root, bool omitLock, CancellationToken token)
    {
        var lease = new SnapshotLease(Local(root), omitLock);
        try
        {
            for (var parent = lease.Root; parent is not null; parent = Path.GetDirectoryName(parent)) { lease.Pin(parent); }
            var inventory = lease.Inventory(token);
            lease.Directories = inventory.Where(e => e.Directory).Select(e => e.Relative).ToArray();
            foreach (var directory in lease.Directories) { lease.Pin(RepositoryPathSafety.ResolveRelativePath(root, directory)); }
            foreach (var relative in inventory.Where(e => !e.Directory).Select(e => e.Relative))
            {
                var path = RepositoryPathSafety.ResolveRelativePath(root, relative);
                var handle = PackageInputLease.OpenHandle(path, 0x80000000, 1, 0x00200000);
                try { PackageInputLease.CheckHandle(handle, false); lease.Files.Add(relative, new FileStream(handle, FileAccess.Read, 65536, false)); }
                catch { handle.Dispose(); throw; }
            }
            lease.Check(token); return lease;
        }
        catch { lease.Dispose(); throw; }
    }
    private void Pin(string directory)
    {
        var handle = PackageInputLease.OpenHandle(directory, 0x80, 3, 0x02200000);
        try { PackageInputLease.CheckHandle(handle, true); handles.Add(handle); } catch { handle.Dispose(); throw; }
    }
    private (string Relative, bool Directory)[] Inventory(CancellationToken token)
    {
        var entries = new List<(string Relative, bool Directory)>(); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>(); pending.Enqueue(Root);
        while (pending.TryDequeue(out var directory))
        {
            foreach (var path in System.IO.Directory.EnumerateFileSystemEntries(directory))
            {
                token.ThrowIfCancellationRequested(); var relative = Path.GetRelativePath(Root, path).Replace('\\', '/');
                if (omitLock && relative == "catalog/verification-workflow.lock") { continue; }
                var attributes = File.GetAttributes(path); var isDirectory = (attributes & FileAttributes.Directory) != 0;
                PackageInputManifest.Need((attributes & FileAttributes.ReparsePoint) == 0 && seen.Add(relative) && entries.Count < 10000,
                    "Unsafe or oversized backup inventory.");
                PackageInputManifest.Need(!relative.EndsWith(".sqlite3-journal", StringComparison.OrdinalIgnoreCase)
                    && !relative.EndsWith(".sqlite3-wal", StringComparison.OrdinalIgnoreCase)
                    && !relative.EndsWith(".sqlite3-shm", StringComparison.OrdinalIgnoreCase), "Close/reconcile SQLite writers before backup.");
                entries.Add((relative, isDirectory)); if (isDirectory) { pending.Enqueue(path); }
            }
        }
        return entries.OrderBy(e => e.Relative, StringComparer.Ordinal).ToArray();
    }
    internal void Check(CancellationToken token)
    {
        var inventory = Inventory(token);
        PackageInputManifest.Need(Directories.SequenceEqual(inventory.Where(e => e.Directory).Select(e => e.Relative))
            && Files.Keys.SequenceEqual(inventory.Where(e => !e.Directory).Select(e => e.Relative)), "Repository inventory changed during snapshot.");
        foreach (var file in Files.Values) { PackageInputLease.CheckHandle(file.SafeFileHandle, false); }
    }
    internal BackupFile[] Copy(string destination, CancellationToken token, Action<string>? boundary)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directories) { Directory.CreateDirectory(RepositoryPathSafety.ResolveRelativePath(destination, directory)); }
        var copied = new List<BackupFile>(); var buffer = new byte[65536];
        foreach (var (relative, input) in Files)
        {
            token.ThrowIfCancellationRequested(); input.Position = 0;
            var path = RepositoryPathSafety.ResolveRelativePath(destination, relative);
            using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.WriteThrough))
            {
                int count;
                while ((count = input.Read(buffer)) != 0) { token.ThrowIfCancellationRequested(); output.Write(buffer, 0, count); }
                output.Flush(true);
            }
            using var check = File.OpenRead(path); var hash = RepositoryBackup.Hash(input, token);
            PackageInputManifest.Need(check.Length == input.Length && RepositoryBackup.Hash(check, token) == hash, "Copied bytes differ.");
            copied.Add(new(relative, input.Length, hash)); boundary?.Invoke("FILE_COPIED");
        }
        Check(token); return copied.ToArray();
    }
    public void Dispose()
    {
        foreach (var file in Files.Values) { file.Dispose(); }
        foreach (var handle in handles.AsEnumerable().Reverse()) { handle.Dispose(); }
    }
}
