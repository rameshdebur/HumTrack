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
    CatalogMetadataMismatch
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
