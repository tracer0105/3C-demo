namespace Cim.DbAdapter.Utilities;

/// <summary>
/// 工控协议常用字节工具。
/// 提供 BCD、位操作、大小端转换、字节拼包/拆包等基础能力。
/// </summary>
public static class ProtocolUtility
{
    public static byte[] BuildFrame(byte station, byte function, params byte[] data)
    {
        var payload = data ?? Array.Empty<byte>();
        var frame = new byte[payload.Length + 2];
        frame[0] = station;
        frame[1] = function;
        Array.Copy(payload, 0, frame, 2, payload.Length);
        return frame;
    }

    public static ushort ToUInt16BigEndian(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 2)
        {
            throw new ArgumentException("At least 2 bytes are required.", nameof(bytes));
        }

        return (ushort)((bytes[0] << 8) | bytes[1]);
    }

    public static ushort ToUInt16LittleEndian(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 2)
        {
            throw new ArgumentException("At least 2 bytes are required.", nameof(bytes));
        }

        return (ushort)(bytes[0] | (bytes[1] << 8));
    }

    public static uint ToUInt32BigEndian(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4)
        {
            throw new ArgumentException("At least 4 bytes are required.", nameof(bytes));
        }

        return ((uint)bytes[0] << 24) |
               ((uint)bytes[1] << 16) |
               ((uint)bytes[2] << 8) |
               bytes[3];
    }

    public static byte[] GetBytesBigEndian(ushort value) =>
        new[] { (byte)(value >> 8), (byte)(value & 0xFF) };

    public static byte[] GetBytesLittleEndian(ushort value) =>
        new[] { (byte)(value & 0xFF), (byte)(value >> 8) };

    public static bool GetBit(byte value, int bitIndex)
    {
        ValidateBitIndex(bitIndex);
        return (value & (1 << bitIndex)) != 0;
    }

    public static byte SetBit(byte value, int bitIndex, bool bitValue)
    {
        ValidateBitIndex(bitIndex);
        return bitValue
            ? (byte)(value | (1 << bitIndex))
            : (byte)(value & ~(1 << bitIndex));
    }

    public static byte[] ReverseWords(byte[] bytes)
    {
        if (bytes is null)
        {
            throw new ArgumentNullException(nameof(bytes));
        }

        if (bytes.Length % 2 != 0)
        {
            throw new ArgumentException("Byte length must be an even number.", nameof(bytes));
        }

        var result = new byte[bytes.Length];
        for (var i = 0; i < bytes.Length; i += 2)
        {
            result[i] = bytes[i + 1];
            result[i + 1] = bytes[i];
        }

        return result;
    }

    public static byte[] EncodeBcd(string digits)
    {
        if (string.IsNullOrWhiteSpace(digits))
        {
            return Array.Empty<byte>();
        }

        var normalized = digits.Trim();
        if (normalized.Length % 2 != 0)
        {
            normalized = "0" + normalized;
        }

        var result = new byte[normalized.Length / 2];
        for (var i = 0; i < normalized.Length; i += 2)
        {
            var high = ParseDigit(normalized[i]);
            var low = ParseDigit(normalized[i + 1]);
            result[i / 2] = (byte)((high << 4) | low);
        }

        return result;
    }

    public static string DecodeBcd(ReadOnlySpan<byte> bytes)
    {
        var chars = new char[bytes.Length * 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            chars[i * 2] = (char)('0' + ((bytes[i] >> 4) & 0x0F));
            chars[i * 2 + 1] = (char)('0' + (bytes[i] & 0x0F));
        }

        return new string(chars).TrimStart('0');
    }

    private static int ParseDigit(char c)
    {
        if (c is < '0' or > '9')
        {
            throw new ArgumentException($"Invalid BCD digit: {c}");
        }

        return c - '0';
    }

    private static void ValidateBitIndex(int bitIndex)
    {
        if (bitIndex is < 0 or > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(bitIndex), "Bit index must be between 0 and 7.");
        }
    }
}
