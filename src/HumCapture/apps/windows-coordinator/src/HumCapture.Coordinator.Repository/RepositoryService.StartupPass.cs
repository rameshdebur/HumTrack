using System.Security.Principal;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace HumCapture.Coordinator.Repository;

/// <summary>Progress of one bounded startup pass, not capture or session completion.</summary>
public enum RepositoryStartupPassStatus
{
    /// <summary>No further candidates were present at the final discovery check.</summary>
    Completed,
    /// <summary>The caller must continue from the returned cursor.</summary>
    MoreWork,
    /// <summary>Stopped between transactions; completed actions remain durable.</summary>
    Cancelled,
    /// <summary>Compatibility permits descriptor inspection only.</summary>
    ReadOnlyInspection
}

/// <summary>One startup result with no raw exception, subject data or absolute paths.</summary>
/// <param name="TransactionId">Inspected transaction.</param>
/// <param name="Reconciliation">Successful durable reconciliation, when available.</param>
/// <param name="ErrorCode">Controlled failure, when reconciliation did not succeed.</param>
/// <param name="NextAction">Safe next step; never a cleanup authorization.</param>
public sealed record RepositoryStartupItem(Guid TransactionId,
    RepositoryStartupReconciliationSnapshot? Reconciliation,
    RepositoryErrorCode? ErrorCode, string NextAction)
{
    /// <summary>Gets the verified normal move result when processing staged work.</summary>
    public RepositoryCommitMoveSnapshot? NormalMove { get; init; }
    /// <summary>Gets whether processing skipped this item without verifying its state.</summary>
    public bool WasSkipped { get; init; }
}

/// <summary>Bounded startup progress; Completed does not imply every item succeeded.</summary>
/// <param name="Repository">Compatibility result.</param>
/// <param name="Status">Pass progress independent of per-item outcome.</param>
/// <param name="LastProcessedTransactionId">Continuation cursor, including failed inspected items.</param>
/// <param name="Items">Results from this invocation only.</param>
public sealed record RepositoryStartupPass(RepositoryOpenResult Repository,
    RepositoryStartupPassStatus Status, Guid? LastProcessedTransactionId,
    IReadOnlyList<RepositoryStartupItem> Items)
{
    /// <summary>Gets whether inspected items require operator investigation.</summary>
    public bool RequiresOperatorAttention => Items.Any(item => item.ErrorCode is not null);
}

public sealed partial class RepositoryService
{
    /// <summary>
    /// Opens the repository and performs one bounded startup recovery pass as the
    /// current Windows identity. Invoke before capture work; not an executable host.
    /// Cancellation takes effect between transactions without interrupting finalization.
    /// </summary>
    public RepositoryStartupPass RunStartupPass(string rootPath, Guid? afterTransactionId = null,
        int maxTransactions = 100, CancellationToken cancellationToken = default)
        => RunBoundedPass(rootPath, afterTransactionId, maxTransactions, cancellationToken, false);

    /// <summary>
    /// Explicitly processes staged candidates through normal durable movement only.
    /// Non-staged candidates are skipped, not reconciled or freshly verified.
    /// </summary>
    public RepositoryStartupPass ProcessStagedPass(string rootPath, Guid? afterTransactionId = null,
        int maxTransactions = 100, CancellationToken cancellationToken = default)
        => RunBoundedPass(rootPath, afterTransactionId, maxTransactions, cancellationToken, true);

    private RepositoryStartupPass RunBoundedPass(string rootPath, Guid? afterTransactionId,
        int maxTransactions, CancellationToken cancellationToken, bool processStaged)
    {
        if (maxTransactions is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTransactions));
        }
        var opened = Open(rootPath);
        if (!opened.CanMutate)
        {
            return new(opened, RepositoryStartupPassStatus.ReadOnlyInspection, afterTransactionId, []);
        }
        var items = new List<RepositoryStartupItem>();
        var cursor = afterTransactionId;
        lock (_commitLocks.GetOrAdd(opened.RootPath, static _ => new object()))
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return new(opened, RepositoryStartupPassStatus.Cancelled, cursor, items.AsReadOnly());
            }
            using var identity = WindowsIdentity.GetCurrent();
            var actor = identity.Name;
            ValidateWindowsAccount(actor);
            var candidates = ListStartupTransactions(opened.RootPath, cursor, maxTransactions);
            foreach (var candidate in candidates)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return new(opened, RepositoryStartupPassStatus.Cancelled, cursor, items.AsReadOnly());
                }
                items.Add(processStaged
                    ? ProcessStagedCandidate(opened.RootPath, candidate, actor)
                    : ReconcileStartupCandidate(opened.RootPath, candidate, actor));
                cursor = candidate.TransactionId;
            }
            var status = RepositoryStartupPassStatus.Cancelled;
            if (!cancellationToken.IsCancellationRequested)
            {
                status = ListStartupTransactions(opened.RootPath, cursor, 1).Count == 0
                    ? RepositoryStartupPassStatus.Completed : RepositoryStartupPassStatus.MoreWork;
            }
            return new(opened, status, cursor, items.AsReadOnly());
        }
    }

    private RepositoryStartupItem ReconcileStartupCandidate(string root,
        RepositoryStartupCandidate candidate, string actor)
    {
        try
        {
            var changesState = candidate.State is "COMMITTING" or "MOVED" or "CATALOGED";
            var now = DateTimeOffset.UtcNow;
            var result = ReconcileStartupTransaction(root, new()
            {
                TransactionId = candidate.TransactionId, ReconciliationId = Guid.NewGuid(),
                ResultTransitionId = changesState ? Guid.NewGuid() : null,
                ResultOperationId = changesState ? Guid.NewGuid() : null,
                ActorWindowsAccount = actor, StartedAt = now, FinishedAt = now
            });
            return new(candidate.TransactionId, result, null,
                result.ResultState == "COMMITTED" ? "AWAIT_FRESH_RECEIPT_AUTHORIZATION" : "CONTINUE_PACKAGE_WORKFLOW");
        }
        catch (RepositoryException exception)
        {
            return new(candidate.TransactionId, null, exception.Code, "RETAIN_AND_INVESTIGATE_THEN_RETRY");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or JsonException or KeyNotFoundException or FormatException or InvalidOperationException or SqliteException)
        {
            return new(candidate.TransactionId, null, RepositoryErrorCode.CommitRecoveryRequired,
                "RETAIN_AND_INVESTIGATE_THEN_RETRY");
        }
    }
}
