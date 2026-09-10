namespace HumCapture.Coordinator.Repository;

/// <summary>Identifies a fail-closed repository initialize or open error.</summary>
public enum RepositoryErrorCode
{
    /// <summary>The configured data root was not absolute.</summary>
    RootPathNotAbsolute,
    /// <summary>The initialization target contained existing material.</summary>
    RootNotEmpty,
    /// <summary>A descriptor already identifies the root as initialized.</summary>
    AlreadyInitialized,
    /// <summary>An initialization lock prevented exclusive initialization.</summary>
    InitializationInProgress,
    /// <summary>The repository descriptor was absent.</summary>
    DescriptorMissing,
    /// <summary>The repository descriptor was malformed or contract-incompatible.</summary>
    DescriptorInvalid,
    /// <summary>A link or reparse condition made the repository path unsafe.</summary>
    UnsafePath,
    /// <summary>The compatible repository's catalog was absent.</summary>
    CatalogMissing,
    /// <summary>The repository catalog was unreadable or failed integrity checks.</summary>
    CatalogInvalid,
    /// <summary>The descriptor and catalog identities or versions disagreed.</summary>
    CatalogMetadataMismatch,
    /// <summary>The repository was opened in inspection-only mode.</summary>
    MutationNotAllowed,
    /// <summary>The staged package path or required evidence was absent.</summary>
    StagedPackageMissing,
    /// <summary>The staged package or verification evidence did not agree with the requested identity.</summary>
    EvidenceMismatch,
    /// <summary>An immutable record already existed with different bytes.</summary>
    ImmutableRecordConflict,
    /// <summary>A transaction, operation, or package identity was reused inconsistently.</summary>
    JournalConflict,
    /// <summary>The repository catalog rejected or could not persist a journal mutation.</summary>
    CatalogWriteFailed
}

/// <summary>Represents a controlled repository initialization or open failure.</summary>
public sealed class RepositoryException : Exception
{
    /// <summary>Initializes a repository exception with a stable error code.</summary>
    /// <param name="code">The controlled repository error.</param>
    /// <param name="message">The bounded diagnostic message.</param>
    public RepositoryException(RepositoryErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Initializes a repository exception with an underlying platform failure.</summary>
    /// <param name="code">The controlled repository error.</param>
    /// <param name="message">The bounded diagnostic message.</param>
    /// <param name="innerException">The underlying platform exception.</param>
    public RepositoryException(RepositoryErrorCode code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Gets the controlled repository error code.</summary>
    public RepositoryErrorCode Code { get; }
}
