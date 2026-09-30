using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using HumCapture.Coordinator.Repository;

namespace HumCapture.Coordinator.Repository.SelfTest;

internal static class TimingEvidenceTests
{
    private static string Vectors => Path.Combine(AppContext.BaseDirectory, "TimingVectors");
    private static byte[] Fixture(string name = "valid-frame-timestamps.bin") => File.ReadAllBytes(Path.Combine(Vectors, name));
    private static void Require(bool value, string reason)
    { if (!value) { throw new InvalidOperationException(reason); } }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidDataException) { return; }
        throw new InvalidOperationException("Invalid timing evidence accepted.");
    }
    private static void Crc(byte[] bytes, int offset, int length) => BinaryPrimitives.WriteUInt32LittleEndian(
        bytes.AsSpan(offset + length - 4), TimingBinaryReader.Crc32C(bytes.AsSpan(offset, length - 4)));

    internal static void GoldenVectors()
    {
        Require(TimingBinaryReader.Crc32C("123456789"u8) == 0xe3069283, "CRC32C standard check failed.");
        using var manifest = JsonDocument.Parse(Fixture("vector-manifest.json"));
        using var reference = JsonDocument.Parse(Fixture("decoded-reference.json"));
        foreach (var vector in manifest.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var bytes = Fixture(vector.GetProperty("file").GetString()!);
            Require(bytes.Length == vector.GetProperty("byte_length").GetInt32()
                && Convert.ToHexStringLower(SHA256.HashData(bytes)) == vector.GetProperty("sha256").GetString(), "Golden vector changed.");
            if (vector.GetProperty("expected").GetString() == "REJECT")
            { Reject(() => TimingBinaryReader.Frames(bytes)); continue; }
            var expected = reference.RootElement.GetProperty("vectors").EnumerateArray()
                .Single(v => v.GetProperty("id").GetString() == vector.GetProperty("id").GetString());
            if (vector.GetProperty("kind").GetString() == "FRAME") { CheckReference(TimingBinaryReader.Frames(bytes), expected); }
            else { CheckReference(TimingBinaryReader.Imu(bytes), expected); }
        }
    }

    private static void CheckReference<T>(TimingStream<T> stream, JsonElement expected)
    {
        var header = expected.GetProperty("header");
        Require(stream.StreamId.ToString() == header.GetProperty("streamId").GetString()
            && stream.CaptureAttemptId.ToString() == header.GetProperty("captureId").GetString(), "UUID byte order changed.");
        var records = expected.GetProperty("records");
        Require(stream.Records.Count == records.GetArrayLength(), "Golden count mismatch.");
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        for (var index = 0; index < records.GetArrayLength(); index++)
        {
            var actual = JsonSerializer.SerializeToElement(stream.Records[index], options);
            foreach (var field in actual.EnumerateObject())
            {
                if (field.Value.ValueKind == JsonValueKind.Null)
                { Require(!records[index].TryGetProperty(field.Name, out _), "Absent field acquired a value."); continue; }
                var value = records[index].GetProperty(field.Name);
                var equal = field.Name is "x" or "y" or "z" or "w"
                    ? BitConverter.SingleToInt32Bits(value.GetSingle()) == BitConverter.SingleToInt32Bits(field.Value.GetSingle())
                    : value.ToString() == field.Value.ToString();
                Require(equal, "Golden record mismatch: " + field.Name);
            }
        }
    }

    internal static void HeaderGuards()
    {
        foreach (var offset in new[] { 0, 8, 10, 12, 14, 16, 52 })
        {
            var bytes = Fixture(); bytes[offset] ^= 2; Crc(bytes, 0, 64);
            Reject(() => TimingBinaryReader.Frames(bytes));
        }
        var nonfinal = Fixture(); nonfinal[16] = 0; Crc(nonfinal, 0, 64);
        Reject(() => TimingBinaryReader.Frames(nonfinal));
        var corrupt = Fixture(); corrupt[20] ^= 1;
        Reject(() => TimingBinaryReader.Frames(corrupt));
        foreach (var size in new[] { 0, 63, 64, 351 }) { Reject(() => TimingBinaryReader.Frames(Fixture().AsSpan(0, size))); }
        Reject(() => TimingBinaryReader.Frames([.. Fixture(), 0]));
        var oversized = Fixture(); BinaryPrimitives.WriteUInt64LittleEndian(oversized.AsSpan(52), ulong.MaxValue); Crc(oversized, 0, 64);
        Reject(() => TimingBinaryReader.Frames(oversized));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        try { TimingBinaryReader.Imu(Fixture("valid-imu-samples.bin"), cancellation.Token); }
        catch (OperationCanceledException) { return; }
        throw new InvalidOperationException("Cancelled reader succeeded.");
    }

    internal static void FrameGuardsAndPrecision()
    {
        foreach (var mutate in new Action<byte[]>[]
        {
            b => b[64 + 71] = 128, b => b[64 + 72] = 1, b => b[64 + 68] = 6,
            b => b[64 + 70] &= 126, b => Array.Clear(b, 64 + 56, 4),
            b => { b[64 + 70] &= 123; Array.Clear(b, 64 + 32, 8); }
        })
        { var bytes = Fixture(); mutate(bytes); Crc(bytes, 64, 96); Reject(() => TimingBinaryReader.Frames(bytes)); }
        var exact = Fixture();
        BinaryPrimitives.WriteUInt64LittleEndian(exact.AsSpan(64), ulong.MaxValue);
        BinaryPrimitives.WriteInt64LittleEndian(exact.AsSpan(64 + 40), long.MinValue);
        Array.Clear(exact, 64 + 16, 8); Crc(exact, 64, 96);
        var frame = TimingBinaryReader.Frames(exact).Records[0];
        Require(frame.Sequence == ulong.MaxValue && frame.EncodedPtsTicks == long.MinValue && frame.SourceFrameNumber == 0,
            "Integer precision or present-zero lost.");
    }

    internal static void ImuGuards()
    {
        foreach (var mutate in new Action<byte[]>[]
        {
            b => b[64 + 65] = 128, b => b[64 + 66] = 1, b => b[64 + 72] = 1,
            b => b[64 + 62] = 7, b => Array.Clear(b, 64 + 60, 2), b => b[64 + 63] = 254,
            b => BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(64 + 32), float.NaN),
            b => BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(64 + 36), float.PositiveInfinity),
            b => { b[64 + 64] &= 251; BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(64 + 44), -0.0f); },
            b => Array.Clear(b, 64 + 48, 4), b => b[64 + 64] &= 254
        })
        { var bytes = Fixture("valid-imu-samples.bin"); mutate(bytes); Crc(bytes, 64, 80); Reject(() => TimingBinaryReader.Imu(bytes)); }
        var corrupt = Fixture("valid-imu-samples.bin"); corrupt[64] ^= 1; Reject(() => TimingBinaryReader.Imu(corrupt));
    }

    private static DecodedVideo Video(params long[] pts) => new(0, "h264", 160, 120, 1, 90000,
        pts.Select((p, i) => new DecodedFrame(i, p, 160, 120)).ToArray());

    internal static void ExactAssociation()
    {
        var records = TimingBinaryReader.Frames(Fixture()).Records.ToArray();
        var evidence = FrameEvidenceAssociation.Compare(records, Video(0, 3000, 6000), 90000, []);
        Require(evidence.AcceptedFrames == 3 && evidence.AllPresentationTimesCompared, "Valid association failed.");
        records[0] = records[0] with { EncodedPtsTicks = long.MinValue };
        records[1] = records[1] with { EncodedPtsTicks = 9007199254740993L };
        records[2] = records[2] with { EncodedPtsTicks = 9007199254740993L };
        FrameEvidenceAssociation.Compare(records, Video(long.MinValue, 9007199254740993L, 9007199254740993L), 90000, []);
        var rational = Video(long.MinValue, 9007199254740993L, 9007199254740993L) with { TimeBaseNumerator = 7, TimeBaseDenominator = 630000 };
        FrameEvidenceAssociation.Compare(records, rational, 90000, []);
        Reject(() => FrameEvidenceAssociation.Compare(records, rational, 90001, []));
    }

    internal static void AssociationGuards()
    {
        var original = TimingBinaryReader.Frames(Fixture()).Records.ToArray();
        foreach (var change in new Func<SourceFrame, SourceFrame>[]
        {
            f => f with { VideoFrameIndex = null }, f => f with { EncodedPtsTicks = null },
            f => f with { VideoFrameIndex = 3 }, f => f with { VideoFrameIndex = 0 },
            f => f with { EncodedPtsTicks = 6001 }, f => f with { Disposition = 2 }
        })
        { var records = original.ToArray(); records[2] = change(records[2]); Reject(() => FrameEvidenceAssociation.Compare(records, Video(0, 3000, 6000), 90000, [])); }
        var generated = new[] { new GeneratedVideoFrame(3, 2, "Declared CFR duplication") };
        var result = FrameEvidenceAssociation.Compare(original, Video(0, 3000, 6000, 9000), 90000, generated);
        Require(result.GeneratedFrames == 1 && !result.AllPresentationTimesCompared, "Undeclared generated PTS inferred.");
        Reject(() => FrameEvidenceAssociation.Compare(original, Video(0, 3000, 6000, 9000), 90000,
            [generated[0] with { VideoFrameIndex = ulong.MaxValue }]));
        var ambiguous = original.ToArray(); ambiguous[0] = ambiguous[0] with { Sequence = 2, SegmentId = 1 };
        Reject(() => FrameEvidenceAssociation.Compare(ambiguous, Video(0, 3000, 6000, 9000), 90000, generated));
        Reject(() => FrameEvidenceAssociation.Compare(original, Video(0, 3000, 6000), 0, []));
    }
}
