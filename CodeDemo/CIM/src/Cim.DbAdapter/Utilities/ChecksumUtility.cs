using System.Security.Cryptography;
using System.Text;

namespace Cim.DbAdapter.Utilities;

/// <summary>
/// 常见工业协议、报文、文件传输相关校验工具。
/// </summary>
public static class ChecksumUtility
{
    public static byte Lrc(ReadOnlySpan<byte> bytes)
    {
        byte sum = 0;
        foreach (var item in bytes)
        {
            sum += item;
        }

        return (byte)((~sum + 1) & 0xFF);
    }

    public static ushort Xor(ReadOnlySpan<byte> bytes)
    {
        ushort result = 0;
        foreach (var item in bytes)
        {
            result ^= item;
        }

        return result;
    }

    public static ushort Crc16Modbus(ReadOnlySpan<byte> bytes)
    {
        const ushort polynomial = 0xA001;
        ushort crc = 0xFFFF;

        foreach (var item in bytes)
        {
            crc ^= item;
            for (var i = 0; i < 8; i++)
            {
                var lsb = (crc & 0x0001) != 0;
                crc >>= 1;
                if (lsb)
                {
                    crc ^= polynomial;
                }
            }
        }

        return crc;
    }

    public static ushort Crc16Ccitt(ReadOnlySpan<byte> bytes, ushort seed = 0xFFFF)
    {
        ushort crc = seed;
        foreach (var item in bytes)
        {
            crc ^= (ushort)(item << 8);
            for (var i = 0; i < 8; i++)
            {
                crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
            }
        }

        return crc;
    }

    public static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        const uint polynomial = 0xEDB88320;
        uint crc = 0xFFFFFFFF;

        foreach (var item in bytes)
        {
            crc ^= item;
            for (var i = 0; i < 8; i++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ polynomial : crc >> 1;
            }
        }

        return ~crc;
    }

    public static string Md5Hex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        using var md5 = MD5.Create();
        return Convert.ToHexString(md5.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    public static string Sha1Hex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        using var sha1 = SHA1.Create();
        return Convert.ToHexString(sha1.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }

    public static string Sha256Hex(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(Encoding.UTF8.GetBytes(value)));
    }
}
