using System.Globalization;
using System.Security.Principal;
using Microsoft.Data.Sqlite;

namespace HumCapture.Coordinator.Repository;

internal sealed class VerificationWorkflowStore : IDisposable
{
    internal const string RelativePath = "catalog/verification-workflow.sqlite3";
    private readonly SqliteConnection connection;
    private VerificationWorkflowStore(SqliteConnection connection) { this.connection = connection; }
    internal static FileStream Lock(string root)
    {
        var path = RepositoryPathSafety.ResolveRelativePath(root, "catalog/verification-workflow.lock");
        RepositoryPathSafety.RejectReparsePointsInExistingPath(path);
        if (File.Exists(path)) { RepositoryPathSafety.RequireSingleLinkFile(path); }
        var guard = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        try { RepositoryPathSafety.RequireSingleLinkFile(guard.SafeFileHandle, path); return guard; }
        catch { guard.Dispose(); throw; }
    }
    internal static void Initialize(RepositoryOpenResult repository)
    {
        var path = RepositoryPathSafety.ResolveRelativePath(repository.RootPath, RelativePath);
        if (File.Exists(path)) { using var existing = Open(repository); return; }
        var temporary = path + "." + Guid.NewGuid().ToString("N");
        try
        {
            using (var database = Connect(temporary, SqliteOpenMode.ReadWriteCreate))
            {
                using var transaction = database.BeginTransaction();
                using var source = typeof(VerificationWorkflowStore).Assembly.GetManifestResourceStream("HumCapture.Workflow.schema.sql")!;
                using var text = new StreamReader(source);
                using var schema = database.CreateCommand(); schema.Transaction = transaction;
                schema.CommandText = text.ReadToEnd(); schema.ExecuteNonQuery();
                using var metadata = database.CreateCommand(); metadata.Transaction = transaction;
                metadata.CommandText = "INSERT INTO workflow_metadata VALUES(1,$id,'1.0.0')";
                metadata.Parameters.AddWithValue("$id", repository.Descriptor.RepositoryId); metadata.ExecuteNonQuery();
                transaction.Commit();
            }
            File.Move(temporary, path, false);
        }
        finally { if (File.Exists(temporary)) { File.Delete(temporary); } }
    }
    internal static VerificationWorkflowStore Open(RepositoryOpenResult repository)
    {
        var path = RepositoryPathSafety.ResolveRelativePath(repository.RootPath, RelativePath);
        RepositoryPathSafety.RejectReparsePointsInExistingPath(path);
        PackageInputManifest.Need(File.Exists(path), "Workflow ledger requires explicit initialization.");
        RepositoryPathSafety.RequireSingleLinkFile(path);
        foreach (var suffix in new[] { "-journal", "-wal", "-shm" })
        {
            RepositoryPathSafety.RejectReparsePointsInExistingPath(path + suffix);
            if (File.Exists(path + suffix)) { RepositoryPathSafety.RequireSingleLinkFile(path + suffix); }
        }
        var database = Connect(path, SqliteOpenMode.ReadWrite);
        try
        {
            using var command = database.CreateCommand(); command.CommandText = "PRAGMA user_version";
            PackageInputManifest.Need(Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture) == 1, "Unsupported workflow database version.");
            command.CommandText = "SELECT repository_id, schema_version FROM workflow_metadata WHERE singleton=1";
            using var reader = command.ExecuteReader();
            PackageInputManifest.Need(reader.Read() && reader.GetString(0) == repository.Descriptor.RepositoryId
                && reader.GetString(1) == "1.0.0", "Workflow ledger belongs to another repository/version.");
            return new(database);
        }
        catch { database.Dispose(); throw; }
    }
    private static SqliteConnection Connect(string path, SqliteOpenMode mode)
    {
        var database = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = mode, Pooling = false }.ToString());
        try
        {
            database.Open(); using var command = database.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys=ON; PRAGMA synchronous=FULL;"; command.ExecuteNonQuery();
            return database;
        }
        catch { database.Dispose(); throw; }
    }
    internal void SaveAssignment(CaptureAssignment assignment)
    {
        var bytes = WorkflowJson.Encode(assignment);
        var existing = Read("SELECT payload,sha256 FROM capture_assignments WHERE assignment_id=$id", assignment.AssignmentId);
        if (existing is not null)
        { PackageInputManifest.Need(existing.AsSpan().SequenceEqual(bytes), "Immutable assignment conflict."); return; }
        ValidateSession(assignment);
        using var actor = WindowsIdentity.GetCurrent();
        Execute("INSERT INTO capture_assignments VALUES($id,$capture,$body,$hash,$actor,$utc)",
            ("$id", assignment.AssignmentId.ToString("D")), ("$capture", assignment.CaptureAttemptId.ToString("D")),
            ("$body", bytes), ("$hash", PackageInputManifest.Hash(bytes)), ("$actor", actor.Name), ("$utc", Utc()));
    }
    private void ValidateSession(CaptureAssignment assignment)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload,sha256 FROM capture_assignments WHERE json_extract(CAST(payload AS TEXT),'$.session_id')=$session";
        command.Parameters.AddWithValue("$session", assignment.SessionId.ToString("D"));
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var previous = WorkflowJson.Read<CaptureAssignment>(Checked(reader));
            PackageInputManifest.Need(previous.SubjectId == assignment.SubjectId
                && previous.ProtocolUtf8.AsSpan().SequenceEqual(assignment.ProtocolUtf8), "Session subject/protocol conflict.");
            if (previous.TrialId != assignment.TrialId) { continue; }
            PackageInputManifest.Need(previous.SlotId == assignment.SlotId
                && (previous.RoleId != assignment.RoleId || previous.SourceId == assignment.SourceId)
                && (previous.SourceId != assignment.SourceId || previous.RoleId == assignment.RoleId), "Trial slot/source/role conflict.");
        }
    }
    internal CaptureAssignment Assignment(Guid id)
    {
        var bytes = Read("SELECT payload,sha256 FROM capture_assignments WHERE assignment_id=$id", id)
            ?? throw new InvalidDataException("Coordinator assignment is absent.");
        var assignment = WorkflowJson.Read<CaptureAssignment>(bytes); assignment.Validate();
        PackageInputManifest.Need(assignment.AssignmentId == id, "Assignment key mismatch."); return assignment;
    }
    internal byte[]? Request(Guid id) => Read("SELECT request,sha256 FROM verification_attempts WHERE attempt_id=$id", id);
    internal void Start(BoundVerificationRequest request)
    {
        using var actor = WindowsIdentity.GetCurrent(); var bytes = WorkflowJson.Encode(request);
        using var transaction = connection.BeginTransaction();
        Execute("INSERT INTO verification_attempts VALUES($id,$assignment,$body,$hash,$actor,$utc)",
            [("$id", request.Admission.VerificationRecordId.ToString("D")), ("$assignment", request.AssignmentId.ToString("D")),
             ("$body", bytes), ("$hash", PackageInputManifest.Hash(bytes)), ("$actor", actor.Name), ("$utc", Utc())], transaction);
        Append(request.Admission.VerificationRecordId, new("STARTED", "ATTEMPT_STARTED", "WAIT_OR_RECOVER"), transaction);
        transaction.Commit();
    }
    internal void Append(Guid id, VerificationWorkflowEvent item, SqliteTransaction? transaction = null)
    {
        var bytes = WorkflowJson.Encode(item);
        Execute("INSERT INTO verification_events SELECT $id,coalesce(max(sequence),0)+1,$body,$hash,$utc FROM verification_events WHERE attempt_id=$id",
            [("$id", id.ToString("D")), ("$body", bytes), ("$hash", PackageInputManifest.Hash(bytes)), ("$utc", Utc())], transaction);
    }
    internal VerificationWorkflowEvent Latest(Guid id)
    {
        var bytes = Read("SELECT payload,sha256 FROM verification_events WHERE attempt_id=$id ORDER BY sequence DESC LIMIT 1", id)
            ?? throw new InvalidDataException("Attempt has no durable event.");
        return WorkflowJson.Read<VerificationWorkflowEvent>(bytes);
    }
    internal IReadOnlyList<VerificationWorkflowEvent> History(Guid id, int after, int limit)
    {
        if (after < 0 || limit is < 1 or > 100) { throw new ArgumentOutOfRangeException(nameof(limit)); }
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT payload,sha256 FROM verification_events WHERE attempt_id=$id AND sequence>$after ORDER BY sequence LIMIT $limit";
        command.Parameters.AddWithValue("$id", id.ToString("D")); command.Parameters.AddWithValue("$after", after); command.Parameters.AddWithValue("$limit", limit);
        using var reader = command.ExecuteReader(); var result = new List<VerificationWorkflowEvent>();
        while (reader.Read()) { result.Add(WorkflowJson.Read<VerificationWorkflowEvent>(Checked(reader))); }
        return result;
    }
    private byte[]? Read(string sql, Guid id)
    {
        using var command = connection.CreateCommand(); command.CommandText = sql; command.Parameters.AddWithValue("$id", id.ToString("D"));
        using var reader = command.ExecuteReader(); return reader.Read() ? Checked(reader) : null;
    }
    private static byte[] Checked(SqliteDataReader reader)
    {
        var length = reader.GetBytes(0, 0, null, 0, 0);
        PackageInputManifest.Need(length > 0 && length <= WorkflowJson.MaximumBytes, "Workflow stored payload exceeds envelope.");
        var bytes = (byte[])reader[0];
        PackageInputManifest.Need(PackageInputManifest.Hash(bytes) == reader.GetString(1), "Workflow payload hash mismatch."); return bytes;
    }
    private void Execute(string sql, params (string Key, object Value)[] values) => Execute(sql, values, null);
    private void Execute(string sql, (string Key, object Value)[] values, SqliteTransaction? transaction)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
        foreach (var (key, value) in values) { command.Parameters.AddWithValue(key, value); }
        command.ExecuteNonQuery();
    }
    private static string Utc() => DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture);
    public void Dispose() => connection.Dispose();
}
