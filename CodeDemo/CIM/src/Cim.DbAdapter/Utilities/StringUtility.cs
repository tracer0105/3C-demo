using System.Text;
using System.Text.RegularExpressions;

namespace Cim.DbAdapter.Utilities;

/// <summary>
/// 工控/制造场景常见字符串处理工具。
/// </summary>
public static partial class StringUtility
{
    private static readonly char[] DefaultSeparators = ['_', '-', '/', '\\', '.', ' '];

    public static string SafeTrim(string? value) => value?.Trim() ?? string.Empty;

    public static bool IsNullOrWhite(string? value) => string.IsNullOrWhiteSpace(value);

    public static string NullToEmpty(string? value) => value ?? string.Empty;

    public static string Left(string? value, int length)
    {
        if (string.IsNullOrEmpty(value) || length <= 0)
        {
            return string.Empty;
        }

        return value.Length <= length ? value : value[..length];
    }

    public static string Right(string? value, int length)
    {
        if (string.IsNullOrEmpty(value) || length <= 0)
        {
            return string.Empty;
        }

        return value.Length <= length ? value : value[^length..];
    }

    public static string PadLeftZero(string? value, int totalWidth) =>
        (value ?? string.Empty).PadLeft(totalWidth, '0');

    public static string PadRightSpace(string? value, int totalWidth) =>
        (value ?? string.Empty).PadRight(totalWidth, ' ');

    public static string RemoveWhitespace(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (!char.IsWhiteSpace(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    public static string KeepLettersAndDigits(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    public static string ToSnakeCase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var words = SplitWords(value);
        return string.Join("_", words).ToLowerInvariant();
    }

    public static string ToPascalCase(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var words = SplitWords(value);
        return string.Concat(words.Select(Capitalize));
    }

    public static string ToCamelCase(string? value)
    {
        var pascal = ToPascalCase(value);
        return string.IsNullOrEmpty(pascal)
            ? string.Empty
            : char.ToLowerInvariant(pascal[0]) + pascal[1..];
    }

    public static string SanitizeSingleLine(string? value, char replacement = ' ')
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value
            .Replace('\r', replacement)
            .Replace('\n', replacement)
            .Replace('\t', replacement);
    }

    public static bool EqualsIgnoreCase(string? left, string? right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    public static bool ContainsIgnoreCase(string? source, string value) =>
        !string.IsNullOrEmpty(source) && source.Contains(value, StringComparison.OrdinalIgnoreCase);

    public static string[] SplitAndTrim(string? value, params char[] separators)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        var actualSeparators = separators is { Length: > 0 } ? separators : [',', ';'];
        return value
            .Split(actualSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static bool IsEquipmentCode(string? value) =>
        !string.IsNullOrWhiteSpace(value) && EquipmentCodeRegex().IsMatch(value);

    private static IEnumerable<string> SplitWords(string value)
    {
        return value
            .Split(DefaultSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(static segment => Regex.Matches(segment, @"[A-Z]?[a-z]+|[A-Z]+(?![a-z])|\d+")
                .Select(static match => match.Value));
    }

    private static string Capitalize(string value) =>
        value.Length switch
        {
            0 => string.Empty,
            1 => value.ToUpperInvariant(),
            _ => char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant()
        };

    [GeneratedRegex(@"^[A-Za-z]{2,10}[\-_]?[A-Za-z0-9]{1,20}$", RegexOptions.Compiled)]
    private static partial Regex EquipmentCodeRegex();
}
