using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using HumCapture.Coordinator.Repository;

namespace HumCapture.Coordinator.Repository.SelfTest;

internal static class PackageInputTests
{
    private static void WithFixture(Action<string> action, string name = "uvc-legacy")
    {
        var parent = Path.Combine(AppContext.BaseDirectory, "package-input-tests", Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, "package"); Directory.CreateDirectory(root);
        try
        {
            using var fixture = JsonDocument.Parse(ImuEvidenceTests.Fixture("package-input-vectors.json"));
            var vector = fixture.RootElement.GetProperty("vectors").EnumerateArray().Single(v => v.GetProperty("name").GetString() == name);
            foreach (var item in vector.GetProperty("files").EnumerateObject())
            {
                var path = Path.Combine(root, item.Name); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, Convert.FromBase64String(item.Value.GetString()!));
            }
            action(root);
        }
        finally { Directory.Delete(parent, true); }
    }
    private static string Hash(string path)
    { using var file = File.OpenRead(path); return Convert.ToHexStringLower(SHA256.HashData(file)); }
    private static JsonNode Manifest(string root) => JsonNode.Parse(File.ReadAllBytes(Path.Combine(root, "package-manifest.json")))!;
    private static byte[] Encode(JsonNode node)
    { using var document = JsonDocument.Parse(node.ToJsonString()); return CaptureCanonicalJson.Encode(document.RootElement); }
    private static void WriteManifest(string root, JsonNode manifest)
    {
        manifest.AsObject().Remove("package_content_sha256");
        manifest["package_content_sha256"] = PackageInputManifest.Hash(Encode(manifest));
        File.WriteAllBytes(Path.Combine(root, "package-manifest.json"), Encode(manifest));
    }
    private static void Rebind(string root)
    {
        var manifest = Manifest(root); ulong total = 0;
        foreach (var item in manifest["artifacts"]!.AsArray())
        {
            var path = Path.Combine(root, item!["relative_path"]!.GetValue<string>());
            var length = new FileInfo(path).Length; total += (ulong)length;
            item["byte_length"] = length.ToString(System.Globalization.CultureInfo.InvariantCulture); item["sha256"] = Hash(path);
        }
        manifest["package_byte_length"] = total.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var inventory = string.Concat(manifest["artifacts"]!.AsArray().Select(a => $"{a!["relative_path"]!.GetValue<string>()}\t{a["byte_length"]!.GetValue<string>()}\t{a["sha256"]!.GetValue<string>()}\n").Order(StringComparer.Ordinal));
        manifest["artifact_set_sha256"] = PackageInputManifest.Hash(Encoding.UTF8.GetBytes(inventory));
        WriteManifest(root, manifest);
    }
    private static DecodedMediaEvidence Observe(string path, CancellationToken token)
    { token.ThrowIfCancellationRequested(); return new DecodedMediaEvidence(Hash(path), new FileInfo(path).Length, FrameMetadataTests.Video()); }
    private static void Reject(Action action)
    {
        try { action(); } catch (Exception e) when (e is InvalidDataException or IOException or RepositoryException or UnauthorizedAccessException) { return; }
        throw new InvalidOperationException("Invalid package input was accepted.");
    }
    private static void Blocked(Action action)
    {
        try { action(); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return; }
        throw new InvalidOperationException("Mutation succeeded while input lease was active.");
    }
    internal static void FileToEvidence()
    {
        foreach (var name in new[] { "uvc-legacy", "android-exact" })
        {
            WithFixture(root =>
            {
                var before = Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(p => p, Hash);
                var result = PackageInputEvidence.Evaluate(root, Observe);
                if (result.Evidence.Frames?.Association.AcceptedFrames != 3 || result.BoundArtifactCount != (name == "uvc-legacy" ? 6 : 8)
                    || !result.Evidence.NotAssessed.Contains("PROTOCOL_ADMISSION") || !result.Evidence.NotAssessed.Contains("DECODER_PROVENANCE"))
                { throw new InvalidOperationException("Package adapter evidence mismatch."); }
                if (name == "android-exact" && (result.Evidence.Imu?.MappedSamplesCompared != 3 || result.Evidence.Frames.MappedFramesCompared != 3))
                { throw new InvalidOperationException("Manifest 1.1 dispatch lost exact comparisons."); }
                if (name == "uvc-legacy" && (result.Evidence.Imu is not null || result.Evidence.Frames.MappedTimesRecomputed))
                { throw new InvalidOperationException("UVC acquired IMU or legacy timing upgraded."); }
                if (before.Any(p => p.Value != Hash(p.Key))) { throw new InvalidOperationException("Input files changed."); }
            }, name);
        }
        WithFixture(root =>
        {
            var result = PackageInputEvidence.Evaluate(root);
            if (result.Evidence.Frames is not null || !result.Evidence.NotAssessed.Contains("MASTER_FULL_DECODE"))
            { throw new InvalidOperationException("No decoder observations became a PASS."); }
        });
    }
    internal static void ManifestRejections()
    {
        foreach (var change in new Action<JsonNode>[]
        {
            m => m["schema_version"] = "2.0.0",
            m => m["interface_profiles"] = JsonNode.Parse("[\"HC-IF-ART-001@1.0.0\",\"HC-IF-TIM-001@9.0.0\"]"),
            m => m["interface_profiles"]!.AsArray().Add("HC-IF-TIM-001@1.1.0"),
            m => m.AsObject().Remove("interface_profiles"),
            m => m["subject_id"] = "not-an-id",
            m => m["artifact_count"] = 255,
            m => m["package_byte_length"] = "0",
            m => m["artifact_set_sha256"] = new string('0', 64),
            m => m["unexpected"] = true,
            m => m["artifacts"]![0]!["capture_attempt_id"] = Guid.NewGuid().ToString(),
            m => m["artifacts"]![0]!["artifact_id"] = m["artifacts"]![1]!["artifact_id"]!.DeepClone(),
            m => m["artifacts"]![0]!["relative_path"] = "../escape.json",
            m => m["artifacts"]![0]!["relative_path"] = "CON.json",
            m => m["artifacts"]![0]!["required"] = false,
            m => m["artifacts"]![0]!["format_version"] = "2.0.0",
            m => m["artifacts"]![0]!["media_type"] = "text/plain",
            m => m["artifacts"]![4]!["format_version"] = "1.1.0",
            m => m["artifacts"]![4]!["role"] = "CAMERA_METADATA",
            m => m["artifacts"]![4]!["role"] = "CALIBRATION"
        })
        { WithFixture(root => { var manifest = Manifest(root); change(manifest); WriteManifest(root, manifest); Reject(() => PackageInputEvidence.Evaluate(root)); }); }
        WithFixture(root => { File.AppendAllText(Path.Combine(root, "package-manifest.json"), " "); Reject(() => PackageInputEvidence.Evaluate(root)); });
        WithFixture(root =>
        {
            var manifest = Manifest(root);
            while (manifest["artifacts"]!.AsArray().Count < 257) { manifest["artifacts"]!.AsArray().Add(manifest["artifacts"]![0]!.DeepClone()); }
            manifest["artifact_count"] = 257; WriteManifest(root, manifest); Reject(() => PackageInputEvidence.Evaluate(root));
        });
    }
    internal static void InventoryAndContentFailures()
    {
        foreach (var change in new Action<string>[]
        {
            root => File.Delete(Path.Combine(root, "media/master.mp4")),
            root => File.WriteAllText(Path.Combine(root, "extra.txt"), "undeclared"),
            root => File.AppendAllText(Path.Combine(root, "metadata/camera-metadata.json"), " "),
            root => { var path = Path.Combine(root, "timing/frame-timestamps.bin"); var bytes = File.ReadAllBytes(path); bytes[^1] ^= 1; File.WriteAllBytes(path, bytes); Rebind(root); },
            root => { File.WriteAllBytes(Path.Combine(root, "metadata/camera-metadata.json"), [0xff, 0xfe]); Rebind(root); },
            root => { var path = Path.Combine(root, "metadata/timing-metadata.json"); var node = JsonNode.Parse(File.ReadAllBytes(path))!; node["capture_attempt_id"] = Guid.NewGuid().ToString(); File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(node)); Rebind(root); }
        }) { WithFixture(root => { change(root); Reject(() => PackageInputEvidence.Evaluate(root, Observe)); }); }
        WithFixture(root => Reject(() => PackageInputEvidence.Evaluate(root, (path, token) => Observe(path, token) with { MediaSha256 = new string('0', 64) })));
    }
    internal static void LiveLeaseMutations()
    {
        WithFixture(root =>
        {
            _ = PackageInputEvidence.Evaluate(root, (path, token) =>
            {
                Blocked(() => { using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); });
                Blocked(() => File.Delete(path));
                Blocked(() => File.Move(path, path + ".moved"));
                Blocked(() => Directory.Move(root, root + "-moved"));
                Blocked(() => File.AppendAllText(Path.Combine(root, "package-manifest.json"), " "));
                return Observe(path, token);
            });
            using var released = new FileStream(Path.Combine(root, "media/master.mp4"), FileMode.Open, FileAccess.Write, FileShare.None);
        });
        WithFixture(root => Reject(() => PackageInputEvidence.Evaluate(root, (path, token) =>
        { File.WriteAllText(Path.Combine(root, "late.txt"), "created during verification"); return Observe(path, token); })));
        WithFixture(root =>
        {
            using (var writer = new FileStream(Path.Combine(root, "media/master.mp4"), FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
            { Reject(() => PackageInputEvidence.Evaluate(root)); }
            _ = PackageInputEvidence.Evaluate(root, Observe); // Failed acquisition released every earlier handle.
        });
    }
    internal static void CancellationAndFailureRelease()
    {
        WithFixture(root =>
        {
            try { PackageInputEvidence.Evaluate(root, token: new CancellationToken(true)); }
            catch (OperationCanceledException) { return; }
            throw new InvalidOperationException("Pre-cancelled input acquired a lease.");
        });
        WithFixture(root =>
        {
            using var stop = new CancellationTokenSource();
            try { PackageInputEvidence.Evaluate(root, (path, token) => { stop.Cancel(); return null; }, stop.Token); }
            catch (OperationCanceledException) { using var released = File.OpenWrite(Path.Combine(root, "media/master.mp4")); return; }
            throw new InvalidOperationException("Cancellation ignored.");
        });
        WithFixture(root =>
        {
            Reject(() => PackageInputEvidence.Evaluate(root, (_, _) => throw new IOException("synthetic decoder failure")));
            _ = PackageInputEvidence.Evaluate(root, Observe);
        });
    }
    internal static void SurvivorsAndStreaming()
    {
        WithFixture(root =>
        {
            var result = PackageInputEvidence.Evaluate(root, (_, _) => throw new InvalidOperationException("No master exists."));
            if (result.BoundArtifactCount != 2 || result.Evidence.Finalization.FinalizationOutcome != "FINALIZED_INCOMPLETE" || result.Evidence.Frames is not null)
            { throw new InvalidOperationException("Survivor package was promoted or discarded."); }
        }, "incomplete-survivors");
        WithFixture(root =>
        {
            using (var video = File.OpenWrite(Path.Combine(root, "media/master.mp4"))) { video.SetLength(129L * 1024 * 1024); }
            Rebind(root); _ = PackageInputEvidence.Evaluate(root); // Video exceeds sidecar budget; hash is streamed.
        });
        WithFixture(root =>
        {
            using (var metadata = File.OpenWrite(Path.Combine(root, "metadata/camera-metadata.json"))) { metadata.SetLength(16L * 1024 * 1024 + 1); }
            Rebind(root); Reject(() => PackageInputEvidence.Evaluate(root));
        });
    }
    internal static void HardLinks()
    {
        WithFixture(root =>
        {
            var info = new ProcessStartInfo("fsutil.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "hardlink", "create", Path.Combine(Path.GetDirectoryName(root)!, "alias.mp4"), Path.Combine(root, "media/master.mp4") }) { info.ArgumentList.Add(arg); }
            using var process = Process.Start(info)!; process.WaitForExit();
            if (process.ExitCode != 0) { throw new InvalidOperationException("Hard-link test setup failed: " + process.StandardError.ReadToEnd()); }
            Reject(() => PackageInputEvidence.Evaluate(root));
        });
    }

    internal static void ReparsePathsAndSurvivorSidecars()
    {
        WithFixture(root =>
        {
            var alias = Path.Combine(Path.GetDirectoryName(root)!, "junction");
            var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("/d"); start.ArgumentList.Add("/c");
            start.ArgumentList.Add("mklink"); start.ArgumentList.Add("/J"); start.ArgumentList.Add(alias); start.ArgumentList.Add(root);
            using var process = Process.Start(start)!; process.WaitForExit();
            if (process.ExitCode != 0) { throw new InvalidOperationException("Junction test setup failed: " + process.StandardError.ReadToEnd()); }
            try { Reject(() => PackageInputEvidence.Evaluate(alias)); }
            finally { Directory.Delete(alias); }
        });
        WithFixture(root =>
        {
            // Optional retained material is integrity-bound but not semantically accepted.
            File.WriteAllText(Path.Combine(root, "extra.txt"), "optional survivor");
            var manifest = Manifest(root); var item = manifest["artifacts"]![0]!.DeepClone();
            item["artifact_id"] = Guid.NewGuid().ToString(); item["relative_path"] = "extra.txt";
            item["role"] = "AUXILIARY_EVIDENCE"; item["media_type"] = "text/plain"; item["required"] = false;
            manifest["artifacts"]!.AsArray().Add(item); manifest["artifact_count"] = 3;
            WriteManifest(root, manifest); Rebind(root);
            var result = PackageInputEvidence.Evaluate(root);
            if (!result.Evidence.NotAssessed.Any(s => s.StartsWith("ARTIFACT_SEMANTICS:", StringComparison.Ordinal)))
            { throw new InvalidOperationException("Optional survivor became semantic PASS."); }
        }, "incomplete-survivors");
    }
}
