using System.Globalization;
using System.Text;

namespace Cim.DbAdapter.Utilities;

/// <summary>
/// 常见转码、十六进制、ASCII、BCD、日期格式转换工具。
/// </summary>
public static class EncodingUtility
{
    public static string ToHexString(ReadOnlySpan<byte> bytes, string separator = " ")
    {
        if (bytes.Length == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder(bytes.Length * 3);
        for (var i = 0; i < bytes.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(separator);
            }

            sb.Append(bytes[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    public static byte[] HexStringToBytes(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return Array.Empty<byte>();
        }

        var normalized = hex
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase);

        if (normalized.Length % 2 != 0)
        {
            throw new ArgumentException("Hex string length must be even.", nameof(hex));
        }

        var result = new byte[normalized.Length / 2];
        for (var i = 0; i < normalized.Length; i += 2)
        {
            result[i / 2] = Convert.ToByte(normalized.Substring(i, 2), 16);
        }

        return result;
    }

    public static string ToAsciiString(ReadOnlySpan<byte> bytes, bool trimEndNull = true)
    {
        var value = Encoding.ASCII.GetString(bytes);
        return trimEndNull ? value.TrimEnd('\0') : value;
    }

    public static byte[] FromAsciiString(string value, int fixedLength = 0, byte padding = 0x20)
    {
        ArgumentNullException.ThrowIfNull(value);

        var bytes = Encoding.ASCII.GetBytes(value);
        if (fixedLength <= 0)
        {
            return bytes;
        }

        if (bytes.Length > fixedLength)
        {
            return bytes[..fixedLength];
        }

        var result = new byte[fixedLength];
        Array.Copy(bytes, result, bytes.Length);
        for (var i = bytes.Length; i < fixedLength; i++)
        {
            result[i] = padding;
        }

        return result;
    }

    public static string ByteArrayToBinaryString(ReadOnlySpan<byte> bytes) =>
        string.Join(' ', bytes.ToArray().Select(static b => Convert.ToString(b, 2).PadLeft(8, '0')));

    public static string NormalizeEquipmentCode(string? value, bool upper = true)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Replace(" ", string.Empty).Replace("-", string.Empty);
        return upper ? normalized.ToUpperInvariant() : normalized;
    }

    public static string ToCompactTimestamp(DateTime value, bool useUtc = false)
    {
        var target = useUtc ? value.ToUniversalTime() : value;
        return target.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
    }

    public static DateTime ParseCompactTimestamp(string value, bool assumeLocal = true)
    {
        var styles = assumeLocal ? DateTimeStyles.AssumeLocal : DateTimeStyles.AssumeUniversal;
        return DateTime.ParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, styles);
    }
}
