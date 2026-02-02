using System;
using System.Text;
using JdeClient.Core.Internal;

namespace JdeClient.Core.UnitTests.Internal;

public class MathNumericParserTests
{
    [Test]
    public async Task ToString_ShortBuffer_ReturnsEmpty()
    {
        // Arrange
        var bytes = new byte[10];

        // Act
        var result = MathNumericParser.ToString(bytes);

        // Assert
        await Assert.That(result).IsEqualTo(string.Empty);
    }

    [Test]
    public async Task ToString_ParsesDecimalAndSign()
    {
        // Arrange
        var bytes = BuildMathBytes("12345", decimalPos: 2, length: 5, sign: (byte)'-');

        // Act
        var result = MathNumericParser.ToString(bytes);

        // Assert
        await Assert.That(result).IsEqualTo("-123.45");
    }

    [Test]
    public async Task ToString_FallsBackToAlignedMeta()
    {
        // Arrange
        var bytes = BuildMathBytes("99", decimalPos: 1, length: 2);
        Array.Copy(BitConverter.GetBytes((short)99), 0, bytes, 35, 2);
        Array.Copy(BitConverter.GetBytes((short)0), 0, bytes, 37, 2);
        Array.Copy(BitConverter.GetBytes((short)1), 0, bytes, 36, 2);
        Array.Copy(BitConverter.GetBytes((short)2), 0, bytes, 38, 2);

        // Act
        var result = MathNumericParser.ToString(bytes);

        // Assert
        await Assert.That(result).IsEqualTo("9.9");
    }

    [Test]
    public async Task ToInt_StripsDecimal()
    {
        // Arrange
        var bytes = BuildMathBytes("12345", decimalPos: 2, length: 5);

        // Act
        var result = MathNumericParser.ToInt(bytes);

        // Assert
        await Assert.That(result).IsEqualTo(123);
    }

    private static byte[] BuildMathBytes(string digits, short decimalPos, short length, byte sign = (byte)'+')
    {
        var bytes = new byte[49];
        var digitBytes = Encoding.ASCII.GetBytes(digits);
        Array.Copy(digitBytes, bytes, Math.Min(digitBytes.Length, 33));
        bytes[33] = sign;
        Array.Copy(BitConverter.GetBytes(decimalPos), 0, bytes, 35, 2);
        Array.Copy(BitConverter.GetBytes(length), 0, bytes, 37, 2);
        return bytes;
    }
}
