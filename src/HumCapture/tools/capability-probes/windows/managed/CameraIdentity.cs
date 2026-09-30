using System.Text.RegularExpressions;

namespace HumCapture.CapabilityProbe.Windows;

internal static partial class CameraIdentity
{
    [GeneratedRegex(@"(?i)(?:\\\\\?\\)?(?<bus>usb)[#\\](?<hardware>vid_[0-9a-f]{4}[&]pid_[0-9a-f]{4}(?:[&]mi_[0-9a-f]{2})?)[#\\](?<instance>[^#{\\]+)")]
    private static partial Regex UsbInterfacePattern();

    public static string? Normalize(string deviceInterfaceId)
    {
        if (string.IsNullOrWhiteSpace(deviceInterfaceId))
        {
            return null;
        }

        Match match = UsbInterfacePattern().Match(deviceInterfaceId);
        if (!match.Success)
        {
            return null;
        }

        return $"USB\\{match.Groups["hardware"].Value}\\{match.Groups["instance"].Value}"
            .ToUpperInvariant();
    }

    public static CameraCandidate SelectExact(
        IReadOnlyList<CameraCandidate> candidates,
        string requestedIdentity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedIdentity);
        string requested = requestedIdentity.Trim();
        CameraCandidate[] matches = candidates
            .Where(candidate => string.Equals(candidate.InterfaceId, requested, StringComparison.OrdinalIgnoreCase)
                || string.Equals(candidate.NormalizedInstanceId, requested, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new CameraSelectionException("DEVICE_NOT_FOUND", "The requested camera identity was not enumerated."),
            _ => throw new CameraSelectionException("DEVICE_IDENTITY_AMBIGUOUS", "The requested camera identity matched more than one enumerated camera.")
        };
    }
}

internal sealed record CameraCandidate(string Name, string InterfaceId, string? NormalizedInstanceId);

/// <summary>Reports an exact, machine-readable camera-selection failure.</summary>
/// <param name="code">Stable probe error code.</param>
/// <param name="message">Operator-readable failure detail.</param>
public sealed class CameraSelectionException(string code, string message) : Exception(message)
{
    /// <summary>Gets the stable probe error code.</summary>
    public string Code { get; } = code;
}
