namespace HumCapture.Coordinator.Repository;

public sealed partial class RepositoryService
{
    private RepositoryStartupItem ProcessStagedCandidate(string root,
        RepositoryStartupCandidate candidate, string actor)
    {
        if (candidate.State != "STAGED_VERIFIED")
        {
            return new(candidate.TransactionId, null, null, "REVIEW_STATE_OR_RUN_STARTUP") { WasSkipped = true };
        }
        try
        {
            var now = DateTimeOffset.UtcNow;
            var moved = MoveStagedVerifiedPackage(root, new()
            {
                TransactionId = candidate.TransactionId,
                CommittingTransitionId = Guid.NewGuid(), CommittingOperationId = Guid.NewGuid(),
                MovedTransitionId = Guid.NewGuid(), MovedOperationId = Guid.NewGuid(),
                ActorWindowsAccount = actor, CommittingRecordedAt = now, MovedRecordedAt = now
            });
            return new(candidate.TransactionId, null, null, "RUN_STARTUP_TO_CONTINUE_CATALOGING") { NormalMove = moved };
        }
        catch (RepositoryException exception)
        {
            return new(candidate.TransactionId, null, exception.Code, "RETAIN_AND_RUN_STARTUP_RECOVERY");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(candidate.TransactionId, null, RepositoryErrorCode.CommitRecoveryRequired,
                "RETAIN_AND_RUN_STARTUP_RECOVERY");
        }
    }
}
