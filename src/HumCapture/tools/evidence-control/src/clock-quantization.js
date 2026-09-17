// Contract reference arithmetic; never use Number for source ticks or scale values.
export function quantizeClock(sourceText, scaleText, offsetText) {
  if (scaleText.length > 128) throw new Error("Scale envelope");
  const match = /^(-?)(0|[1-9][0-9]*)(?:\.([0-9]+))?(?:[eE]([+-]?[0-9]+))?$/.exec(scaleText);
  if (!match || match[0].length !== scaleText.length) throw new Error("Scale grammar");
  const exponent = Number(match[4] ?? "0");
  if (!Number.isInteger(exponent) || Math.abs(exponent) > 128) throw new Error("Scale exponent envelope");
  if (!/^(0|[1-9][0-9]*)$/.test(sourceText) || !/^(0|-?[1-9][0-9]*)$/.test(offsetText)) throw new Error("Integer grammar");
  const source = BigInt(sourceText), offset = BigInt(offsetText), max = (1n << 64n) - 1n;
  if (source > max || offset < -(1n << 63n) || offset >= (1n << 63n)) throw new Error("Integer range");
  let p = BigInt(match[2] + (match[3] ?? "")) * (match[1] ? -1n : 1n), q = 1n;
  const power = exponent - (match[3]?.length ?? 0);
  if (power >= 0) p *= 10n ** BigInt(power); else q = 10n ** BigInt(-power);
  const numerator = p * source + offset * q;
  if (numerator < 0n || numerator > max * q) throw new Error("Mapped range");
  let result = numerator / q;
  const twiceRemainder = 2n * (numerator % q);
  if (twiceRemainder > q || (twiceRemainder === q && result % 2n !== 0n)) result++;
  return result.toString();
}
