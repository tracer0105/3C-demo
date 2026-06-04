using System.Globalization;
using System.Text.RegularExpressions;

namespace EthercatEsi.Core;

public static partial class EsiValueParser
{
    public static string NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return WhiteSpacePattern().Replace(value.Trim(), " ");
    }

    public static string NormalizeHex(string? value, int minimumDigits = 0)
    {
        var text = NormalizeText(value);
        if (text.Length == 0)
        {
            return string.Empty;
        }

        if (text.StartsWith("#x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }
        else if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }
        else
        {
            return text;
        }

        text = text.TrimStart('0');
        if (text.Length == 0)
        {
            text = "0";
        }

        if (minimumDigits > 0)
        {
            text = text.PadLeft(minimumDigits, '0');
        }

        return "0x" + text.ToUpperInvariant();
    }

    public static bool TryParseHex(string? value, out int result)
    {
        var text = NormalizeText(value);
        if (text.StartsWith("#x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }
        else if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            text = text[2..];
        }

        return int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out result);
    }

    public static int? ParseNullableInt(string? value)
    {
        var text = NormalizeText(value);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : null;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpacePattern();
}
