using System.Runtime.InteropServices;
using System.Text;
using JdeClient.Core.Interop;

namespace JdeClient.Core.Internal;

/// <summary>
/// Helpers for parsing JDE MATH_NUMERIC values into .NET primitives.
/// </summary>
internal static class MathNumericParser
{
    private const int StringLength = 33;
    private const int PackedDecimalOffset = StringLength + 2;
    private const int AlignedDecimalOffset = StringLength + 3;

    /// <summary>
    /// Convert a raw MATH_NUMERIC byte buffer into a string representation.
    /// </summary>
    public static string ToString(byte[] bytes)
    {
        if (bytes.Length < 49)
        {
            return string.Empty;
        }

        string digits = Encoding.ASCII.GetString(bytes, 0, StringLength).TrimEnd('\0').Trim();
        byte sign = bytes[StringLength];
        short decimalPos = BitConverter.ToInt16(bytes, PackedDecimalOffset);
        short length = BitConverter.ToInt16(bytes, PackedDecimalOffset + 2);

        if (!IsValidMathNumericMeta(decimalPos, length))
        {
            decimalPos = BitConverter.ToInt16(bytes, AlignedDecimalOffset);
            length = BitConverter.ToInt16(bytes, AlignedDecimalOffset + 2);
        }

        if (length <= 0 || string.IsNullOrEmpty(digits))
        {
            return "0";
        }

        if (digits.Length > length)
        {
            digits = digits.Substring(0, length);
        }

        string normalized = NormalizeNumericString(digits, decimalPos);
        if (sign == (byte)'-')
        {
            return "-" + normalized;
        }

        return normalized;
    }

    /// <summary>
    /// Convert a native MATH_NUMERIC pointer into a string representation.
    /// </summary>
    public static string ToString(IntPtr valuePtr)
    {
        if (valuePtr == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            IntPtr rawPtr = JdeKernelApi.jdeZMathGetRawString(valuePtr);
            if (rawPtr != IntPtr.Zero)
            {
                string digits = Marshal.PtrToStringAnsi(rawPtr) ?? string.Empty;
                digits = digits.TrimEnd('\0').Trim();
                if (digits.Length == 0)
                {
                    return "0";
                }

                short decimalPos = JdeKernelApi.jdeMathGetDecimalPosition(valuePtr);
                char sign = (char)JdeKernelApi.jdeMathGetSign(valuePtr);
                if (decimalPos < 0 || decimalPos > StringLength)
                {
                    decimalPos = 0;
                }

                string normalized = NormalizeNumericString(digits, decimalPos);
                return sign == '-' ? "-" + normalized : normalized;
            }
        }
        catch
        {
            // Fallback to manual parsing below.
        }

        var bytes = new byte[49];
        Marshal.Copy(valuePtr, bytes, 0, bytes.Length);
        return ToString(bytes);
    }

    /// <summary>
    /// Convert a raw MATH_NUMERIC byte buffer into an integer.
    /// </summary>
    public static int ToInt(byte[] bytes)
    {
        string text = ToString(bytes);
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        if (text.Contains('.'))
        {
            text = text.Split('.')[0];
        }

        return int.TryParse(text, out int value) ? value : 0;
    }

    private static string NormalizeNumericString(string digits, int decimalPos)
    {
        if (decimalPos <= 0)
        {
            return digits;
        }

        int pad = decimalPos - digits.Length;
        if (pad >= 0)
        {
            digits = new string('0', pad + 1) + digits;
        }

        int decimalIndex = digits.Length - decimalPos;
        return digits.Insert(decimalIndex, ".");
    }

    private static bool IsValidMathNumericMeta(short decimalPos, short length)
    {
        if (decimalPos < 0 || decimalPos > StringLength)
        {
            return false;
        }

        if (length <= 0 || length > StringLength)
        {
            return false;
        }

        return true;
    }
}
