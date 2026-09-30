using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace HumCapture.Coordinator.Desktop;

internal sealed record Subject(string Code, string Name, string BirthDate, string Sex, decimal HeightCm);
internal sealed record Session(Guid Id, Guid SubjectId, Subject Subject, string Protocol, string Operator);
internal sealed record Attempt(Guid Id, Guid SessionId, string[] Sources, string Scenario, string State,
    DateTimeOffset? Started, DateTimeOffset? Ended, string Preview, string Reason, string? FailedSource = null,
    string Processing = "NotRun", int ProcessingAttempts = 0);
internal sealed record Event(long Sequence, Guid AttemptId, string State, string Reason);
internal sealed record Snapshot(Session[] Sessions, Attempt[] Attempts, Event[] Events, int Page);
internal sealed record Request(string Command, Guid SessionId = default, Guid AttemptId = default,
    Subject? Subject = null, string Protocol = "single-v1", string[]? Sources = null,
    string Scenario = "normal", int Page = 0, string SchemaVersion = "1.0.0");
internal sealed record Response(bool Success, string Message, Snapshot? Snapshot = null, string SchemaVersion = "1.0.0", Guid? Cursor = null);

internal static class Workspace
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    internal static readonly string[] Scenarios = ["normal", "source-unavailable", "unsupported-profile", "preview-loss", "source-loss", "finalization-failure", "verification-failure", "storage-failure"];
    internal static bool Active(Attempt? attempt) => attempt?.State is "Recording" or "Finalizing";

    internal static Response Execute(string root, Request request)
    {
        if (request.SchemaVersion != "1.0.0" || request.Page < 0 || request.Page > 100000)
        {
            throw new InvalidDataException("Unsupported request version or page.");
        }

        ValidateRoot(root);
        Directory.CreateDirectory(root);
        using var guard = new FileStream(Path.Combine(root, "workspace.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        string db = Path.Combine(root, "synthetic-workspace.sqlite3");
        bool exists = File.Exists(db);
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = db, Pooling = false }.ToString());
        connection.Open();
        Sql(connection, "PRAGMA busy_timeout=3000; PRAGMA synchronous=FULL;");
        if (!exists)
        {
            Sql(connection, """
                BEGIN IMMEDIATE;
                PRAGMA application_id=1212372819; PRAGMA user_version=1;
                CREATE TABLE sessions(id TEXT PRIMARY KEY, body TEXT NOT NULL);
                CREATE TABLE attempts(id TEXT PRIMARY KEY, session_id TEXT NOT NULL, body TEXT NOT NULL);
                CREATE TABLE events(sequence INTEGER PRIMARY KEY AUTOINCREMENT, attempt_id TEXT NOT NULL, state TEXT NOT NULL, reason TEXT NOT NULL);
                COMMIT;
                """);
        }
        if (Scalar(connection, "PRAGMA application_id") != 1212372819 || Scalar(connection, "PRAGMA user_version") != 1)
        {
            throw new InvalidDataException("Unsupported workspace. No migration performed.");
        }

        using var transaction = connection.BeginTransaction();
        Dispatch(connection, request);
        var sessions = Read<Session>(connection, "SELECT body FROM sessions ORDER BY rowid DESC LIMIT 20 OFFSET $offset", ("$offset", request.Page * 20));
        var attempts = Read<Attempt>(connection, "SELECT body FROM attempts WHERE session_id=$id ORDER BY rowid DESC LIMIT 20", ("$id", request.SessionId.ToString()));
        using var eventCommand = connection.CreateCommand();
        eventCommand.CommandText = "SELECT sequence,attempt_id,state,reason FROM events WHERE attempt_id=$id ORDER BY sequence DESC LIMIT 50";
        eventCommand.Parameters.AddWithValue("$id", request.AttemptId.ToString());
        var events = new List<Event>();
        using (var reader = eventCommand.ExecuteReader())
        {
            while (reader.Read())
            {
                events.Add(new Event(reader.GetInt64(0), Guid.Parse(reader.GetString(1)), reader.GetString(2), reader.GetString(3)));
            }
        }

        transaction.Commit();
        return new Response(true, "Synthetic workspace updated. No physical capture or package commit.", new Snapshot(sessions, attempts, events.ToArray(), request.Page));
    }

    private static void Dispatch(SqliteConnection db, Request r)
    {
        if (r.Command == "list")
        {
            return;
        }

        if (r.Command == "reconcile")
        {
            foreach (var item in Read<Attempt>(db, "SELECT body FROM attempts").Where(Active))
            {
                Save(db, item with { State = "Interrupted", Ended = DateTimeOffset.UtcNow, Reason = "Application restarted; synthetic activity interrupted. Start a new attempt." });
            }

            return;
        }
        if (r.Command == "create-session")
        {
            if (r.SessionId == Guid.Empty || r.Subject is null || r.Protocol is not ("single-v1" or "dual-v1"))
            {
                throw new InvalidDataException("Select a subject and an existing simulation protocol.");
            }

            ValidateSubject(r.Subject);
            var existing = Read<Session>(db, "SELECT body FROM sessions WHERE id=$id", ("$id", r.SessionId.ToString())).SingleOrDefault();
            if (existing is not null)
            {
                if (existing.Subject != r.Subject || existing.Protocol != r.Protocol)
                {
                    throw new InvalidDataException("Session identity conflict.");
                }

                return;
            }
            var subject = Array.Find(Read<Session>(db, "SELECT body FROM sessions"), s => s.Subject.Code == r.Subject.Code);
            if (subject is not null && subject.Subject != r.Subject)
            {
                throw new InvalidDataException("Subject code already belongs to different demographics. Use the existing subject or a new code.");
            }

            var session = new Session(r.SessionId, subject?.SubjectId ?? Guid.NewGuid(), r.Subject, r.Protocol, Environment.UserName);
            Sql(db, "INSERT INTO sessions(id,body) VALUES($id,$body)", ("$id", session.Id.ToString()), ("$body", JsonSerializer.Serialize(session, Json)));
            return;
        }
        var owner = Read<Session>(db, "SELECT body FROM sessions WHERE id=$id", ("$id", r.SessionId.ToString())).SingleOrDefault()
            ?? throw new InvalidDataException("Select a saved session.");
        if (r.Command == "prepare")
        {
            if (r.AttemptId == Guid.Empty || r.Sources is null || r.Sources.Length != (owner.Protocol == "single-v1" ? 1 : 2)
                || r.Sources.Distinct(StringComparer.Ordinal).Count() != r.Sources.Length
                || Array.Exists(r.Sources, s => s is not ("SIM-A" or "SIM-B")) || !Scenarios.Contains(r.Scenario))
            {
                throw new InvalidDataException("Assign one distinct simulated source to each required role.");
            }

            if (r.Scenario is "source-unavailable" or "unsupported-profile")
            {
                throw new InvalidDataException("Not ready: simulated source unavailable or incompatible. Choose a compatible test scenario.");
            }

            var existing = Find(db, r.AttemptId);
            if (existing is not null)
            {
                if (existing.SessionId != r.SessionId || !existing.Sources.SequenceEqual(r.Sources) || existing.Scenario != r.Scenario)
                {
                    throw new InvalidDataException("Attempt assignment is immutable. Create a new attempt.");
                }

                return;
            }
            if (Array.Exists(Read<Attempt>(db, "SELECT body FROM attempts"), Active))
            {
                throw new InvalidDataException("An attempt is active. Stop it before preparing another.");
            }

            Save(db, new Attempt(r.AttemptId, r.SessionId, r.Sources, r.Scenario, "Ready", null, null, "Synthetic preview available", "Configuration checks passed; no measured camera evidence."));
            return;
        }
        var attempt = Find(db, r.AttemptId) ?? throw new InvalidDataException("Select a saved attempt.");
        if (attempt.SessionId != owner.Id)
        {
            throw new InvalidDataException("Attempt belongs to another session.");
        }

        switch (r.Command)
        {
            case "simulate-processing":
                Require(attempt.State == "Finalized", "Only a finalized synthetic attempt can exercise processing.");
                if (attempt.Processing == "SimulatedOnly") { return; }
                string processing = "SimulatedOnly";
                if (attempt.ProcessingAttempts == 0 && attempt.Scenario == "verification-failure") { processing = "VerificationFailed"; }
                if (attempt.ProcessingAttempts == 0 && attempt.Scenario == "storage-failure") { processing = "StorageFailed"; }
                Save(db, attempt with { Processing = processing, ProcessingAttempts = attempt.ProcessingAttempts + 1,
                    Reason = "SIMULATED processing: " + processing + ". No media verified or committed. Retry keeps this attempt; new recording requires a new attempt." }); break;
            case "start":
                if (attempt.State == "Recording")
                {
                    return;
                }

                Require(attempt.State == "Ready", "Only a ready attempt can start.");
                if (Array.Exists(Read<Attempt>(db, "SELECT body FROM attempts"), Active))
                {
                    throw new InvalidDataException("Another attempt is active.");
                }

                Save(db, attempt with { State = "Recording", Started = DateTimeOffset.UtcNow, Reason = "Simulated recording; no video is being acquired." }); break;
            case "preview-loss":
                Require(attempt.State == "Recording", "Preview scenario requires recording.");
                Save(db, attempt with { Preview = "Preview unavailable; simulated recording continues", Reason = "Preview failure is independent of master state." }); break;
            case "source-loss":
                Require(attempt.State == "Recording", "Source scenario requires recording.");
                if (attempt.FailedSource is not null) { return; }
                bool survivor = attempt.Sources.Length > 1;
                Save(db, attempt with { State = survivor ? "Recording" : "Interrupted", Ended = survivor ? null : DateTimeOffset.UtcNow,
                    FailedSource = attempt.Sources[0], Reason = survivor
                        ? "First synthetic source lost; surviving source activity continues until Stop. Attempt cannot complete."
                        : "Synthetic source lost. Attempt retained; start a new attempt. No physical-source behaviour qualified." }); break;
            case "stop":
                if (attempt.State is "Finalizing" or "Finalized" or "FinalizationFailed" or "Interrupted")
                {
                    return;
                }

                Require(attempt.State == "Recording", "Only a recording attempt can stop.");
                Save(db, attempt with { State = "Finalizing", Ended = DateTimeOffset.UtcNow, Reason = "Stopped; synthetic finalization pending. Not completed." }); break;
            case "finalize":
                if (attempt.State is "Finalized" or "FinalizationFailed")
                {
                    return;
                }

                Require(attempt.State == "Finalizing", "Stop before finalization.");
                if (attempt.FailedSource is not null)
                {
                    Save(db, attempt with { State = "Interrupted", Reason = "Required source lost; surviving synthetic activity finalized. Retained incomplete; prepare a new attempt." }); break;
                }
                bool failed = attempt.Scenario == "finalization-failure";
                Save(db, attempt with { State = failed ? "FinalizationFailed" : "Finalized", Reason = failed
                    ? "Synthetic finalization failed. Retain attempt and record a new attempt. Real repair is unavailable."
                    : "Synthetic finalization finished. Verification NOT ASSESSED; storage NOT COMMITTED; workflow NOT COMPLETE." }); break;
            default: throw new InvalidDataException("Unknown workspace command.");
        }
    }

    internal static void ValidateSubject(Subject s)
    {
        Require(!string.IsNullOrWhiteSpace(s.Name) && s.Name.Length <= 120 && !string.IsNullOrWhiteSpace(s.Code) && s.Code.Length <= 40, "Synthetic subject name and code are required (120/40 character limits).");
        Require(DateOnly.TryParseExact(s.BirthDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var birth)
            && birth <= DateOnly.FromDateTime(DateTime.Today), "Enter a valid synthetic date of birth as YYYY-MM-DD.");
        Require(s.Sex is "Female" or "Male" or "Other" or "Not recorded", "Select a controlled sex value.");
        Require(s.HeightCm > 0 && s.HeightCm <= 300, "Height must be greater than 0 and at most 300 cm.");
    }
    internal static void ValidateRoot(string root)
    {
        Require(Path.IsPathFullyQualified(root) && !root.StartsWith(@"\\", StringComparison.Ordinal), "An absolute local simulation directory is required.");
        for (DirectoryInfo? directory = new(root); directory is not null; directory = directory.Parent)
        {
            Require(!directory.Exists || (directory.Attributes & FileAttributes.ReparsePoint) == 0, "Linked workspace directories are not supported.");
            Require(!File.Exists(Path.Combine(directory.FullName, "repository.json")), "A simulation workspace cannot be inside a capture repository.");
        }
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var entry in new DirectoryInfo(root).EnumerateFileSystemInfos())
        {
            Require((entry.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) == 0
                && entry.Name is "workspace.lock" or "synthetic-workspace.sqlite3" or "synthetic-workspace.sqlite3-journal",
                "Choose an empty directory or an existing synthetic workspace, not a real repository.");
        }
    }
    private static void Require(bool condition, string message) { if (!condition)
        {
            throw new InvalidDataException(message);
        }
    }
    private static Attempt? Find(SqliteConnection db, Guid id) => Read<Attempt>(db, "SELECT body FROM attempts WHERE id=$id", ("$id", id.ToString())).SingleOrDefault();
    private static void Save(SqliteConnection db, Attempt a)
    {
        Sql(db, "INSERT INTO attempts(id,session_id,body) VALUES($id,$session,$body) ON CONFLICT(id) DO UPDATE SET body=excluded.body",
            ("$id", a.Id.ToString()), ("$session", a.SessionId.ToString()), ("$body", JsonSerializer.Serialize(a, Json)));
        Sql(db, "INSERT INTO events(attempt_id,state,reason) VALUES($id,$state,$reason)", ("$id", a.Id.ToString()), ("$state", a.State), ("$reason", a.Reason));
    }
    private static T[] Read<T>(SqliteConnection db, string sql, params (string Name, object Value)[] values)
    {
        using var cmd = Command(db, sql, values); using var reader = cmd.ExecuteReader(); var result = new List<T>();
        while (reader.Read())
        {
            result.Add(JsonSerializer.Deserialize<T>(reader.GetString(0), Json) ?? throw new InvalidDataException("Invalid saved workspace."));
        }

        return result.ToArray();
    }
    private static long Scalar(SqliteConnection db, string sql) { using var cmd = Command(db, sql); return Convert.ToInt64(cmd.ExecuteScalar(), CultureInfo.InvariantCulture); }
    private static void Sql(SqliteConnection db, string sql, params (string Name, object Value)[] values) { using var cmd = Command(db, sql, values); cmd.ExecuteNonQuery(); }
    private static SqliteCommand Command(SqliteConnection db, string sql, params (string Name, object Value)[] values)
    {
        var cmd = db.CreateCommand(); cmd.CommandText = sql;
        foreach (var item in values)
        {
            cmd.Parameters.AddWithValue(item.Name, item.Value);
        }

        return cmd;
    }
}
