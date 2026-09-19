using System.Security.Principal;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace HumCapture.Coordinator.Repository;

public sealed partial class RepositoryService
{
    private static readonly JsonSerializerOptions AdmissionJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };

    /// <summary>
    /// Admits existing verified staging using a bounded request file and current
    /// Windows identity. Does not collect packages or generate verifier evidence.
    /// </summary>
    public RepositoryTransactionSnapshot AdmitStagedRequest(string rootPath, string requestPath)
    {
        var opened = Open(rootPath);
        if (!opened.CanMutate)
        {
            throw new RepositoryException(RepositoryErrorCode.MutationNotAllowed, "Admission requires a supported writable repository.");
        }
        lock (_commitLocks.GetOrAdd(opened.RootPath, static _ => new object()))
        {
            var request = ReadAdmissionRequest(requestPath);
            return RegisterStagedVerifiedPackage(opened.RootPath, request);
        }
    }

    private static StagedVerifiedPackageRegistration ReadAdmissionRequest(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            {
                throw InvalidAdmission();
            }
            RepositoryPathSafety.RejectReparsePointsInExistingPath(path);
            RepositoryPathSafety.RequireSingleLinkFile(path);
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is < 1 or > 1048576) { throw InvalidAdmission(); }
            var bytes = new byte[checked((int)stream.Length)];
            stream.ReadExactly(bytes);
            using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            RequireUniqueAdmissionFields(root);
            if (root.ValueKind != JsonValueKind.Object || root.EnumerateObject().Count() != 2
                || root.GetProperty("schema_version").GetString() != "1.0.0")
            {
                throw InvalidAdmission();
            }
            var element = root.GetProperty("registration");
            if (element.ValueKind != JsonValueKind.Object || element.TryGetProperty("actor_windows_account", out _))
            {
                throw InvalidAdmission();
            }
            var registration = JsonNode.Parse(element.GetRawText())!.AsObject();
            using var identity = WindowsIdentity.GetCurrent();
            registration["actor_windows_account"] = identity.Name;
            var result = registration.Deserialize<StagedVerifiedPackageRegistration>(AdmissionJsonOptions)
                ?? throw InvalidAdmission();
            if (result.RecordedAt is null || result.RecordedAt.Value.Offset != TimeSpan.Zero
                || result.RecordedAt.Value == DateTimeOffset.MinValue)
            {
                throw InvalidAdmission();
            }
            return result;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException
            or KeyNotFoundException or FormatException or ArgumentException)
        {
            throw new RepositoryException(RepositoryErrorCode.AdmissionRequestInvalid,
                "Admission request is invalid; retain it and the staged package for review.", exception);
        }
    }

    private static void RequireUniqueAdmissionFields(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) { throw InvalidAdmission(); }
                RequireUniqueAdmissionFields(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray()) { RequireUniqueAdmissionFields(child); }
        }
    }

    private static RepositoryException InvalidAdmission() =>
        new(RepositoryErrorCode.AdmissionRequestInvalid, "Admission request does not satisfy version 1.0.0.");
}
