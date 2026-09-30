using System.Text.Json;
using System.Text.Json.Nodes;
using System.Buffers.Binary;
using Microsoft.Data.Sqlite;
using HumCapture.Coordinator.Repository;

namespace HumCapture.Coordinator.Repository.SelfTest;

internal static class CoverageVerificationTests
{
    private static readonly DateTimeOffset Audit = new(2026, 9, 23, 12, 0, 0, TimeSpan.Zero);
    private static void Require(bool condition, string reason)
    { if (!condition) { throw new InvalidOperationException(reason); } }
    private static void Invalid(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Invalid evidence was accepted.");
    }
    internal static void SharedCoverage()
    {
        foreach (var name in new[] { "uvc-covered", "android-covered" })
        {
            PackageInputTests.WithFixture(root =>
            {
                var result = PackageInputEvidence.Evaluate(root);
                Require(result.Coverage?.Disposition == "PASS" && !result.Evidence.NotAssessed.Contains("TIMING_COVERAGE"),
                    "Native coverage did not match independent JS fixture.");
                Require(result.Evidence.NotAssessed.Contains("DECODER_PROVENANCE"), "Coverage promoted synthetic media to decoder evidence.");
            }, name);
        }
    }
    internal static void CoverageMutations()
    {
        foreach (var field in new[] { "stream_id", "clock_id", "ticks_per_second", "record_count", "last_ticks", "lane_id", "run_index", "segment_id", "sequence_gap_count" })
        {
            PackageInputTests.WithFixture(root =>
            {
                var manifest = PackageInputTests.Manifest(root);
                var coverage = manifest["artifacts"]!.AsArray().Single(a => a!["role"]!.GetValue<string>() == "IMU_SAMPLES")!["timing_coverage"]!;
                if (field is "stream_id" or "clock_id") { coverage[field] = Guid.NewGuid().ToString("D"); }
                else if (field == "ticks_per_second") { coverage[field] = 1000; }
                else if (field == "record_count") { coverage[field] = "4"; }
                else if (field is "lane_id" or "run_index" or "segment_id") { coverage["spans"]![0]![field] = 12; }
                else { coverage["spans"]![0]![field] = "123"; }
                PackageInputTests.WriteManifest(root, manifest);
                Invalid(() => PackageInputEvidence.Evaluate(root));
            }, "android-covered");
        }
    }
    internal static void LegacyAndEmpty()
    {
        PackageInputTests.WithFixture(root =>
        {
            var evidence = PackageInputEvidence.Evaluate(root);
            Require(evidence.Coverage?.Disposition == "NOT_ASSESSED", "Legacy bad IMU summary promoted.");
        }, "android-exact");
        var value = TimingCoverageEvidence.Build(Guid.NewGuid(), Guid.NewGuid(), 1000, []);
        Require(value.GetProperty("record_count").GetString() == "0" && value.GetProperty("spans").GetArrayLength() == 0, "Invented empty endpoints.");
        CoverageSample[] samples = [new(1, 0, 0, 10), new(2, 0, 0, 1), new(1, 0, 2, 20),
            new(1, 1, 3, 2), new(2, 0, 1, 2), new(1, 1, 0, 1)];
        var spans = TimingCoverageEvidence.Build(Guid.NewGuid(), Guid.NewGuid(), 1000, samples).GetProperty("spans");
        Require(spans.GetArrayLength() == 4 && spans[0].GetProperty("sequence_gap_count").GetString() == "1"
            && spans[3].GetProperty("record_count").GetString() == "2", "Interleaving/regression was flattened.");
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { TimingCoverageEvidence.Build(Guid.NewGuid(), Guid.NewGuid(), 1000, samples, cancelled.Token); }
        catch (OperationCanceledException) { return; }
        throw new InvalidOperationException("Coverage ignored cancellation.");
    }
    internal static void RecordFailure()
    {
        PackageInputTests.WithFixture(root =>
        {
            var result = PackageVerificationProducer.VerifyAsync(root, Path.Combine(root, "absent"), TimeSpan.FromSeconds(5),
                Guid.NewGuid(), Audit).GetAwaiter().GetResult();
            Require(result.Outcome == "FAILED" && result.Sha256 == PackageInputManifest.Hash(result.Utf8.Span), "Missing decoder produced verified record.");
            var record = new MetadataSchemaValidator().Validate(MetadataKind.Verification, result.Utf8);
            Require(record.GetProperty("checks").GetArrayLength() == 12 && record.GetProperty("artifact_results").GetArrayLength() == 6,
                "Verification inventory incomplete.");
            Require(record.GetProperty("checks").EnumerateArray().Single(c => c.GetProperty("check").GetString() == "MASTER_FULL_DECODE")
                .GetProperty("disposition").GetString() == "NOT_ASSESSED", "Unavailable decoder claimed measured corruption.");
            foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            { using var writable = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
        });
    }
    private static VerificationAdmissionRequest Request(Guid package) => new(Guid.NewGuid(), package,
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Audit);

    private static void WithStaging(string fixture, Action<string, string, RepositoryService, VerificationAdmissionRequest> action)
    {
        PackageInputTests.WithFixture(source =>
        {
            var project = new DirectoryInfo(AppContext.BaseDirectory);
            while (project.Name != "HumCapture" || !File.Exists(Path.Combine(project.FullName, "AGENTS.md")))
            { project = project.Parent ?? throw new InvalidOperationException("HumCapture test scope not found."); }
            var repository = Path.Combine(project.FullName, "evidence-vault", "verification-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(repository);
            try
            {
                var service = new RepositoryService(); _ = service.Initialize(repository, "TEST\\operator");
                var request = Request(Guid.Parse(PackageInputTests.Manifest(source)["package_id"]!.GetValue<string>()));
                var staging = Path.Combine(repository, "staging", request.CollectionAttemptId.ToString("D"), request.PackageId.ToString("D"));
                Directory.CreateDirectory(Path.GetDirectoryName(staging)!); Directory.Move(source, staging);
                action(repository, staging, service, request);
            }
            finally { Directory.Delete(repository, true); }
        }, fixture);
    }
    internal static void SurvivorAdmission()
    {
        WithStaging("incomplete-survivors", (root, staging, service, request) =>
        {
            var first = service.VerifyAndAdmitAsync(root, request, "unused", TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            var replay = service.VerifyAndAdmitAsync(root, request, "unused", TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Require(first.Verification.Outcome == "VERIFIED" && first.Admission?.State == "STAGED_VERIFIED"
                && replay.Admission?.WasAlreadyPresent == true && first.Verification.Sha256 == replay.Verification.Sha256, "Staged survivor retry differs.");
            Require(Directory.Exists(staging) && !Directory.Exists(Path.Combine(root, first.Admission!.DestinationRelativePath)),
                "Admission moved incomplete data.");
            Require(first.Verification.Evaluation.Input!.Evidence.Finalization.FinalizationOutcome == "FINALIZED_INCOMPLETE",
                "Custody fabricated complete capture.");
            try
            {
                _ = service.VerifyAndAdmitAsync(root, request with { AuditTime = Audit.AddSeconds(1) }, "unused", TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
                throw new InvalidOperationException("Changed immutable record overwrote original.");
            }
            catch (RepositoryException exception)
            { Require(exception.Code == RepositoryErrorCode.ImmutableRecordConflict, "Unexpected conflict result."); }
        });
    }
    internal static void FailedAdmissionAndCancellation()
    {
        WithStaging("uvc-covered", (root, staging, service, request) =>
        {
            var result = service.VerifyAndAdmitAsync(root, request, "absent", TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
            Require(result.Verification.Outcome == "FAILED" && result.Admission is null && Directory.Exists(staging),
                "Failed verification entered repository.");
            Require(!Directory.EnumerateFiles(Path.Combine(root, "subjects"), "*", SearchOption.AllDirectories).Any(),
                "Failed verification published a milestone.");
            using var database = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = Path.Combine(root, RepositoryConstants.CatalogRelativePath), Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
            database.Open();
            using var query = database.CreateCommand();
            query.CommandText = "SELECT count(*) FROM repository_transactions";
            Require(Convert.ToInt64(query.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) == 0,
                "Failed verification advanced the journal.");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { _ = service.VerifyAndAdmitAsync(root, request, "absent", TimeSpan.FromSeconds(5), cancelled.Token).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { return; }
            throw new InvalidOperationException("Cancelled verification returned success.");
        });
    }
    internal static void VersionAndBounds()
    {
        Invalid(() => TimingCoverageEvidence.Build(Guid.NewGuid(), Guid.NewGuid(), 1000,
            Enumerable.Range(0, TimingCoverageEvidence.MaximumSpans + 1)
                .Select(n => new CoverageSample(0, (uint)n, (ulong)n, (ulong)n))));
        foreach (var field in new[] { "schema", "profile", "extra", "overflow", "order", "empty", "untimed" })
        {
            PackageInputTests.WithFixture(root =>
            {
                var m = PackageInputTests.Manifest(root);
                var a = m["artifacts"]!.AsArray().First(x => x!["timing_coverage"] is not null)!;
                if (field == "schema") { m["schema_version"] = "1.2.0"; }
                else if (field == "untimed") { m["artifacts"]![0]!["timing_coverage"] = a["timing_coverage"]!.DeepClone(); }
                else if (field == "profile") { m["interface_profiles"]!.AsArray().Add("HC-IF-XFR-001@1.2.0"); }
                else if (field == "extra") { a["timing_coverage"]!["invented"] = true; }
                else if (field == "overflow") { a["timing_coverage"]!["record_count"] = "18446744073709551616"; }
                else if (field == "order") { a["timing_coverage"]!["spans"]![0]!["run_index"] = 1; }
                else { a["timing_coverage"]!["spans"] = new JsonArray(); }
                PackageInputTests.WriteManifest(root, m);
                Invalid(() => PackageInputEvidence.Evaluate(root));
            }, "uvc-covered");
        }
        PackageInputTests.WithFixture(root =>
        {
            var m = PackageInputTests.Manifest(root); m["schema_version"] = "1.1.0";
            PackageInputTests.WriteManifest(root, m); Invalid(() => PackageInputEvidence.Evaluate(root));
        });
    }
    internal static void AcceptedMasterOnly()
    {
        PackageInputTests.WithFixture(root =>
        {
            var path = Path.Combine(root, "timing/frame-timestamps.bin");
            var bytes = File.ReadAllBytes(path);
            var second = bytes.AsSpan(64 + 96, 96);
            BinaryPrimitives.WriteUInt16LittleEndian(second[68..], 2);
            BinaryPrimitives.WriteUInt32LittleEndian(second[92..], TimingBinaryReader.Crc32C(second[..92]));
            File.WriteAllBytes(path, bytes);
            var cameraPath = Path.Combine(root, "metadata/camera-metadata.json");
            var camera = JsonNode.Parse(File.ReadAllBytes(cameraPath))!;
            camera["observed_timing"]!["accepted_frame_count"] = 2;
            File.WriteAllBytes(cameraPath, JsonSerializer.SerializeToUtf8Bytes(camera));
            PackageInputTests.Rebind(root);
            Invalid(() => PackageInputEvidence.Evaluate(root));
            var manifest = PackageInputTests.Manifest(root);
            var coverage = manifest["artifacts"]!.AsArray().Single(a => a!["role"]!.GetValue<string>() == "SCIENTIFIC_MASTER_VIDEO")!["timing_coverage"]!;
            coverage["record_count"] = "2"; coverage["spans"]![0]!["record_count"] = "2";
            coverage["spans"]![0]!["sequence_gap_count"] = "1";
            PackageInputTests.WriteManifest(root, manifest);
            Require(PackageInputEvidence.Evaluate(root).Coverage?.Disposition == "PASS",
                "Master coverage did not distinguish accepted frames from all source frames.");
        }, "uvc-covered");
    }
    internal static int Real(string bin, string media)
    {
        WithStaging("uvc-covered", (root, staging, service, request) =>
        {
            File.Copy(Path.Combine(media, "master3.mp4"), Path.Combine(staging, "media/master.mp4"), true);
            PackageInputTests.Rebind(staging);
            var protocol = ScientificEvidenceTests.Context(staging);
            var before = Directory.GetFiles(staging, "*", SearchOption.AllDirectories).ToDictionary(p => p, p => PackageInputManifest.Hash(File.ReadAllBytes(p)));
            var result = service.VerifyAndAdmitAsync(root, request, bin, TimeSpan.FromSeconds(30), protocol: protocol).GetAwaiter().GetResult();
            Require(result.Verification.Outcome == "VERIFIED" && result.Admission?.State == "STAGED_VERIFIED"
                && result.Verification.Evaluation.Input?.Scientific?.Protocol.Outcome == "CONFORMANT", "Actual decoder did not admit covered package.");
            var replay = service.VerifyAndAdmitAsync(root, request, bin, TimeSpan.FromSeconds(30), protocol: protocol).GetAwaiter().GetResult();
            Require(replay.Admission?.WasAlreadyPresent == true, "Real decoder replay not idempotent.");
            Require(before.All(p => PackageInputManifest.Hash(File.ReadAllBytes(p.Key)) == p.Value), "Source changed during admission.");
            Require(!Directory.Exists(Path.Combine(root, result.Admission!.DestinationRelativePath)), "Admission moved master.");
            Console.WriteLine("PASS HC-C14UVW-REAL covered master verified and staged; exact retry; source unchanged; no move");
        });
        PackageInputTests.WithFixture(root =>
        {
            File.Copy(Path.Combine(media, "master3.mp4"), Path.Combine(root, "media/master.mp4"), true); PackageInputTests.Rebind(root);
            var result = PackageVerificationProducer.VerifyAsync(root, bin, TimeSpan.FromSeconds(30), Guid.NewGuid(), Audit).GetAwaiter().GetResult();
            Require(result.Outcome == "FAILED" && result.Evaluation.Input?.Coverage?.Disposition == "NOT_ASSESSED",
                "Successful decode silently upgraded legacy coverage.");
            Console.WriteLine("PASS HC-C14UVW-REAL legacy successful decode retains NOT_ASSESSED coverage");
        });
        PackageInputTests.WithFixture(root =>
        {
            File.Copy(Path.Combine(media, "master3.mp4"), Path.Combine(root, "media/master.mp4"), true); PackageInputTests.Rebind(root);
            var context = ScientificEvidenceTests.Context(root);
            var snapshot = JsonNode.Parse(context.SnapshotBytes.Span)!;
            foreach (var rule in snapshot["scientific_rules"]!.AsArray())
            {
                if (rule!["metric"]!.GetValue<string>() == "OBSERVED_MILLIHZ")
                { rule["level"] = "REQUIRED"; rule["operator"] = "GE"; rule["threshold"] = 1000000; }
            }
            context = ScientificEvidenceTests.Bind(root, snapshot);
            var result = PackageVerificationProducer.VerifyAsync(root, bin, TimeSpan.FromSeconds(30), Guid.NewGuid(), Audit,
                protocol: context).GetAwaiter().GetResult();
            Require(result.Outcome == "VERIFIED" && result.Evaluation.Input?.Scientific?.Protocol.Outcome == "REJECTED",
                "Custody verification conflated with scientific quality.");
            using var record = JsonDocument.Parse(result.Utf8);
            Require(record.RootElement.GetProperty("checks").EnumerateArray()
                .SelectMany(c => c.GetProperty("evidence_references").EnumerateArray())
                .Any(e => e.GetString() == "scientific-outcome:REJECTED"), "Record lost scientific rejection evidence.");
            Console.WriteLine("PASS HC-C14UVW-REAL verified custody retains protocol REJECTED independently");
        }, "uvc-covered");
        foreach (var file in new[] { "corrupt.mp4", "different-pts.mp4" })
        {
            PackageInputTests.WithFixture(root =>
            {
                File.Copy(Path.Combine(media, file), Path.Combine(root, "media/master.mp4"), true); PackageInputTests.Rebind(root);
                if (file == "different-pts.mp4")
                { Invalid(() => PackageVerificationProducer.VerifyAsync(root, bin, TimeSpan.FromSeconds(30), Guid.NewGuid(), Audit).GetAwaiter().GetResult()); }
                else
                {
                    var result = PackageVerificationProducer.VerifyAsync(root, bin, TimeSpan.FromSeconds(30), Guid.NewGuid(), Audit).GetAwaiter().GetResult();
                    Require(result.Outcome == "FAILED", "Corrupt media verified.");
                }
                Console.WriteLine("PASS HC-C14UVW-REAL refusal " + file);
            }, "uvc-covered");
        }
        return 0;
    }
}
