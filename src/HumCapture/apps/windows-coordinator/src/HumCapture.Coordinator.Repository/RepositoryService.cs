using System.Globalization;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Collections.Concurrent;

namespace HumCapture.Coordinator.Repository;

/// <summary>Initializes and compatibility-checks the Coordinator-owned HumCapture repository.</summary>
public sealed partial class RepositoryService
{
    private const string InitializationLockName = ".humcapture-initialize.lock";
    private static readonly ConcurrentDictionary<string, object> _commitLocks =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes an empty absolute data root and publishes its descriptor after catalog durability checks.</summary>
    /// <param name="rootPath">The absolute Coordinator-owned data root.</param>
    /// <param name="creatingWindowsAccount">The signed-in Windows account identity.</param>
    /// <param name="createdAt">An optional injected UTC creation time for deterministic verification.</param>
    /// <param name="repositoryId">An optional injected repository UUID for deterministic verification.</param>
    /// <returns>A verified mutation-capable open result.</returns>
    public RepositoryOpenResult Initialize(
        string rootPath,
        string creatingWindowsAccount,
        DateTimeOffset? createdAt = null,
        Guid? repositoryId = null)
    {
        var root = RepositoryPathSafety.NormalizeAbsoluteRoot(rootPath);
        ValidateWindowsAccount(creatingWindowsAccount);
        RepositoryPathSafety.RejectReparsePointsInExistingPath(root);
        Directory.CreateDirectory(root);
        RepositoryPathSafety.RejectReparsePointsInExistingPath(root);

        var descriptorPath = Path.Combine(root, RepositoryConstants.DescriptorFileName);
        if (File.Exists(descriptorPath))
        {
            throw new RepositoryException(RepositoryErrorCode.AlreadyInitialized, "Repository data root is already initialized.");
        }

        var lockPath = Path.Combine(root, InitializationLockName);
        FileStream initializationLock;
        try
        {
            initializationLock = new FileStream(lockPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        }
        catch (IOException exception)
        {
            throw new RepositoryException(RepositoryErrorCode.InitializationInProgress, "Repository initialization is already in progress or its lock requires inspection.", exception);
        }

        try
        {
            using (initializationLock)
            {
                var unexpected = Directory.EnumerateFileSystemEntries(root)
                    .Where(path => !string.Equals(path, lockPath, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                if (unexpected.Length != 0)
                {
                    throw new RepositoryException(RepositoryErrorCode.RootNotEmpty, "Repository data root must be empty before initialization.");
                }

                var timestamp = (createdAt ?? DateTimeOffset.UtcNow).ToUniversalTime();
                var id = repositoryId ?? Guid.NewGuid();
                if (id == Guid.Empty)
                {
                    throw new ArgumentException("Repository ID must not be empty.", nameof(repositoryId));
                }

                var descriptor = new RepositoryDescriptor
                {
                    SchemaVersion = RepositoryConstants.DescriptorSchemaVersion,
                    RepositoryId = id.ToString("D"),
                    InterfaceVersion = RepositoryConstants.InterfaceVersion,
                    NamespaceVersion = RepositoryConstants.NamespaceVersion,
                    CatalogSchemaVersion = RepositoryConstants.CatalogSchemaVersion,
                    CreatedUtc = timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture),
                    CreatedByWindowsAccount = creatingWindowsAccount,
                    CatalogRelativePath = RepositoryConstants.CatalogRelativePath,
                    RequiredFeatures = RepositoryConstants.RequiredFeatures
                };

                Directory.CreateDirectory(Path.Combine(root, "catalog"));
                Directory.CreateDirectory(Path.Combine(root, "staging"));
                Directory.CreateDirectory(Path.Combine(root, "quarantine"));
                Directory.CreateDirectory(Path.Combine(root, "subjects"));

                var catalogPath = Path.Combine(root, "catalog", "humcapture.sqlite3");
                RepositoryCatalog.Create(catalogPath, descriptor);
                PublishDescriptor(descriptorPath, descriptor);
            }
        }
        finally
        {
            try
            {
                File.Delete(lockPath);
            }
            catch (IOException)
            {
                // A retained lock is safer than hiding an uncertain initialization boundary.
            }
        }

        return Open(root);
    }

    /// <summary>Opens a repository for mutation only when every supported compatibility and catalog check passes.</summary>
    /// <param name="rootPath">The absolute Coordinator-owned data root.</param>
    /// <returns>A mutation-capable or explicitly read-only compatibility result.</returns>
    public RepositoryOpenResult Open(string rootPath)
    {
        var root = RepositoryPathSafety.NormalizeAbsoluteRoot(rootPath);
        RepositoryPathSafety.RejectReparsePointsInExistingPath(root);
        var descriptorPath = Path.Combine(root, RepositoryConstants.DescriptorFileName);
        if (!File.Exists(descriptorPath))
        {
            throw new RepositoryException(RepositoryErrorCode.DescriptorMissing, "Repository descriptor is missing.");
        }

        RepositoryPathSafety.RejectReparsePointsInExistingPath(descriptorPath);
        RepositoryPathSafety.RequireSingleLinkFile(descriptorPath);
        var descriptor = RepositoryDescriptorCodec.Parse(File.ReadAllBytes(descriptorPath), out var properties);
        var compatibility = ClassifyCompatibility(descriptor, properties);
        if (!compatibility.CanMutate)
        {
            return compatibility with { RootPath = root };
        }

        var catalogPath = Path.Combine(root, "catalog", "humcapture.sqlite3");
        RepositoryPathSafety.RejectReparsePointsInExistingPath(catalogPath);
        if (!File.Exists(catalogPath))
        {
            throw new RepositoryException(RepositoryErrorCode.CatalogMissing, "Repository catalog is missing.");
        }

        RepositoryPathSafety.RequireSingleLinkFile(catalogPath);
        RepositoryCatalog.Verify(catalogPath, descriptor);
        return compatibility with { RootPath = root };
    }

    /// <summary>
    /// Admits an already-collected and fully verified package into the durable repository
    /// journal at STAGED_VERIFIED. This method does not collect, move, commit, or delete package data.
    /// </summary>
    public RepositoryTransactionSnapshot RegisterStagedVerifiedPackage(
        string rootPath,
        StagedVerifiedPackageRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ValidateRegistration(registration);
        var opened = Open(rootPath);
        if (!opened.CanMutate)
        {
            throw new RepositoryException(RepositoryErrorCode.MutationNotAllowed, "Repository compatibility permits inspection only; journal mutation is disabled.");
        }

        var root = opened.RootPath;
        var stagingRelativePath = $"staging/{Id(registration.CollectionAttemptId)}/{Id(registration.PackageId)}";
        var destinationRelativePath = $"subjects/{Id(registration.SubjectId)}/sessions/{Id(registration.SessionId)}/packages/{Id(registration.PackageId)}";
        var verificationRelativePath = $"subjects/{Id(registration.SubjectId)}/sessions/{Id(registration.SessionId)}/records/verifications/{Id(registration.VerificationRecordId)}.json";
        var stagingPath = RepositoryPathSafety.ResolveRelativePath(root, stagingRelativePath);
        var destinationPath = RepositoryPathSafety.ResolveRelativePath(root, destinationRelativePath);
        if (Path.Exists(destinationPath))
        {
            throw new RepositoryException(RepositoryErrorCode.JournalConflict, "Final package destination already contains material; staged admission requires reconciliation.");
        }

        var evidence = StagedPackageEvidenceValidator.Validate(stagingPath, registration);
        var verificationPath = RepositoryPathSafety.ResolveRelativePath(root, verificationRelativePath);
        PublishImmutableRecord(verificationPath, registration.VerificationRecordUtf8.Span);

        var timestamp = (registration.RecordedAt ?? DateTimeOffset.UtcNow).ToUniversalTime()
            .ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
        var admission = new JournalAdmission(
            opened.Descriptor.RepositoryId,
            registration,
            stagingRelativePath,
            destinationRelativePath,
            verificationRelativePath,
            evidence.VerificationRecordRevision,
            timestamp);
        var catalogPath = RepositoryPathSafety.ResolveRelativePath(root, RepositoryConstants.CatalogRelativePath);
        return RepositoryCatalog.RegisterStagedVerified(catalogPath, admission);
    }

    /// <summary>
    /// Durably records commit intent, moves the exact package through a same-volume
    /// write-through rename, verifies the result, and advances the journal only to MOVED.
    /// This method does not catalog or commit the package and cannot authorize a receipt.
    /// </summary>
    public RepositoryCommitMoveSnapshot MoveStagedVerifiedPackage(
        string rootPath,
        RepositoryCommitMoveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCommitMoveRequest(request);
        var opened = Open(rootPath);
        if (!opened.CanMutate)
        {
            throw new RepositoryException(RepositoryErrorCode.MutationNotAllowed, "Repository compatibility permits inspection only; package movement is disabled.");
        }

        var gate = _commitLocks.GetOrAdd(opened.RootPath, static _ => new object());
        lock (gate)
        {
            return MoveStagedVerifiedPackage(opened, request);
        }
    }

    /// <summary>
    /// Observes all six repository authorities and records one exact automatic
    /// startup reconciliation within the implemented early and committed boundaries.
    /// </summary>
    public RepositoryStartupReconciliationSnapshot ReconcileStartupTransaction(
        string rootPath,
        RepositoryStartupReconciliationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateStartupReconciliationRequest(request);
        var opened = Open(rootPath);
        if (!opened.CanMutate)
        {
            throw new RepositoryException(RepositoryErrorCode.MutationNotAllowed, "Repository compatibility permits inspection only; reconciliation is disabled.");
        }

        var gate = _commitLocks.GetOrAdd(opened.RootPath, static _ => new object());
        lock (gate)
        {
            var catalogPath = RepositoryPathSafety.ResolveRelativePath(opened.RootPath, RepositoryConstants.CatalogRelativePath);
            var replay = RepositoryCatalog.ReadStartupReconciliationReplay(catalogPath, request);
            if (replay is not null)
            {
                if (replay.ResultState == "COMMITTED")
                {
                    RevalidateRetainedCommit(opened, catalogPath, request.TransactionId);
                }
                return replay;
            }
            var context = RepositoryCatalog.ReadCommitContext(catalogPath, request.TransactionId, opened.Descriptor.RepositoryId);
            if (context.State is "CATALOGED" or "COMMITTED")
            {
                return ReconcileLaterStartup(opened, catalogPath, context, request);
            }
            if (context.State is not ("STAGED_VERIFIED" or "COMMITTING" or "MOVED"))
            {
                throw new RepositoryException(RepositoryErrorCode.CommitRecoveryRequired, "C4 cannot reconcile this later or exceptional state; a later controlled slice or operator action is required.");
            }

            var observations = ObserveStartupAuthorities(opened.RootPath, catalogPath, context)
                .OrderBy(item => item.Authority, StringComparer.Ordinal)
                .ToArray();
            var byAuthority = observations.ToDictionary(item => item.Authority, StringComparer.Ordinal);
            var action = ClassifyStartupAction(context.State, byAuthority);
            if (action is null)
            {
                throw new RepositoryException(RepositoryErrorCode.CommitRecoveryRequired, "Startup evidence is conflicting, unsafe, unreadable, or outside the C4 boundary; retain all material and use trained-operator reconciliation.");
            }

            var changed = action.Value.ResultState != context.State;
            if (changed != (request.ResultTransitionId is not null && request.ResultOperationId is not null))
            {
                throw new ArgumentException("A state-changing reconciliation requires both result transition and operation identities; NO_ACTION requires neither.", nameof(request));
            }

            var value = new JournalStartupReconciliation(
                request.ReconciliationId,
                request.TransactionId,
                context.Revision,
                context.State,
                action.Value.ResultState,
                action.Value.ActionCode,
                request.ResultTransitionId,
                request.ResultOperationId,
                request.ActorWindowsAccount,
                Utc(request.StartedAt),
                Utc(request.FinishedAt),
                action.Value.Explanation,
                observations);
            return RepositoryCatalog.RecordAutomaticReconciliation(catalogPath, value);
        }
    }

    private static RepositoryCommitMoveSnapshot MoveStagedVerifiedPackage(
        RepositoryOpenResult opened,
        RepositoryCommitMoveRequest request)
    {
        var root = opened.RootPath;
        var catalogPath = RepositoryPathSafety.ResolveRelativePath(root, RepositoryConstants.CatalogRelativePath);
        var context = RepositoryCatalog.ReadCommitContext(
            catalogPath,
            request.TransactionId,
            opened.Descriptor.RepositoryId);
        var baseRevision = context.State switch
        {
            "STAGED_VERIFIED" => context.Revision,
            "COMMITTING" => context.Revision - 1,
            "MOVED" => context.Revision - 2,
            _ => throw new RepositoryException(RepositoryErrorCode.CommitRecoveryRequired, "C3 cannot replay after a later repository boundary; reconcile through the owning slice.")
        };
        var committing = NormalTransition(
            request,
            baseRevision,
            baseRevision + 1,
            "STAGED_VERIFIED",
            "COMMITTING",
            request.CommittingTransitionId,
            request.CommittingOperationId,
            request.CommittingRecordedAt);
        var moved = NormalTransition(
            request,
            baseRevision + 1,
            baseRevision + 2,
            "COMMITTING",
            "MOVED",
            request.MovedTransitionId,
            request.MovedOperationId,
            request.MovedRecordedAt);

        if (context.State == "MOVED")
        {
            RepositoryCatalog.RequireExactNormalTransition(catalogPath, committing);
            RepositoryCatalog.RequireExactNormalTransition(catalogPath, moved);
            RequireMovedPackage(root, context);
            return Snapshot(context, request, wasAlreadyMoved: true);
        }

        if (context.State == "COMMITTING")
        {
            RepositoryCatalog.RequireExactNormalTransition(catalogPath, committing);
            throw CommitRecovery(root, context);
        }

        if (context.State != "STAGED_VERIFIED")
        {
            throw new RepositoryException(RepositoryErrorCode.CommitRecoveryRequired, "Repository transaction is not at a normal C3 move boundary; controlled reconciliation is required.");
        }

        var stagingPath = RepositoryPathSafety.ResolveRelativePath(root, context.StagingRelativePath);
        var destinationPath = RepositoryPathSafety.ResolveRelativePath(root, context.DestinationRelativePath);
        if (!Directory.Exists(stagingPath) || Path.Exists(destinationPath))
        {
            throw CommitRecovery(root, context);
        }

        ValidatePackageAt(root, stagingPath, context);
        var destinationParent = Path.GetDirectoryName(destinationPath)
            ?? throw new RepositoryException(RepositoryErrorCode.UnsafePath, "Repository destination has no parent directory.");
        try
        {
            Directory.CreateDirectory(destinationParent);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new RepositoryException(RepositoryErrorCode.PackageMoveFailed, "Repository destination parent could not be prepared before commit intent.", exception);
        }

        RepositoryPathSafety.RejectReparsePointsInExistingPath(destinationParent);
        RepositoryDirectoryMover.RequireSameVolumeAndAvailableMetadataSpace(stagingPath, destinationParent);

        RepositoryCatalog.AdvanceNormalState(catalogPath, committing);
        RepositoryDirectoryMover.MoveDirectoryWriteThrough(stagingPath, destinationPath);
        if (Path.Exists(stagingPath) || !Directory.Exists(destinationPath))
        {
            throw new RepositoryException(RepositoryErrorCode.CommitRecoveryRequired, "Package location after the move is ambiguous; reconciliation is required.");
        }

        ValidatePackageAt(root, destinationPath, context);
        RepositoryCatalog.AdvanceNormalState(catalogPath, moved);
        var completed = RepositoryCatalog.ReadCommitContext(
            catalogPath,
            request.TransactionId,
            opened.Descriptor.RepositoryId);
        if (completed.State != "MOVED" || completed.Revision != moved.NextRevision)
        {
            throw new RepositoryException(RepositoryErrorCode.CatalogWriteFailed, "MOVED journal state could not be read back exactly.");
        }

        return Snapshot(completed, request, wasAlreadyMoved: false);
    }

    private static IReadOnlyList<RepositoryAuthorityObservation> ObserveStartupAuthorities(
        string root,
        string catalogPath,
        JournalCommitContext context)
    {
        var expectedId = Id(context.PackageId);
        var expectedHash = context.PackageContentSha256;
        var expectedLength = context.PackageByteLength;
        RepositoryAuthorityObservation Observation(string authority, string disposition, string? path = null) =>
            new(authority, disposition, expectedId, disposition == "MATCH" ? expectedId : null,
                expectedHash, disposition == "MATCH" ? expectedHash : null,
                expectedLength, disposition == "MATCH" ? expectedLength : null,
                path is null ? [] : [path]);

        var staging = ObservePackage(root, context.StagingRelativePath, context);
        var destination = ObservePackage(root, context.DestinationRelativePath, context);
        var verification = ObserveVerification(root, context);
        var later = RepositoryCatalog.ObserveLaterAuthorities(catalogPath, context);
        return
        [
            Observation("JOURNAL", "MATCH", RepositoryConstants.CatalogRelativePath),
            Observation("STAGING_PACKAGE", staging, context.StagingRelativePath),
            Observation("DESTINATION_PACKAGE", destination, context.DestinationRelativePath),
            Observation("CATALOG_LINKAGE", later.Catalog),
            Observation("VERIFICATION_RECORD", verification, context.VerificationRecordRelativePath),
            Observation("COMMIT_RECORD", later.Commit)
        ];
    }

    private static string ObservePackage(string root, string relativePath, JournalCommitContext context)
    {
        string path;
        try
        {
            path = RepositoryPathSafety.ResolveRelativePath(root, relativePath);
            if (!Path.Exists(path))
            {
                return "ABSENT";
            }
            if (!Directory.Exists(path))
            {
                return "MISMATCH";
            }
            ValidatePackageAt(root, path, context);
            return "MATCH";
        }
        catch (RepositoryException exception) when (exception.Code == RepositoryErrorCode.UnsafePath)
        {
            return "UNSAFE";
        }
        catch (RepositoryException exception) when (exception.Code is RepositoryErrorCode.EvidenceMismatch or RepositoryErrorCode.StagedPackageMissing or RepositoryErrorCode.CommitRecoveryRequired)
        {
            return "MISMATCH";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return "UNREADABLE";
        }
    }

    private static string ObserveVerification(string root, JournalCommitContext context)
    {
        try
        {
            var path = RepositoryPathSafety.ResolveRelativePath(root, context.VerificationRecordRelativePath);
            if (!Path.Exists(path))
            {
                return "ABSENT";
            }
            if (!File.Exists(path) || Directory.Exists(path))
            {
                return "MISMATCH";
            }
            RepositoryPathSafety.RejectReparsePointsInExistingPath(path);
            RepositoryPathSafety.RequireSingleLinkFile(path);
            return Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path))) == context.VerificationRecordContentSha256
                ? "MATCH"
                : "MISMATCH";
        }
        catch (RepositoryException exception) when (exception.Code == RepositoryErrorCode.UnsafePath)
        {
            return "UNSAFE";
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return "UNREADABLE";
        }
    }

    private static (string ActionCode, string ResultState, string Explanation)? ClassifyStartupAction(
        string state,
        IReadOnlyDictionary<string, RepositoryAuthorityObservation> observations)
    {
        bool Is(string authority, string disposition) => observations[authority].Disposition == disposition;
        if (!Is("JOURNAL", "MATCH") || !Is("VERIFICATION_RECORD", "MATCH")
            || !Is("CATALOG_LINKAGE", "ABSENT") || !Is("COMMIT_RECORD", "ABSENT"))
        {
            return null;
        }

        return (state, observations["STAGING_PACKAGE"].Disposition, observations["DESTINATION_PACKAGE"].Disposition) switch
        {
            ("STAGED_VERIFIED", "MATCH", "ABSENT") => ("NO_ACTION", "STAGED_VERIFIED", "Exact durable evidence directly proves STAGED_VERIFIED."),
            ("COMMITTING", "MATCH", "ABSENT") => ("RETRY_FROM_STAGED", "STAGED_VERIFIED", "Exact verified staging evidence permits a bounded retry from staging."),
            ("COMMITTING", "ABSENT", "MATCH") => ("RESUME_AFTER_MOVE", "MOVED", "Exact destination evidence and durable intent prove the move boundary."),
            ("MOVED", "ABSENT", "MATCH") => ("NO_ACTION", "MOVED", "Exact durable evidence directly proves MOVED."),
            _ => null
        };
    }

    private static void RequireMovedPackage(string root, JournalCommitContext context)
    {
        var stagingPath = RepositoryPathSafety.ResolveRelativePath(root, context.StagingRelativePath);
        var destinationPath = RepositoryPathSafety.ResolveRelativePath(root, context.DestinationRelativePath);
        if (Path.Exists(stagingPath) || !Directory.Exists(destinationPath))
        {
            throw new RepositoryException(RepositoryErrorCode.CommitRecoveryRequired, "Journal state and package location disagree; reconciliation is required.");
        }

        ValidatePackageAt(root, destinationPath, context);
    }

    private static void ValidatePackageAt(string root, string packagePath, JournalCommitContext context)
    {
        var verificationPath = RepositoryPathSafety.ResolveRelativePath(root, context.VerificationRecordRelativePath);
        RepositoryPathSafety.RejectReparsePointsInExistingPath(verificationPath);
        if (!File.Exists(verificationPath) || Directory.Exists(verificationPath))
        {
            throw new RepositoryException(RepositoryErrorCode.CommitRecoveryRequired, "Immutable verification record is missing; reconciliation is required.");
        }

        RepositoryPathSafety.RequireSingleLinkFile(verificationPath);
        var verificationBytes = File.ReadAllBytes(verificationPath);
        var expectation = new StagedPackageEvidenceExpectation(
            context.SubjectId,
            context.SessionId,
            context.TrialId,
            context.SourceId,
            context.CaptureAttemptId,
            context.PackageId,
            context.PackageContentSha256,
            context.ArtifactSetSha256,
            context.VerificationRecordId,
            context.VerificationRecordContentSha256,
            context.PackageByteLength,
            context.ArtifactCount,
            verificationBytes);
        var evidence = StagedPackageEvidenceValidator.Validate(packagePath, expectation);
        if (evidence.VerificationRecordRevision != context.VerificationRecordRevision)
        {
            throw new RepositoryException(RepositoryErrorCode.EvidenceMismatch, "Verification-record revision differs from its immutable repository index.");
        }
    }

    private static RepositoryException CommitRecovery(string root, JournalCommitContext context)
    {
        var staging = Path.Exists(RepositoryPathSafety.ResolveRelativePath(root, context.StagingRelativePath));
        var destination = Path.Exists(RepositoryPathSafety.ResolveRelativePath(root, context.DestinationRelativePath));
        var location = (staging, destination) switch
        {
            (true, false) => "The exact staging location remains; RETRY_FROM_STAGED reconciliation is required.",
            (false, true) => "The destination location exists; RESUME_AFTER_MOVE reconciliation is required.",
            (true, true) => "Both staging and destination contain material; operator-controlled reconciliation is required.",
            _ => "Neither expected package location exists; operator-controlled reconciliation is required."
        };
        return new RepositoryException(RepositoryErrorCode.CommitRecoveryRequired, location);
    }

    private static JournalNormalTransition NormalTransition(
        RepositoryCommitMoveRequest request,
        long expectedRevision,
        long nextRevision,
        string fromState,
        string toState,
        Guid transitionId,
        Guid operationId,
        DateTimeOffset recordedAt) =>
        new(
            request.TransactionId,
            expectedRevision,
            nextRevision,
            fromState,
            toState,
            transitionId,
            operationId,
            request.ActorWindowsAccount,
            recordedAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture));

    private static RepositoryCommitMoveSnapshot Snapshot(
        JournalCommitContext context,
        RepositoryCommitMoveRequest request,
        bool wasAlreadyMoved) =>
        new(
            Id(context.TransactionId),
            context.Revision,
            context.State,
            context.StagingRelativePath,
            context.DestinationRelativePath,
            Id(request.CommittingOperationId),
            Id(request.MovedOperationId),
            wasAlreadyMoved);

    private static RepositoryOpenResult ClassifyCompatibility(
        RepositoryDescriptor descriptor,
        IReadOnlySet<string> properties)
    {
        var placeholderRoot = string.Empty;
        if (!TryVersion(descriptor.SchemaVersion, out var descriptorSchema) || descriptorSchema.Major != 1)
        {
            return ReadOnly("UNSUPPORTED_DESCRIPTOR_SCHEMA", "Descriptor schema is not supported; mutation and migration are disabled.");
        }

        if (!TryVersion(descriptor.InterfaceVersion, out var interfaceVersion) || interfaceVersion.Major != 1)
        {
            return ReadOnly("UNSUPPORTED_INTERFACE_MAJOR", "Repository interface major version is not supported; mutation and migration are disabled.");
        }

        var unknownFeature = descriptor.RequiredFeatures.FirstOrDefault(
            feature => !RepositoryConstants.RequiredFeatures.Contains(feature, StringComparer.Ordinal));
        if (unknownFeature is not null)
        {
            return ReadOnly("UNKNOWN_REQUIRED_FEATURE", $"Required feature '{unknownFeature}' is not supported; mutation and migration are disabled.");
        }

        if (!string.Equals(descriptor.InterfaceVersion, RepositoryConstants.InterfaceVersion, StringComparison.Ordinal)
            || !string.Equals(descriptor.NamespaceVersion, RepositoryConstants.NamespaceVersion, StringComparison.Ordinal)
            || !string.Equals(descriptor.CatalogSchemaVersion, RepositoryConstants.CatalogSchemaVersion, StringComparison.Ordinal)
            || !string.Equals(descriptor.SchemaVersion, RepositoryConstants.DescriptorSchemaVersion, StringComparison.Ordinal))
        {
            return ReadOnly("MIGRATION_REQUIRED", "Repository versions are not an exact supported set; opening does not migrate or mutate them.");
        }

        if (!string.Equals(descriptor.CatalogRelativePath, RepositoryConstants.CatalogRelativePath, StringComparison.Ordinal)
            || !descriptor.RequiredFeatures.ToHashSet(StringComparer.Ordinal).SetEquals(RepositoryConstants.RequiredFeatures)
            || !RepositoryDescriptorCodec.HasOnlyVersionOneProperties(properties))
        {
            throw new RepositoryException(RepositoryErrorCode.DescriptorInvalid, "Version 1 repository descriptor does not conform to its closed contract.");
        }

        return new RepositoryOpenResult(
            placeholderRoot,
            descriptor,
            RepositoryAccessMode.MutationAllowed,
            "SUPPORTED",
            "Repository descriptor and catalog are compatible and verified for mutation.");

        RepositoryOpenResult ReadOnly(string code, string explanation) =>
            new(placeholderRoot, descriptor, RepositoryAccessMode.ReadOnlyInspection, code, explanation);
    }

    private static bool TryVersion(string text, out Version version) =>
        Version.TryParse(text, out version!);

    private static void PublishDescriptor(string descriptorPath, RepositoryDescriptor descriptor)
    {
        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(descriptorPath)!,
            $".repository-{Guid.NewGuid():D}.tmp");
        try
        {
            var bytes = RepositoryDescriptorCodec.Serialize(descriptor);
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            var verified = RepositoryDescriptorCodec.Parse(File.ReadAllBytes(temporaryPath), out var properties);
            if (!Equivalent(verified, descriptor) || !RepositoryDescriptorCodec.HasOnlyVersionOneProperties(properties))
            {
                throw new RepositoryException(RepositoryErrorCode.DescriptorInvalid, "Serialized repository descriptor did not verify before publication.");
            }

            File.Move(temporaryPath, descriptorPath, overwrite: false);
            RepositoryPathSafety.RequireSingleLinkFile(descriptorPath);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static void PublishImmutableRecord(string targetPath, ReadOnlySpan<byte> bytes)
    {
        var parent = Path.GetDirectoryName(targetPath)
            ?? throw new RepositoryException(RepositoryErrorCode.UnsafePath, "Immutable record path has no parent.");
        Directory.CreateDirectory(parent);
        RepositoryPathSafety.RejectReparsePointsInExistingPath(parent);
        if (File.Exists(targetPath))
        {
            RepositoryPathSafety.RequireSingleLinkFile(targetPath);
            if (File.ReadAllBytes(targetPath).AsSpan().SequenceEqual(bytes))
            {
                return;
            }

            throw new RepositoryException(RepositoryErrorCode.ImmutableRecordConflict, "Immutable verification record already exists with different bytes.");
        }

        var temporaryPath = Path.Combine(parent, $".{Path.GetFileName(targetPath)}-{Guid.NewGuid():D}.tmp");
        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            try
            {
                File.Move(temporaryPath, targetPath, overwrite: false);
            }
            catch (IOException) when (File.Exists(targetPath))
            {
                RepositoryPathSafety.RequireSingleLinkFile(targetPath);
                if (!File.ReadAllBytes(targetPath).AsSpan().SequenceEqual(bytes))
                {
                    throw new RepositoryException(RepositoryErrorCode.ImmutableRecordConflict, "Concurrent immutable verification record publication conflicted.");
                }
            }
            RepositoryPathSafety.RequireSingleLinkFile(targetPath);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private static bool Equivalent(RepositoryDescriptor left, RepositoryDescriptor right) =>
        string.Equals(left.SchemaVersion, right.SchemaVersion, StringComparison.Ordinal)
        && string.Equals(left.RepositoryId, right.RepositoryId, StringComparison.Ordinal)
        && string.Equals(left.InterfaceVersion, right.InterfaceVersion, StringComparison.Ordinal)
        && string.Equals(left.NamespaceVersion, right.NamespaceVersion, StringComparison.Ordinal)
        && string.Equals(left.CatalogSchemaVersion, right.CatalogSchemaVersion, StringComparison.Ordinal)
        && string.Equals(left.CreatedUtc, right.CreatedUtc, StringComparison.Ordinal)
        && string.Equals(left.CreatedByWindowsAccount, right.CreatedByWindowsAccount, StringComparison.Ordinal)
        && string.Equals(left.CatalogRelativePath, right.CatalogRelativePath, StringComparison.Ordinal)
        && left.RequiredFeatures.SequenceEqual(right.RequiredFeatures, StringComparer.Ordinal);

    private static void ValidateWindowsAccount(string account)
    {
        if (string.IsNullOrWhiteSpace(account) || account.Length > 256)
        {
            throw new ArgumentException("Creating Windows account must contain 1 to 256 non-whitespace characters.", nameof(account));
        }
    }

    private static void ValidateRegistration(StagedVerifiedPackageRegistration registration)
    {
        var ids = new[]
        {
            registration.TransactionId, registration.TransitionId, registration.OperationId,
            registration.RecordIndexEntryId, registration.SubjectId, registration.SessionId,
            registration.TrialId, registration.SourceId, registration.CaptureAttemptId,
            registration.CollectionAttemptId, registration.PackageId, registration.VerificationRecordId
        };
        if (Array.Exists(ids, id => id == Guid.Empty))
        {
            throw new ArgumentException("Repository registration UUIDs must not be empty.", nameof(registration));
        }

        ValidateSha256(registration.PackageContentSha256, nameof(registration.PackageContentSha256));
        ValidateSha256(registration.ArtifactSetSha256, nameof(registration.ArtifactSetSha256));
        ValidateSha256(registration.VerificationRecordContentSha256, nameof(registration.VerificationRecordContentSha256));
        if (!ulong.TryParse(registration.PackageByteLength, NumberStyles.None, CultureInfo.InvariantCulture, out var byteLength)
            || registration.PackageByteLength != byteLength.ToString(CultureInfo.InvariantCulture))
        {
            throw new ArgumentException("Package byte length must be a canonical unsigned 64-bit decimal string.", nameof(registration));
        }

        if (registration.ArtifactCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(registration), "Artifact count must be positive.");
        }

        if (registration.VerificationRecordUtf8.IsEmpty)
        {
            throw new ArgumentException("Verification record bytes are required.", nameof(registration));
        }

        ValidateWindowsAccount(registration.ActorWindowsAccount);
    }

    private static void ValidateCommitMoveRequest(RepositoryCommitMoveRequest request)
    {
        var ids = new[]
        {
            request.TransactionId,
            request.CommittingTransitionId,
            request.CommittingOperationId,
            request.MovedTransitionId,
            request.MovedOperationId
        };
        if (Array.Exists(ids, id => id == Guid.Empty))
        {
            throw new ArgumentException("Commit-move UUIDs must not be empty.", nameof(request));
        }

        if (request.CommittingTransitionId == request.MovedTransitionId
            || request.CommittingOperationId == request.MovedOperationId)
        {
            throw new ArgumentException("Commit-move transition and operation identities must be unique per boundary.", nameof(request));
        }

        ValidateWindowsAccount(request.ActorWindowsAccount);
    }

    private static void ValidateStartupReconciliationRequest(RepositoryStartupReconciliationRequest request)
    {
        if (request.TransactionId == Guid.Empty || request.ReconciliationId == Guid.Empty
            || request.ResultTransitionId == Guid.Empty || request.ResultOperationId == Guid.Empty)
        {
            throw new ArgumentException("Startup reconciliation UUIDs must not be empty.", nameof(request));
        }
        if ((request.ResultTransitionId is null) != (request.ResultOperationId is null))
        {
            throw new ArgumentException("Result transition and operation identities must be supplied together.", nameof(request));
        }
        if (request.FinishedAt < request.StartedAt)
        {
            throw new ArgumentException("Reconciliation finish time cannot precede start time.", nameof(request));
        }
        ValidateWindowsAccount(request.ActorWindowsAccount);
    }

    private static string Utc(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);

    private static void ValidateSha256(string value, string parameterName)
    {
        if (value is null || !Regex.IsMatch(value, "^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
        {
            throw new ArgumentException("SHA-256 values must contain exactly 64 lowercase hexadecimal characters.", parameterName);
        }
    }

    private static string Id(Guid value) => value.ToString("D");
}
