namespace HumCapture.Coordinator.Repository;

/// <summary>Defines the authority granted by repository compatibility checks.</summary>
public enum RepositoryAccessMode
{
    /// <summary>The repository may be mutated by the current implementation.</summary>
    MutationAllowed,
    /// <summary>Only descriptor inspection is permitted; mutation and migration are disabled.</summary>
    ReadOnlyInspection
}

/// <summary>Reports the compatibility decision made while opening a repository.</summary>
/// <param name="RootPath">The normalized absolute repository root.</param>
/// <param name="Descriptor">The parsed repository descriptor.</param>
/// <param name="AccessMode">The granted access mode.</param>
/// <param name="ReasonCode">A stable machine-readable compatibility reason.</param>
/// <param name="Explanation">A bounded operator-facing explanation.</param>
public sealed record RepositoryOpenResult(
    string RootPath,
    RepositoryDescriptor Descriptor,
    RepositoryAccessMode AccessMode,
    string ReasonCode,
    string Explanation)
{
    /// <summary>Gets a value indicating whether repository mutation is allowed.</summary>
    public bool CanMutate => AccessMode == RepositoryAccessMode.MutationAllowed;
}
