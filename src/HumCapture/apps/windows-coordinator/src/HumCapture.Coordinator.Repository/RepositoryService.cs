namespace HumCapture.Coordinator.Repository;

/// <summary>Initializes and compatibility-checks the Coordinator-owned HumCapture repository.</summary>
public sealed class RepositoryService
{
    private const string InitializationLockName = ".humcapture-initialize.lock";

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
}
