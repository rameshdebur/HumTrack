using System.Buffers.Binary;

namespace HumCapture.Coordinator.Repository;

internal sealed record TimingStream<T>(Guid StreamId, Guid CaptureAttemptId, IReadOnlyList<T> Records);
internal sealed record SourceFrame(ulong Sequence, ulong NativeTicks, ulong? SourceFrameNumber,
    ulong? HostArrivalTicks, ulong? MappedSessionTicks, long? EncodedPtsTicks, ulong? VideoFrameIndex,
    uint ClockModelId, uint UncertaintyNs, uint SegmentId, ushort Disposition,
    ulong? ExposureDurationNs, ulong? RollingShutterSkewNs);
internal sealed record ImuSample(ulong Sequence, ulong NativeTicks, ulong? HostArrivalTicks,
    ulong? MappedSessionTicks, float X, float Y, float Z, float? W, uint ClockModelId,
    uint UncertaintyNs, uint SegmentId, ushort SensorStreamId, byte SensorKind, sbyte Accuracy, uint? BatchId);

// Finalized input only. Recovery never discards bytes through this verifier reader.
internal static class TimingBinaryReader
{
    internal const int MaximumFrameRecords = 100000;
    internal const int MaximumImuRecords = 1000000;
    private static readonly (int Offset, int Bit)[] FrameOptionalFields =
        [(16, 1), (24, 2), (32, 4), (40, 8), (48, 16), (76, 32), (84, 64)];

    internal static TimingStream<SourceFrame> Frames(ReadOnlySpan<byte> bytes, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var count = Header(bytes, "HCTIME1\0"u8, 96, MaximumFrameRecords);
        var records = new SourceFrame[count];
        for (var index = 0; index < count; index++)
        {
            token.ThrowIfCancellationRequested();
            var record = bytes.Slice(64 + index * 96, 96);
            Crc(record);
            var presence = U16(record, 70);
            Require((presence & ~127) == 0 && U32(record, 72) == 0, "Unknown frame flags.");
            var disposition = U16(record, 68);
            Require(disposition is 1 or 2 or 3 or 4 or 5 or 255, "Unknown frame disposition.");
            foreach (var (offset, bit) in FrameOptionalFields)
            { Absent(record, offset, 8, presence, bit); }
            Mapping(presence, 4, U32(record, 56), U32(record, 60));
            records[index] = new SourceFrame(U64(record, 0), U64(record, 8), Optional(record, 16, presence, 1),
                Optional(record, 24, presence, 2), Optional(record, 32, presence, 4),
                (presence & 8) != 0 ? BinaryPrimitives.ReadInt64LittleEndian(record[40..]) : null,
                Optional(record, 48, presence, 16), U32(record, 56), U32(record, 60), U32(record, 64), disposition,
                Optional(record, 76, presence, 32), Optional(record, 84, presence, 64));
        }
        return new TimingStream<SourceFrame>(new Guid(bytes.Slice(20, 16), bigEndian: true),
            new Guid(bytes.Slice(36, 16), bigEndian: true), Array.AsReadOnly(records));
    }

    internal static TimingStream<ImuSample> Imu(ReadOnlySpan<byte> bytes, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var count = Header(bytes, "HCIMU1\0\0"u8, 80, MaximumImuRecords);
        var records = new ImuSample[count];
        for (var index = 0; index < count; index++)
        {
            token.ThrowIfCancellationRequested();
            var record = bytes.Slice(64 + index * 80, 80);
            Crc(record);
            var presence = U16(record, 64);
            Require((presence & ~15) == 0 && U16(record, 66) == 0 && U32(record, 72) == 0, "Unknown IMU flags.");
            Absent(record, 16, 8, presence, 1);
            Absent(record, 24, 8, presence, 2);
            Absent(record, 44, 4, presence, 4);
            Absent(record, 68, 4, presence, 8);
            Mapping(presence, 2, U32(record, 48), U32(record, 52));
            var accuracy = unchecked((sbyte)record[63]);
            Require(U16(record, 60) != 0 && record[62] is >= 1 and <= 6 && accuracy is >= -1 and <= 3,
                "Unknown IMU sensor or accuracy.");
            records[index] = new ImuSample(U64(record, 0), U64(record, 8), Optional(record, 16, presence, 1),
                Optional(record, 24, presence, 2), Component(record, 32), Component(record, 36), Component(record, 40),
                (presence & 4) != 0 ? Component(record, 44) : null, U32(record, 48), U32(record, 52), U32(record, 56),
                U16(record, 60), record[62], accuracy, (presence & 8) != 0 ? U32(record, 68) : null);
        }
        return new TimingStream<ImuSample>(new Guid(bytes.Slice(20, 16), bigEndian: true),
            new Guid(bytes.Slice(36, 16), bigEndian: true), Array.AsReadOnly(records));
    }

    private static int Header(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> magic, int size, int maximum)
    {
        Require(bytes.Length >= 64, "Truncated timing header.");
        Require(bytes[..8].SequenceEqual(magic) && U16(bytes, 8) == 1 && U16(bytes, 10) == 0
            && U16(bytes, 12) == 64 && U16(bytes, 14) == size, "Unsupported timing layout/version.");
        Crc(bytes[..64]);
        Require(U32(bytes, 16) == 1, "Stream not finalized or unknown header flags.");
        var count = U64(bytes, 52);
        Require(count <= (ulong)maximum, "Timing reader engineering record limit exceeded.");
        Require((ulong)(bytes.Length - 64) == count * (ulong)size, "Timing record count/tail mismatch.");
        return (int)count;
    }

    internal static uint Crc32C(ReadOnlySpan<byte> bytes)
    {
        var crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) { crc = (crc >> 1) ^ ((crc & 1) == 0 ? 0 : 0x82f63b78U); }
        }
        return ~crc;
    }

    private static void Crc(ReadOnlySpan<byte> bytes) => Require(U32(bytes, bytes.Length - 4)
        == Crc32C(bytes[..^4]), "Timing CRC32C mismatch.");
    private static ushort U16(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(bytes[offset..]);
    private static uint U32(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
    private static ulong U64(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadUInt64LittleEndian(bytes[offset..]);
    private static ulong? Optional(ReadOnlySpan<byte> bytes, int offset, int presence, int bit) =>
        (presence & bit) != 0 ? U64(bytes, offset) : null;
    private static void Absent(ReadOnlySpan<byte> bytes, int offset, int length, int presence, int bit)
    {
        if ((presence & bit) == 0)
        { Require(bytes.Slice(offset, length).IndexOfAnyExcept((byte)0) < 0, "Absent timing field has nonzero bytes."); }
    }
    private static void Mapping(int presence, int bit, uint model, uint uncertainty) =>
        Require((presence & bit) != 0 ? model != 0 : model == 0 && uncertainty == 0, "Timing mapping evidence mismatch.");
    private static float Component(ReadOnlySpan<byte> bytes, int offset)
    {
        var value = BinaryPrimitives.ReadSingleLittleEndian(bytes[offset..]);
        Require(float.IsFinite(value), "Non-finite IMU component.");
        return value;
    }
    private static void Require(bool condition, string reason)
    { if (!condition) { throw new InvalidDataException(reason); } }
}
