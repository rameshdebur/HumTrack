using System.Text.Json;
using System.Text.Json.Nodes;
using HumCapture.Coordinator.Repository;

namespace HumCapture.Coordinator.Repository.SelfTest;

internal static class PackageDecodeTests
{
    private static void Require(bool condition, string message)
    { if (!condition) { throw new InvalidOperationException(message); } }

    private static void Released(string root)
    {
        foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        { using var writable = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
    }

    internal static void MissingDecoder()
    {
        PackageInputTests.WithFixture(root =>
        {
            var result = PackageInputEvidence.EvaluateAsync(root, Path.Combine(root, "absent-decoder"), TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Require(result.Input is null && result.Inspection?.Outcome == DecoderExit.Failed
                && result.Inspection.Evidence is null, "Missing decoder returned package evidence.");
            Released(root);
        });
    }

    internal static void PinRefusal()
    {
        PackageInputTests.WithFixture(root =>
        {
            var bin = Path.Combine(Path.GetDirectoryName(root)!, "bin"); Directory.CreateDirectory(bin);
            File.WriteAllText(Path.Combine(bin, "ffmpeg.exe"), "not the pinned executable");
            File.WriteAllText(Path.Combine(bin, "ffprobe.exe"), "not the pinned executable");
            var result = PackageInputEvidence.EvaluateAsync(root, bin, TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Require(result.Input is null && result.Inspection?.Outcome == DecoderExit.Failed
                && result.Inspection.Reason.Contains("identity", StringComparison.Ordinal), "Unpinned decoder was accepted.");
            Released(root);
        });
    }

    internal static void MissingMaster()
    {
        PackageInputTests.WithFixture(root =>
        {
            var result = PackageInputEvidence.EvaluateAsync(root, "unused", TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Require(result.Inspection is null && result.Input is not null
                && result.Input.Evidence.NotAssessed.Contains("MASTER_FULL_DECODE")
                && result.Input.Evidence.NotAssessed.Contains("DECODER_PROVENANCE"), "Missing master was promoted to decode success.");
            Released(root);
        }, "incomplete-survivors");
    }

    internal static void CancellationAndDeadline()
    {
        PackageInputTests.WithFixture(root =>
        {
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try
            {
                PackageInputEvidence.EvaluateAsync(root, "unused", TimeSpan.FromSeconds(5), cancelled.Token).GetAwaiter().GetResult();
                throw new InvalidOperationException("Cancelled package admission returned.");
            }
            catch (OperationCanceledException) { Released(root); }
            foreach (var timeout in new[] { TimeSpan.Zero, TimeSpan.FromSeconds(-1), TimeSpan.FromHours(25) })
            {
                try
                {
                    PackageInputEvidence.EvaluateAsync(root, "unused", timeout).GetAwaiter().GetResult();
                    throw new InvalidOperationException("Invalid timeout accepted.");
                }
                catch (ArgumentOutOfRangeException) { Released(root); }
            }
        });
    }

    // Real pinned binary execution is opt-in, not a hosted-CI or hardware claim.
    internal static int Real(string bin, string mediaDirectory)
    {
        foreach (var name in new[] { "master3.mp4", "different-pts.mp4", "corrupt.mp4", "geometry-mismatch" })
        {
            PackageInputTests.WithFixture(root =>
            {
                var geometryMismatch = name == "geometry-mismatch";
                File.Copy(Path.Combine(mediaDirectory, geometryMismatch ? "master3.mp4" : name), Path.Combine(root, "media/master.mp4"), true);
                if (geometryMismatch)
                {
                    var path = Path.Combine(root, "metadata/camera-metadata.json");
                    var camera = JsonNode.Parse(File.ReadAllBytes(path))!;
                    camera["negotiated_mode"]!["width"] = 640;
                    File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(camera));
                }
                PackageInputTests.Rebind(root);
                var before = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
                    .ToDictionary(path => path, path => PackageInputManifest.Hash(File.ReadAllBytes(path)));
                if (name == "different-pts.mp4" || geometryMismatch)
                {
                    try
                    {
                        PackageInputEvidence.EvaluateAsync(root, bin, TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                        throw new InvalidOperationException("Decoded metadata mismatch was accepted.");
                    }
                    catch (InvalidDataException exception)
                    {
                        Require(exception.Message.Contains(geometryMismatch ? "geometry" : "presentation timestamps", StringComparison.Ordinal),
                            "Package failed before the intended decoder comparison: " + exception.Message);
                        Released(root);
                    }
                }
                else
                {
                    var result = PackageInputEvidence.EvaluateAsync(root, bin, TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                    if (name == "corrupt.mp4")
                    { Require(result.Input is null && result.Inspection?.Outcome == DecoderExit.Failed, "Corrupt master returned package evidence."); }
                    else
                    {
                        Require(result.Inspection?.Outcome == DecoderExit.Decoded && result.Input?.Evidence.Frames?.Association.AcceptedFrames == 3,
                            "Actual decoded package did not bind frame evidence.");
                        Require(!result.Input!.Evidence.NotAssessed.Contains("DECODER_PROVENANCE")
                            && !result.Input.Evidence.NotAssessed.Contains("MASTER_FULL_DECODE")
                            && result.Input.Evidence.NotAssessed.Contains("CADENCE_ACCEPTANCE")
                            && result.Input.Evidence.NotAssessed.Contains("PROTOCOL_ADMISSION"), "Decode was promoted to scientific acceptance.");
                    }
                }
                Require(before.All(item => item.Value == PackageInputManifest.Hash(File.ReadAllBytes(item.Key))), "Source package changed.");
                Released(root);
                Console.WriteLine(JsonSerializer.Serialize(new { Test = "HC-PACKAGE-DECODE-REAL", File = name, Passed = true, SourceUnchanged = true }));
            });
        }
        return 0;
    }
}
