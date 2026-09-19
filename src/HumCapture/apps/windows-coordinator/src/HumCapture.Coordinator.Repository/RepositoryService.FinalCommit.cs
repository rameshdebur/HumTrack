using System.Security.Cryptography;
using System.Text.Json;

namespace HumCapture.Coordinator.Repository;

public sealed partial class RepositoryService
{
    /// <summary>Publishes and reconciles the immutable record before committing one cataloged package.</summary>
    public RepositoryFinalCommitSnapshot CompleteCatalogedPackage(string rootPath, RepositoryFinalCommitRequest request)
        => CompleteCatalogedPackageCore(rootPath, request, "PRE_RECEIPT", false);

    private RepositoryFinalCommitSnapshot CompleteCatalogedPackageCore(string rootPath,
        RepositoryFinalCommitRequest request, string trigger, bool preserveRecordFields)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Publication);
        var publication = request.Publication;
        if (Array.Exists(new[] { publication.TransactionId, publication.CatalogEntryId, publication.TransitionId,
            publication.OperationId, request.CommitRecordId, request.IndexEntryId, request.ReconciliationId,
            request.TransitionId, request.OperationId }, id => id == Guid.Empty)
            || request.RecordedAt < request.StartedAt)
        {
            throw new ArgumentException("Finalization requires nonempty identities and ordered audit times.", nameof(request));
        }
        ValidateWindowsAccount(request.ActorWindowsAccount);
        ValidateWindowsAccount(publication.ActorWindowsAccount);
        var opened = Open(rootPath);
        if (!opened.CanMutate)
        {
            throw new RepositoryException(RepositoryErrorCode.MutationNotAllowed, "Final commit requires a supported writable repository.");
        }
        lock (_commitLocks.GetOrAdd(opened.RootPath, static _ => new object()))
        {
            var database = RepositoryPathSafety.ResolveRelativePath(opened.RootPath, RepositoryConstants.CatalogRelativePath);
            var context = RepositoryCatalog.ReadCommitContext(database, publication.TransactionId, opened.Descriptor.RepositoryId);
            if (context.State is not ("CATALOGED" or "COMMITTED"))
            {
                throw new RepositoryException(RepositoryErrorCode.CommitRecoveryRequired, "Finalization requires catalog publication first.");
            }
            var replay = context.State == "COMMITTED";
            var catalogRevision = context.Revision - (replay ? 1 : 0);
            RepositoryCatalog.RequirePublicationHistory(database, context, publication, catalogRevision);
            RepositoryCatalog.RequirePublishedCatalog(database, context, publication, forFinalization: true);
            RequireMovedPackage(opened.RootPath, context);
            var relative = $"subjects/{Id(context.SubjectId)}/sessions/{Id(context.SessionId)}/records/commits/{Id(request.CommitRecordId)}.json";
            var path = RepositoryPathSafety.ResolveRelativePath(opened.RootPath, relative);
            RepositoryPathSafety.RejectReparsePointsInExistingPath(path);
            var recordRequest = request;
            if (preserveRecordFields && File.Exists(path))
            {
                using var retained = JsonDocument.Parse(ReadRecoveryRecord(path));
                recordRequest = request with
                {
                    ActorWindowsAccount = retained.RootElement.GetProperty("committed_by_windows_account").GetString()!,
                    RecordedAt = retained.RootElement.GetProperty("committed_utc").GetDateTimeOffset()
                };
                ValidateWindowsAccount(recordRequest.ActorWindowsAccount);
            }
            var bytes = CreateCommitRecord(context, recordRequest);
            var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
            var value = new JournalStartupReconciliation(request.ReconciliationId, context.TransactionId,
                catalogRevision, "CATALOGED", "COMMITTED", "FINALIZE_COMMIT", request.TransitionId,
                request.OperationId, request.ActorWindowsAccount, Utc(request.StartedAt), Utc(request.RecordedAt),
                "Exact package, catalog, verification and immutable commit evidence agree.",
                CommitObservations(context, relative), trigger);
            if (!replay)
            {
                RepositoryCatalog.RequireUncommittedEvidence(database, context.TransactionId, context.PackageId);
                PublishImmutableRecord(path, bytes);
            }
            if (!File.Exists(path))
            {
                throw new RepositoryException(RepositoryErrorCode.CommitRecoveryRequired, "Committed record is missing; preserve the package for investigation.");
            }
            RepositoryPathSafety.RequireSingleLinkFile(path);
            if (!File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes))
            {
                throw new RepositoryException(RepositoryErrorCode.ImmutableRecordConflict, "Commit record differs from the exact requested evidence.");
            }
            RequireMovedPackage(opened.RootPath, context);
            if (!replay)
            {
                RepositoryCatalog.CommitFinalEvidence(database, context, request, relative, hash, value);
            }
            RepositoryCatalog.RequireFinalEvidence(database, context, request, relative, hash, value);
            return new(Id(context.TransactionId), catalogRevision + 1, relative, hash, replay);
        }
    }

    private static byte[] CreateCommitRecord(JournalCommitContext c, RepositoryFinalCommitRequest r) =>
        JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["schema_version"] = "1.0.0", ["commit_record_id"] = Id(r.CommitRecordId), ["record_revision"] = 1,
            ["repository_id"] = c.RepositoryId, ["transaction_id"] = Id(c.TransactionId),
            ["subject_id"] = Id(c.SubjectId), ["session_id"] = Id(c.SessionId), ["package_id"] = Id(c.PackageId),
            ["package_content_sha256"] = c.PackageContentSha256, ["artifact_set_sha256"] = c.ArtifactSetSha256,
            ["package_byte_length"] = c.PackageByteLength, ["artifact_count"] = c.ArtifactCount,
            ["package_relative_path"] = c.DestinationRelativePath, ["verification_record_id"] = Id(c.VerificationRecordId),
            ["verification_record_content_sha256"] = c.VerificationRecordContentSha256,
            ["catalog_entry_id"] = Id(r.Publication.CatalogEntryId), ["catalog_revision"] = 1,
            ["committed_by_windows_account"] = r.ActorWindowsAccount, ["committed_utc"] = Utc(r.RecordedAt)
        });

    private static RepositoryAuthorityObservation[] CommitObservations(JournalCommitContext c, string commitPath)
    {
        RepositoryAuthorityObservation Match(string authority, string path) =>
            new(authority, "MATCH", Id(c.PackageId), Id(c.PackageId), c.PackageContentSha256,
                c.PackageContentSha256, c.PackageByteLength, c.PackageByteLength, [path]);
        return
        [
            Match("CATALOG_LINKAGE", RepositoryConstants.CatalogRelativePath),
            Match("COMMIT_RECORD", commitPath),
            Match("DESTINATION_PACKAGE", c.DestinationRelativePath),
            Match("JOURNAL", RepositoryConstants.CatalogRelativePath),
            new("STAGING_PACKAGE", "ABSENT", Id(c.PackageId), null, c.PackageContentSha256, null,
                c.PackageByteLength, null, [c.StagingRelativePath]),
            Match("VERIFICATION_RECORD", c.VerificationRecordRelativePath)
        ];
    }
}
