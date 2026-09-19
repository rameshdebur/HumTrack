using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace HumCapture.Coordinator.Repository;

// Local Windows snapshot lease. Does not publish, admit, move or delete packages.
internal sealed class PackageInputLease : IDisposable
{
    private readonly string root;
    private readonly Dictionary<string, FileStream> files = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SafeFileHandle> directories = [];
    private readonly HashSet<string> entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> buffers = new(StringComparer.OrdinalIgnoreCase);
    private long bufferedBytes;
    private bool disposed;
    internal PackageInputManifest Manifest { get; private set; } = null!;

    private PackageInputLease(string root) { this.root = root; }

    internal static PackageInputLease Open(string directory, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var root = RepositoryPathSafety.NormalizeAbsoluteRoot(directory);
        if (!OperatingSystem.IsWindows() || root.StartsWith(@"\\", StringComparison.Ordinal)
            || Path.GetPathRoot(root) is not { Length: 3 } drive || drive[1] != ':'
            || new DriveInfo(drive).DriveType == DriveType.Network)
        { throw new InvalidDataException("Only ordinary absolute local Windows package directories are supported."); }
        var lease = new PackageInputLease(root);
        try { lease.Initialize(token); return lease; }
        catch { lease.Dispose(); throw; }
    }

    private void Initialize(CancellationToken token)
    {
        var ancestors = new Stack<string>();
        for (var current = root; current is not null; current = Path.GetDirectoryName(current)) { ancestors.Push(current); }
        foreach (var directory in ancestors) { token.ThrowIfCancellationRequested(); PinDirectory(directory); }
        var pending = new Queue<string>(); pending.Enqueue(root); var count = 0;
        while (pending.TryDequeue(out var directory))
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                token.ThrowIfCancellationRequested();
                var attributes = File.GetAttributes(path);
                PackageInputManifest.Need((attributes & FileAttributes.ReparsePoint) == 0, "Reparse entry is unsupported.");
                var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                PackageInputManifest.Need(entries.Add(relative), "Duplicate package entry.");
                if ((attributes & FileAttributes.Directory) != 0)
                {
                    PackageInputManifest.Need(++count <= 256, "Package directory envelope exceeded.");
                    PinDirectory(path); pending.Enqueue(path);
                }
                else
                {
                    PackageInputManifest.Need(files.Count < 257, "Package artifact envelope exceeded.");
                    var handle = OpenHandle(path, 0x80000000, 1, 0x00200000); // read; share read; open reparse itself
                    try { CheckHandle(handle, false); files.Add(relative, new FileStream(handle, FileAccess.Read, 65536, false)); }
                    catch { handle.Dispose(); throw; }
                }
            }
        }
        PackageInputManifest.Need(files.ContainsKey("package-manifest.json"), "Package manifest is missing.");
        Manifest = PackageInputManifest.Parse(Read("package-manifest.json", MetadataSchemaValidator.MaximumBytes, token), token);
        var expected = Manifest.Artifacts.Select(a => a.Path).Append("package-manifest.json").ToHashSet(StringComparer.OrdinalIgnoreCase);
        PackageInputManifest.Need(expected.SetEquals(files.Keys), "Package file set differs from manifest.");
        foreach (var artifact in Manifest.Artifacts)
        {
            token.ThrowIfCancellationRequested(); var file = files[artifact.Path];
            PackageInputManifest.Need(file.Length == artifact.Digest.ByteLength, "Artifact byte length differs: " + artifact.Path);
            PackageInputManifest.Need(Hash(file, token) == artifact.Digest.Sha256, "Artifact hash differs: " + artifact.Path);
        }
        CheckUnchanged(token);
    }

    private void PinDirectory(string directory)
    {
        var handle = OpenHandle(directory, 0x80, 3, 0x02200000); // attributes; read/write sharing, no delete; backup + reparse
        try { CheckHandle(handle, true); directories.Add(handle); }
        catch { handle.Dispose(); throw; }
    }

    private byte[] Read(string path, int maximum, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        token.ThrowIfCancellationRequested();
        if (buffers.TryGetValue(path, out var existing)) { return existing; }
        var stream = files[path];
        PackageInputManifest.Need(stream.Length <= maximum && bufferedBytes + stream.Length <= 128L * 1024 * 1024,
            "Sidecar input exceeds bounded memory envelope.");
        var bytes = new byte[(int)stream.Length]; stream.Position = 0;
        var offset = 0;
        while (offset < bytes.Length)
        {
            token.ThrowIfCancellationRequested();
            var read = stream.Read(bytes.AsSpan(offset, Math.Min(65536, bytes.Length - offset)));
            if (read == 0) { throw new EndOfStreamException("Leased file ended unexpectedly."); }
            offset += read;
        }
        bufferedBytes += bytes.Length; buffers.Add(path, bytes); return bytes;
    }

    internal ReadOnlyMemory<byte> ReadArtifact(PackageInputArtifact artifact, CancellationToken token)
    {
        PackageInputManifest.Need(Manifest.Artifacts.Contains(artifact), "Artifact is not admitted by this manifest.");
        PackageInputManifest.Need(artifact.Role != "SCIENTIFIC_MASTER_VIDEO", "Master must be streamed, not buffered.");
        var maximum = artifact.Role switch { "IMU_SAMPLES" => 80_000_064, "FRAME_TIMESTAMPS" => 9_600_064, _ => MetadataSchemaValidator.MaximumBytes };
        return Read(artifact.Path, maximum, token);
    }

    internal string MasterPath(PackageInputArtifact artifact)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        PackageInputManifest.Need(artifact.Role == "SCIENTIFIC_MASTER_VIDEO" && Manifest.Artifacts.Contains(artifact), "Unknown master artifact.");
        return RepositoryPathSafety.ResolveRelativePath(root, artifact.Path);
    }

    internal void CheckUnchanged(CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(disposed, this); token.ThrowIfCancellationRequested();
        var actual = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>(); pending.Enqueue(root);
        while (pending.TryDequeue(out var directory))
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(directory))
            {
                token.ThrowIfCancellationRequested(); var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                PackageInputManifest.Need(entries.Contains(relative) && actual.Add(relative), "Package inventory changed during verification.");
                var attributes = File.GetAttributes(path);
                PackageInputManifest.Need((attributes & FileAttributes.ReparsePoint) == 0, "Package acquired reparse entry.");
                if ((attributes & FileAttributes.Directory) != 0) { pending.Enqueue(path); }
            }
        }
        PackageInputManifest.Need(entries.SetEquals(actual), "Package inventory changed during verification.");
        foreach (var file in files.Values) { token.ThrowIfCancellationRequested(); CheckHandle(file.SafeFileHandle, false); }
    }

    private static string Hash(Stream stream, CancellationToken token)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[65536]; stream.Position = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested(); var read = stream.Read(buffer);
            if (read == 0) { break; }
            hash.AppendData(buffer, 0, read);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }

    public void Dispose()
    {
        if (disposed) { return; }
        disposed = true;
        foreach (var file in files.Values) { file.Dispose(); }
        foreach (var directory in directories.AsEnumerable().Reverse()) { directory.Dispose(); }
        buffers.Clear();
    }

    private static SafeFileHandle OpenHandle(string path, uint access, uint share, uint flags)
    {
        // Inputs were normalized to ordinary local drive paths above. Native Win32
        // calls need extended-length syntax even when .NET file APIs already work.
        var handle = CreateFileW(@"\\?\" + path, access, share, IntPtr.Zero, 3, flags, IntPtr.Zero);
        if (!handle.IsInvalid) { return handle; }
        var error = Marshal.GetLastWin32Error(); handle.Dispose();
        throw new IOException("Cannot acquire read lease: " + path, new System.ComponentModel.Win32Exception(error));
    }
    private static void CheckHandle(SafeFileHandle handle, bool directory)
    {
        if (!GetFileInformationByHandle(handle, out var info)) { throw new IOException("Cannot inspect leased file identity."); }
        PackageInputManifest.Need((info.Attributes & 0x400) == 0 && ((info.Attributes & 0x10) != 0) == directory
            && (directory || info.Links == 1), "Leased entry is a reparse point, hard link or wrong file type.");
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Created, Accessed, Written;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
}
