using System.Text.Json;

namespace HumCapture.Coordinator.Repository;

/// <summary>A journal candidate for inspection; its stored state is not fresh verification.</summary>
/// <param name="TransactionId">Transaction identity and pagination cursor.</param>
/// <param name="State">Stored state.</param>
/// <param name="Revision">Stored journal revision.</param>
public sealed record RepositoryStartupCandidate(Guid TransactionId, string State, long Revision);

public sealed partial class RepositoryService
{
    /// <summary>Lists bounded journal candidates without changing repository state.</summary>
    public IReadOnlyList<RepositoryStartupCandidate> ListStartupTransactions(string rootPath, Guid? afterTransactionId = null, int limit = 100)
    {
        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }
        var opened = Open(rootPath);
        if (!opened.CanMutate)
        {
            throw new RepositoryException(RepositoryErrorCode.MutationNotAllowed, "Unsupported repositories permit descriptor inspection only.");
        }
        return RepositoryCatalog.ListStartupCandidates(
            RepositoryPathSafety.ResolveRelativePath(opened.RootPath, RepositoryConstants.CatalogRelativePath), afterTransactionId, limit);
    }

    private RepositoryStartupReconciliationSnapshot ReconcileLaterStartup(RepositoryOpenResult opened,
        string database, JournalCommitContext context, RepositoryStartupReconciliationRequest startup)
    {
        if (context.State == "COMMITTED")
        {
            if (startup.ResultTransitionId is not null || startup.ResultOperationId is not null)
            {
                throw new ArgumentException("Commit confirmation does not create a state transition.", nameof(startup));
            }
            var commit = RevalidateRetainedCommit(opened, database, context.TransactionId);
            var value = new JournalStartupReconciliation(startup.ReconciliationId, context.TransactionId,
                context.Revision, "COMMITTED", "COMMITTED", "CONFIRM_IDEMPOTENT_COMMIT", null, null,
                startup.ActorWindowsAccount, Utc(startup.StartedAt), Utc(startup.FinishedAt),
                "Retained finalization and current package evidence agree.",
                CommitObservations(context, commit.CommitRecordRelativePath));
            return RepositoryCatalog.RecordAutomaticReconciliation(database, value);
        }
        if (startup.ResultTransitionId is null || startup.ResultOperationId is null)
        {
            throw new ArgumentException("Startup finalization requires transition and operation identities.", nameof(startup));
        }
        var publication = RepositoryCatalog.ReadRetainedPublication(database, context.TransactionId);
        RepositoryCatalog.RequirePublishedCatalog(database, context, publication);
        RequireMovedPackage(opened.RootPath, context);
        var recordId = FindUnindexedCommit(opened.RootPath, context) ?? Guid.NewGuid();
        var request = new RepositoryFinalCommitRequest
        {
            Publication = publication, CommitRecordId = recordId, IndexEntryId = Guid.NewGuid(),
            ReconciliationId = startup.ReconciliationId, TransitionId = startup.ResultTransitionId.Value,
            OperationId = startup.ResultOperationId.Value, ActorWindowsAccount = startup.ActorWindowsAccount,
            StartedAt = startup.StartedAt, RecordedAt = startup.FinishedAt
        };
        CompleteCatalogedPackageCore(opened.RootPath, request, "STARTUP", true);
        return RepositoryCatalog.ReadStartupReconciliationReplay(database, startup)! with { WasAlreadyRecorded = false };
    }

    private RepositoryFinalCommitSnapshot RevalidateRetainedCommit(RepositoryOpenResult opened, string database, Guid transactionId)
    {
        var publication = RepositoryCatalog.ReadRetainedPublication(database, transactionId);
        var retained = RepositoryCatalog.ReadRetainedFinalRequest(database, publication);
        return CompleteCatalogedPackageCore(opened.RootPath, retained.Request, retained.Trigger, true);
    }

    private static Guid? FindUnindexedCommit(string root, JournalCommitContext context)
    {
        var relative = $"subjects/{Id(context.SubjectId)}/sessions/{Id(context.SessionId)}/records/commits";
        var directory = RepositoryPathSafety.ResolveRelativePath(root, relative);
        RepositoryPathSafety.RejectReparsePointsInExistingPath(directory);
        if (!Path.Exists(directory))
        {
            return null;
        }
        Guid? found = null;
        try
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                using var document = JsonDocument.Parse(ReadRecoveryRecord(path));
                var record = document.RootElement;
                if (record.GetProperty("transaction_id").GetString() != Id(context.TransactionId))
                {
                    continue;
                }
                var text = record.GetProperty("commit_record_id").GetString();
                if (!Guid.TryParseExact(text, "D", out var id) || id == Guid.Empty || text != Id(id)
                    || Path.GetFileName(path) != $"{text}.json" || found is not null)
                {
                    throw new RepositoryException(RepositoryErrorCode.CommitRecoveryRequired, "Ambiguous or noncanonical retained commit record; retain for operator investigation.");
                }
                found = id;
            }
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or IOException)
        {
            throw new RepositoryException(RepositoryErrorCode.CommitRecoveryRequired, "Retained commit evidence is unreadable; preserve it for investigation.", exception);
        }
        return found;
    }

    private static byte[] ReadRecoveryRecord(string path)
    {
        RepositoryPathSafety.RejectReparsePointsInExistingPath(path);
        RepositoryPathSafety.RequireSingleLinkFile(path);
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 65536)
        {
            throw new RepositoryException(RepositoryErrorCode.CommitRecoveryRequired, "Retained commit record exceeds the recovery inspection limit.");
        }
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }
}
