using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace HumCapture.Coordinator.Repository;

internal static class RepositoryDirectoryMover
{
    private const uint _moveFileWriteThrough = 0x00000008;
    private const int _errorFileExists = 80;
    private const int _errorAlreadyExists = 183;
    private const int _errorNotSameDevice = 17;

    public static void RequireSameVolumeAndAvailableMetadataSpace(string stagingPath, string destinationParent)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new RepositoryException(RepositoryErrorCode.SameVolumeRequired, "Atomic repository movement requires Windows volume checks.");
        }

        var sourceVolume = GetVolumePath(stagingPath);
        var destinationVolume = GetVolumePath(destinationParent);
        if (!string.Equals(sourceVolume, destinationVolume, StringComparison.OrdinalIgnoreCase))
        {
            throw new RepositoryException(RepositoryErrorCode.SameVolumeRequired, "Staging and destination are not on the same filesystem volume.");
        }

        if (!GetDiskFreeSpaceExW(destinationParent, out var availableBytes, out _, out _))
        {
            throw PlatformFailure("Repository destination free space could not be checked.");
        }

        if (availableBytes == 0)
        {
            throw new RepositoryException(RepositoryErrorCode.PackageMoveFailed, "Repository volume has no available space for filesystem metadata.");
        }
    }

    public static void MoveDirectoryWriteThrough(string stagingPath, string destinationPath)
    {
        if (Path.Exists(destinationPath))
        {
            throw new RepositoryException(RepositoryErrorCode.CommitRecoveryRequired, "Destination material appeared before the package move; reconciliation is required.");
        }

        if (MoveFileExW(stagingPath, destinationPath, _moveFileWriteThrough))
        {
            return;
        }

        var error = Marshal.GetLastWin32Error();
        if (error is _errorFileExists or _errorAlreadyExists)
        {
            throw new RepositoryException(RepositoryErrorCode.CommitRecoveryRequired, "Destination material appeared during the package move; reconciliation is required.");
        }

        if (error == _errorNotSameDevice)
        {
            throw new RepositoryException(RepositoryErrorCode.SameVolumeRequired, "The operating system refused a cross-volume package move.");
        }

        throw new RepositoryException(
            RepositoryErrorCode.PackageMoveFailed,
            "The write-through atomic package move failed after durable commit intent; reconciliation is required.",
            new Win32Exception(error));
    }

    private static string GetVolumePath(string path)
    {
        var buffer = new StringBuilder(512);
        if (!GetVolumePathNameW(path, buffer, (uint)buffer.Capacity))
        {
            throw PlatformFailure("Repository volume identity could not be resolved.");
        }

        return buffer.ToString();
    }

    private static RepositoryException PlatformFailure(string message) =>
        new(RepositoryErrorCode.PackageMoveFailed, message, new Win32Exception(Marshal.GetLastWin32Error()));

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetVolumePathNameW(string fileName, StringBuilder volumePathName, uint bufferLength);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceExW(
        string directoryName,
        out ulong freeBytesAvailable,
        out ulong totalNumberOfBytes,
        out ulong totalNumberOfFreeBytes);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveFileExW(string existingFileName, string newFileName, uint flags);
}
