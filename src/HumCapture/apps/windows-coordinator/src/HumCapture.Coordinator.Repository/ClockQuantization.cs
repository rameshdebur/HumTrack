using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace HumCapture.Coordinator.Repository;

internal sealed record ExactClockScale(BigInteger Numerator, BigInteger Denominator)
{
    internal bool IsOne => Numerator == Denominator;
}

internal static class ClockQuantization
{
    internal static ExactClockScale ParseScale(string text)
    {
        if (text.Length is 0 or > 128) { throw new InvalidDataException("Scale outside supported text envelope."); }
        var match = Regex.Match(text, @"\A(?<sign>-?)(?<whole>0|[1-9][0-9]*)(?:\.(?<fraction>[0-9]+))?(?:[eE](?<exponent>[+-]?[0-9]+))?\z",
            RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        if (!match.Success) { throw new InvalidDataException("Scale is not a JSON number."); }
        var exponent = 0;
        if (match.Groups["exponent"].Success
            && (!int.TryParse(match.Groups["exponent"].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent)
                || exponent is < -128 or > 128))
        { throw new InvalidDataException("Scale exponent outside supported envelope."); }
        var fraction = match.Groups["fraction"].Value;
        var numerator = BigInteger.Parse(match.Groups["whole"].Value + fraction, CultureInfo.InvariantCulture);
        if (match.Groups["sign"].Value == "-") { numerator = -numerator; }
        var power = exponent - fraction.Length;
        var denominator = BigInteger.One;
        if (power >= 0) { numerator *= BigInteger.Pow(10, power); }
        else { denominator = BigInteger.Pow(10, -power); }
        var divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        return new ExactClockScale(numerator / divisor, denominator / divisor);
    }

    internal static ulong Map(ExactClockScale scale, ulong sourceTicks, long offsetTicks)
    {
        if (scale.Denominator <= 0) { throw new InvalidDataException("Invalid scale denominator."); }
        var numerator = scale.Numerator * sourceTicks + (BigInteger)offsetTicks * scale.Denominator;
        if (numerator < 0 || numerator > (BigInteger)ulong.MaxValue * scale.Denominator)
        { throw new InvalidDataException("Unrounded mapped time outside uint64 range."); }
        var quotient = BigInteger.DivRem(numerator, scale.Denominator, out var remainder);
        var comparison = (remainder * 2).CompareTo(scale.Denominator);
        if (comparison > 0 || (comparison == 0 && !quotient.IsEven)) { quotient++; }
        return checked((ulong)quotient);
    }
}
