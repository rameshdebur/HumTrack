using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;
using HumCapture.Coordinator.Repository;

namespace HumCapture.Coordinator.Repository.SelfTest;

internal static class ScientificEvidenceTests
{
    private static JsonNode Vectors() => JsonNode.Parse(ImuEvidenceTests.Fixture("scientific-rules-vectors.json"))!;
    private static JsonNode Snapshot() => Vectors()["protocol"]!.DeepClone();
    internal static ProtocolEvidenceContext Context(string root) => Bind(root, Snapshot());
    private static void Require(bool value, string message)
    { if (!value) { throw new InvalidOperationException(message); } }
    private static void Reject(Action action)
    {
        try { action(); } catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Invalid scientific evidence accepted.");
    }
    private static byte[] Encode(JsonNode node)
    { using var document = JsonDocument.Parse(node.ToJsonString()); return CaptureCanonicalJson.Encode(document.RootElement); }
    internal static ProtocolEvidenceContext Bind(string root, JsonNode snapshot)
    {
        snapshot.AsObject().Remove("snapshot_content_sha256");
        snapshot["snapshot_content_sha256"] = PackageInputManifest.Hash(Encode(snapshot));
        var manifest = PackageInputTests.Manifest(root);
        manifest["protocol_snapshot_id"] = snapshot["protocol_snapshot_id"]!.DeepClone();
        manifest["protocol_snapshot_content_sha256"] = snapshot["snapshot_content_sha256"]!.DeepClone();
        PackageInputTests.WriteManifest(root, manifest);
        return new(Encode(snapshot), Guid.Parse(manifest["source_id"]!.GetValue<string>()),
            Guid.Parse(snapshot["source_roles"]![0]!["role_id"]!.GetValue<string>()));
    }
    private static DecodedMediaEvidence Observe(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return new(PackageInputManifest.Hash(File.ReadAllBytes(path)), new FileInfo(path).Length, FrameMetadataTests.Video());
    }
    private static SourceFrame Frame(ulong sequence, ulong ticks, uint segment = 0, uint model = 0) =>
        new(sequence, ticks, null, null, model == 0 ? null : ticks + 4000000000, null, null, model, 250000, segment, 1, null, null);
    private static readonly Guid Attempt = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid FrameId = Guid.Parse("10000000-0000-4000-8000-000000000002");
    private static readonly Guid ImuId = Guid.Parse("10000000-0000-4000-8000-000000000003");
    private static TimingClockBindings Bindings()
    {
        var timing = FrameMetadataTests.Timing();
        var second = timing["clock_models"]![0]!.DeepClone(); second["model_id"] = 2;
        timing["clock_models"]!.AsArray().Add(second);
        timing["streams"]![1]!["native_clock_id"] = timing["streams"]![0]!["native_clock_id"]!.DeepClone();
        using var document = JsonDocument.Parse(timing.ToJsonString());
        return new(document.RootElement.Clone(), TimingMetadataVersion.LegacyV1, default);
    }
    private static ClockContinuityEvidence Continuity(params SourceFrame[] frames) =>
        ClockModelContinuity.Check(Bindings(), new(FrameId, Attempt, frames), null);

    internal static void SharedConditions()
    {
        foreach (var vector in Vectors()["cases"]!.AsArray())
        {
            var conditions = vector!["conditions"]!.AsArray().Select(c => new ScientificCondition("RULE", "METRIC",
                c!["level"]!.GetValue<string>(), c["outcome"]!.GetValue<string>(), "SYNTHETIC")).ToArray();
            Require(ProtocolScientificEvidence.Aggregate(conditions) == vector["expected"]!.GetValue<string>(), "Shared policy mismatch.");
        }
        Reject(() => ProtocolScientificEvidence.Aggregate([new("RULE", "METRIC", "REQUIRED", "NOT_APPLICABLE", "NONE")]));
    }

    internal static void ExactCadence()
    {
        var offset = 9007199254740993UL;
        var measured = SourceCadenceEvidence.Measure([Frame(1, offset), Frame(2, offset + 33333333), Frame(3, offset + 66666666)], 1000000000);
        Require(measured.ObservedMilliHz!.Compare(30000) > 0 && measured.ObservedMilliHz.Compare(30001) < 0,
            "Exact high-tick cadence lost precision.");
        Require(measured.MaximumIntervalUs!.Compare(33333) > 0 && measured.MaximumIntervalUs.Compare(33334) < 0, "Interval rounded across threshold.");
        measured = SourceCadenceEvidence.Measure([Frame(1, 10), Frame(4, 20), Frame(4, 20), Frame(2, 15), Frame(1, 100, 1)], 10);
        Require(measured.SequenceGaps == 2 && measured.SequenceDuplicates == 1 && measured.SequenceRegressions == 1
            && measured.TimestampRegressions == 1 && measured.TimestampDuplicates == 1 && measured.SegmentChanges == 1
            && measured.ObservedMilliHz is null, "Discontinuity cadence was stitched or hidden.");
        Require(SourceCadenceEvidence.Measure([], 1).ObservedMilliHz is null
            && SourceCadenceEvidence.Measure([Frame(1, 0)], 1).MaximumIntervalUs is null, "Missing intervals became zero evidence.");
        Reject(() => SourceCadenceEvidence.Measure([], 0));
        Require(SourceCadenceEvidence.Measure([Frame(1, 0), Frame(2, 2), Frame(3, 5)], 10).ObservedMilliHz!.Compare(4000) == 0,
            "Variable-interval native cadence was replaced by a nominal rate.");
    }

    internal static void ProtocolAndLegacy()
    {
        PackageInputTests.WithFixture(root =>
        {
            var snapshot = Snapshot(); var context = Bind(root, snapshot);
            var result = PackageInputEvidence.Evaluate(root, Observe, protocol: context);
            Require(result.Scientific!.Protocol.Outcome == "CONFORMANT" && result.Scientific.ClockContinuity!.MappedRecordsChecked == 3,
                "Bound protocol measurements did not pass.");
            Require(result.Evidence.NotAssessed.Contains("PROTOCOL_ADMISSION") && result.Evidence.NotAssessed.Contains("DECODER_PROVENANCE"),
                "Internal evidence promoted to admission or trusted decode.");
            Require(PackageInputEvidence.Evaluate(root, protocol: context).Scientific!.Protocol.Outcome == "REVIEW_REQUIRED", "Negotiated width substituted for decoded width.");
            snapshot["scientific_rules"]![0]!["threshold"] = 60000;
            Require(PackageInputEvidence.Evaluate(root, Observe, protocol: Bind(root, snapshot)).Scientific!.Protocol.Outcome == "REJECTED", "Measured rule failure not rejected.");
            snapshot["schema_version"] = "1.0.0"; snapshot.AsObject().Remove("scientific_rules");
            Require(PackageInputEvidence.Evaluate(root, Observe, protocol: Bind(root, snapshot)).Scientific!.Protocol.Outcome == "REVIEW_REQUIRED", "Legacy rules inferred.");
            Require(PackageInputEvidence.Evaluate(root, Observe).Scientific!.Protocol.Outcome == "REVIEW_REQUIRED", "Absent protocol automatically passed.");
        });
    }

    internal static void ProtocolGuards()
    {
        PackageInputTests.WithFixture(root =>
        {
            var context = Bind(root, Snapshot());
            Reject(() => PackageInputEvidence.Evaluate(root, Observe, protocol: context with { SourceId = Guid.NewGuid() }));
            Reject(() => PackageInputEvidence.Evaluate(root, Observe, protocol: context with { RoleId = Guid.NewGuid() }));
            var tampered = JsonNode.Parse(context.SnapshotBytes.Span)!; tampered["scientific_rules"]![0]!["threshold"] = 1;
            Reject(() => PackageInputEvidence.Evaluate(root, Observe, protocol: context with { SnapshotBytes = Encode(tampered) }));
            var manifest = PackageInputTests.Manifest(root); manifest["protocol_snapshot_content_sha256"] = new string('a', 64);
            PackageInputTests.WriteManifest(root, manifest);
            Reject(() => PackageInputEvidence.Evaluate(root, Observe, protocol: context));
            foreach (var change in new Action<JsonNode>[]
            {
                p => p["scientific_rules"]!.AsArray().Add(p["scientific_rules"]![0]!.DeepClone()),
                p => p["scientific_rules"]![0]!["role_id"] = Guid.NewGuid().ToString(),
                p => p["scientific_rules"]![0]!["metric"] = "UNSUPPORTED",
                p => p["scientific_rules"]![0]!["threshold"] = "thirty",
                p => p["scientific_rules"]![0]!["threshold"] = -1,
                p => p["scientific_rules"]![0]!["metric"] = "RATE_CONTROL_CLASS",
                p => p["source_roles"]![0]!["allowed_source_kinds"] = new JsonArray("ANDROID_CAMERA2")
            })
            {
                var snapshot = Snapshot(); change(snapshot); var invalid = Bind(root, snapshot);
                Reject(() => PackageInputEvidence.Evaluate(root, Observe, protocol: invalid));
            }
        });
    }

    internal static void ModelBarriers()
    {
        Require(Continuity(Frame(1, 1000000000, model: 1), Frame(2, 1100000000, model: 1)).MappedRecordsChecked == 2, "Continuous model rejected.");
        Reject(() => Continuity(Frame(1, 1000000000, model: 1), Frame(2, 1000000000, 1, 1)));
        Reject(() => Continuity(Frame(1, 1100000000, model: 1), Frame(2, 1000000000, model: 1)));
        Reject(() => Continuity(Frame(2, 1000000000, model: 1), Frame(1, 1100000000, model: 1)));
        Reject(() => Continuity(Frame(1, 1000000000, model: 1), Frame(1, 1000000000, 1), Frame(2, 1100000000, 1, 1)));
        Require(Continuity(Frame(1, 1100000000, model: 1), Frame(1, 1000000000, 1, 2)).Barriers == 1, "New segment/model did not reconcile.");
        Reject(() => Continuity(Frame(1, 1000000000), Frame(1, 1000000000, 1), Frame(2, 1100000000)));
    }

    internal static void IndependentImuLanes()
    {
        static ImuSample Sample(ushort sensor, ulong sequence, ulong ticks, uint segment = 0, uint model = 1) =>
            new(sequence, ticks, null, ticks + 4000000000, 0, 0, 0, null, model, 250000, segment, sensor, 1, 0, null);
        var interleaved = new TimingStream<ImuSample>(ImuId, Attempt,
            [Sample(1, 1, 1100000000), Sample(2, 1, 1000000000), Sample(1, 2, 1200000000), Sample(2, 2, 1150000000)]);
        var result = ClockModelContinuity.Check(Bindings(), null, interleaved);
        Require(result.MappedRecordsChecked == 4 && result.Barriers == 0, "IMU interleaving mistaken for regression.");
        var regressed = interleaved with { Records = [Sample(1, 1, 1100000000), Sample(2, 1, 1000000000), Sample(1, 2, 1000000000)] };
        Reject(() => ClockModelContinuity.Check(Bindings(), null, regressed));
        var changedSegment = interleaved with { Records = [Sample(1, 1, 1000000000, 1)] };
        Reject(() => ClockModelContinuity.Check(Bindings(), new(FrameId, Attempt, [Frame(1, 1000000000, model: 1)]), changedSegment));
    }

    internal static void CameraClassesAndMissingRules()
    {
        PackageInputTests.WithFixture(root =>
        {
            var snapshot = Snapshot();
            foreach (var rateClass in new[] { "FIXED", "VARIABLE", "ADAPTIVE", "UNKNOWN" })
            {
                var path = Path.Combine(root, "metadata/camera-metadata.json");
                var camera = JsonNode.Parse(File.ReadAllBytes(path))!;
                camera["negotiated_mode"]!["rate_control_class"] = rateClass;
                File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(camera)); PackageInputTests.Rebind(root);
                Require(PackageInputEvidence.Evaluate(root, Observe, protocol: Bind(root, snapshot)).Scientific!.Protocol.Outcome == "CONFORMANT", "Camera class acquired implicit requirement.");
            }
            snapshot["scientific_rules"]!.AsArray().Clear();
            Require(PackageInputEvidence.Evaluate(root, Observe, protocol: Bind(root, snapshot)).Scientific!.Protocol.Outcome == "REVIEW_REQUIRED", "Empty rules passed.");
        });
        PackageInputTests.WithFixture(root =>
        {
            var result = PackageInputEvidence.Evaluate(root, protocol: Bind(root, Snapshot()));
            Require(result.Scientific!.Protocol.Outcome == "REVIEW_REQUIRED" && result.Scientific.Cadence is null,
                "Incomplete survivors acquired synthetic measurements.");
        }, "incomplete-survivors");
    }

    internal static void AdapterConsistencyGuards()
    {
        foreach (var change in new Action<JsonNode>[]
        {
            c => c["camera_stream_id"] = Guid.NewGuid().ToString(),
            c => c["camera_type"] = "ANDROID_CAMERA2",
            c => c["timestamp_provenance"] = "HOST_ARRIVAL",
            c => c["observed_timing"]!["record_count"] = 500
        })
        {
            PackageInputTests.WithFixture(root =>
            {
                var path = Path.Combine(root, "metadata/camera-metadata.json");
                var camera = JsonNode.Parse(File.ReadAllBytes(path))!; change(camera);
                File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(camera)); PackageInputTests.Rebind(root);
                Reject(() => PackageInputEvidence.Evaluate(root));
            });
        }
        PackageInputTests.WithFixture(root =>
        {
            var path = Path.Combine(root, "timing/frame-timestamps.bin");
            var bytes = File.ReadAllBytes(path);
            var record = bytes.AsSpan(64 + 96, 96);
            BinaryPrimitives.WriteUInt32LittleEndian(record[64..], 1);
            BinaryPrimitives.WriteUInt32LittleEndian(record[92..], TimingBinaryReader.Crc32C(record[..92]));
            File.WriteAllBytes(path, bytes); PackageInputTests.Rebind(root);
            Reject(() => PackageInputEvidence.Evaluate(root));
        });
        PackageInputTests.WithFixture(root =>
        {
            var path = Path.Combine(root, "metadata/timing-metadata.json");
            var timing = JsonNode.Parse(File.ReadAllBytes(path))!;
            timing["clocks"]![1]!["provenance"] = "ENCODER_PTS";
            timing["clocks"]![1]!["authority"] = "PRESENTATION";
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(timing)); PackageInputTests.Rebind(root);
            Reject(() => PackageInputEvidence.Evaluate(root));
        });
        PackageInputTests.WithFixture(root =>
        {
            var context = Bind(root, Snapshot());
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            try { PackageInputEvidence.Evaluate(root, Observe, cancellation.Token, context); }
            catch (OperationCanceledException) { return; }
            throw new InvalidOperationException("Cancelled scientific evaluation passed.");
        });
    }

    internal static void CategoricalAndLensRules()
    {
        PackageInputTests.WithFixture(root =>
        {
            var snapshot = Snapshot();
            var rules = snapshot["scientific_rules"]!.AsArray();
            rules[0]!["metric"] = "RATE_CONTROL_CLASS"; rules[0]!["operator"] = "EQ"; rules[0]!["threshold"] = "FIXED";
            rules[1]!["metric"] = "TIMESTAMP_PROVENANCE"; rules[1]!["threshold"] = "MF_DEVICE_TIMESTAMP";
            var context = Bind(root, snapshot);
            Require(PackageInputEvidence.Evaluate(root, protocol: context).Scientific!.Protocol.Outcome == "CONFORMANT", "Reported categorical rules lost evidence.");
            var admitted = PackageInputManifest.Parse(File.ReadAllBytes(Path.Combine(root, "package-manifest.json")));
            using var cameraDocument = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "metadata/camera-metadata.json")));
            Require(ProtocolScientificEvidence.Evaluate(context, admitted.Json, null, cameraDocument.RootElement, null, false, default).Outcome == "REVIEW_REQUIRED",
                "Unbound reported provenance became assessed source evidence.");
            rules[1]!["metric"] = "FOCAL_LENGTH_PRESENT"; rules[1]!["threshold"] = 1; rules[1]!["level"] = "PREFERRED";
            Require(PackageInputEvidence.Evaluate(root, protocol: Bind(root, snapshot)).Scientific!.Protocol.Outcome == "DEGRADED_ACCEPTABLE", "Reported unavailable lens not distinguished.");
            rules[1]!["level"] = "REQUIRED";
            var path = Path.Combine(root, "metadata/camera-metadata.json");
            var camera = JsonNode.Parse(File.ReadAllBytes(path))!; camera["unavailable_fields"]!.AsArray().Clear();
            File.WriteAllBytes(path, JsonSerializer.SerializeToUtf8Bytes(camera)); PackageInputTests.Rebind(root);
            Require(PackageInputEvidence.Evaluate(root, protocol: Bind(root, snapshot)).Scientific!.Protocol.Outcome == "REVIEW_REQUIRED", "Unexplained lens absence treated as measured failure.");
        });
    }
}
