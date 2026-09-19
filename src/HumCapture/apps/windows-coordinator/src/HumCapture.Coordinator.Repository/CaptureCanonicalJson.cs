using System.Globalization;
using System.Text;
using System.Text.Json;

namespace HumCapture.Coordinator.Repository;

// RFC 8785 for the capture schemas' safe-integer/string subset ONLY.
// Not a general JSON-number canonicalizer or manifest admission mechanism.
internal static class CaptureCanonicalJson
{
    internal static byte[] Encode(JsonElement value, CancellationToken token = default, string? excludedRootProperty = null)
    {
        var output = new StringBuilder();
        try { Write(value, output, token, excludedRootProperty); }
        catch (InvalidOperationException error) { throw new InvalidDataException("Invalid capture JSON value/Unicode.", error); }
        return new UTF8Encoding(false, true).GetBytes(output.ToString());
    }

    private static void Write(JsonElement value, StringBuilder output, CancellationToken token, string? excludedProperty = null)
    {
        token.ThrowIfCancellationRequested();
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                output.Append('{'); var first = true;
                foreach (var property in value.EnumerateObject().Where(p => p.Name != excludedProperty).OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    if (!first) { output.Append(','); }
                    first = false; WriteString(property.Name, output); output.Append(':'); Write(property.Value, output, token);
                }
                output.Append('}'); break;
            case JsonValueKind.Array:
                output.Append('['); var firstItem = true;
                foreach (var item in value.EnumerateArray())
                {
                    if (!firstItem) { output.Append(','); }
                    firstItem = false; Write(item, output, token);
                }
                output.Append(']'); break;
            case JsonValueKind.String: WriteString(value.GetString()!, output); break;
            case JsonValueKind.Number:
                if (!value.TryGetInt64(out var number) || number < -9007199254740991 || number > 9007199254740991)
                { throw new InvalidDataException("Capture canonical number is outside the safe-integer subset."); }
                output.Append(number.ToString(CultureInfo.InvariantCulture)); break;
            case JsonValueKind.True: output.Append("true"); break;
            case JsonValueKind.False: output.Append("false"); break;
            case JsonValueKind.Null: output.Append("null"); break;
            default: throw new InvalidDataException("Unsupported capture JSON value.");
        }
    }

    private static void WriteString(string value, StringBuilder output)
    {
        output.Append('"');
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (char.IsSurrogate(c))
            {
                if ((char.IsHighSurrogate(c) && (i + 1 >= value.Length || !char.IsLowSurrogate(value[i + 1])))
                    || (char.IsLowSurrogate(c) && (i == 0 || !char.IsHighSurrogate(value[i - 1]))))
                { throw new InvalidDataException("Invalid capture Unicode surrogate."); }
                output.Append(c); continue;
            }
            switch (c)
            {
                case '"': output.Append("\\\""); break;
                case '\\': output.Append("\\\\"); break;
                case '\b': output.Append("\\b"); break;
                case '\t': output.Append("\\t"); break;
                case '\n': output.Append("\\n"); break;
                case '\f': output.Append("\\f"); break;
                case '\r': output.Append("\\r"); break;
                default:
                    if (c < 0x20) { output.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture)); }
                    else { output.Append(c); }
                    break;
            }
        }
        output.Append('"');
    }
}
