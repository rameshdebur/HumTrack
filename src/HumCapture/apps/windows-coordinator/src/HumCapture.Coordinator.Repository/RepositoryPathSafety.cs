using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace HumCapture.Coordinator.Repository;

internal static class RepositoryPathSafety
{
    public static string NormalizeAbsoluteRoot(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath) || !Path.IsPathFullyQualified(rootPath))
        {
            throw new RepositoryException(RepositoryErrorCode.RootPathNotAbsolute, "Repository data root must be an absolute path.");
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(rootPath));
    }

    public static void RejectReparsePointsInExistingPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath)
            ?? throw new RepositoryException(RepositoryErrorCode.UnsafePath, "Repository path has no filesystem root.");
        var relative = fullPath[root.Length..];
        var current = root;
        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!Path.Exists(current))
            {
                break;
            }

            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new RepositoryException(RepositoryErrorCode.UnsafePath, $"Repository path crosses a reparse point: {current}");
            }
        }
    }

    public static void RequireSingleLinkFile(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new RepositoryException(RepositoryErrorCode.UnsafePath, "Mutable repository open requires Windows file-identity checks.");
        }

        using SafeFileHandle handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (!GetFileInformationByHandle(handle, out var information))
        {
            throw new RepositoryException(RepositoryErrorCode.UnsafePath, $"Could not inspect repository file links: {path}", new System.ComponentModel.Win32Exception());
        }

        if (information.NumberOfLinks != 1)
        {
            throw new RepositoryException(RepositoryErrorCode.UnsafePath, $"Repository file must not be hard-linked: {path}");
        }
    }

    public static string ResolveRelativePath(string root, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || Path.IsPathFullyQualified(relativePath)
            || relativePath.Contains('\\', StringComparison.Ordinal)
            || relativePath.Contains(':', StringComparison.Ordinal))
        {
            throw new RepositoryException(RepositoryErrorCode.UnsafePath, "Repository-relative path is not canonical.");
        }

        var canonical = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var resolved = Path.GetFullPath(Path.Combine(root, canonical));
        var prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new RepositoryException(RepositoryErrorCode.UnsafePath, "Repository-relative path escapes the data root.");
        }

        return resolved;
    }

    public static void RequireSafeDirectoryTree(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new RepositoryException(RepositoryErrorCode.StagedPackageMissing, "Required staged package directory is missing.");
        }

        RejectReparsePointsInExistingPath(directoryPath);
        foreach (var entry in Directory.EnumerateFileSystemEntries(directoryPath, "*", SearchOption.AllDirectories))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new RepositoryException(RepositoryErrorCode.UnsafePath, $"Staged package contains a reparse point: {entry}");
            }

            if (!Directory.Exists(entry))
            {
                RequireSingleLinkFile(entry);
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle file,
        out ByHandleFileInformation fileInformation);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
}
