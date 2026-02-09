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

    [Test]
    public async Task ToString_EmptyDigits_ReturnsZero()
    {
        // All null bytes for digits, but valid metadata
        var bytes = new byte[49];
        // Set length > 0 but digits are all \0
        Array.Copy(BitConverter.GetBytes((short)0), 0, bytes, 35, 2); // decimalPos=0
        Array.Copy(BitConverter.GetBytes((short)5), 0, bytes, 37, 2); // length=5
        var result = MathNumericParser.ToString(bytes);
        await Assert.That(result).IsEqualTo("0");
    }

    [Test]
    public async Task ToString_DigitsLongerThanLength_Truncates()
    {
        var bytes = BuildMathBytes("123456789", decimalPos: 0, length: 5);
        var result = MathNumericParser.ToString(bytes);
        await Assert.That(result).IsEqualTo("12345");
    }

    [Test]
    public async Task ToString_NegativeSign_ReturnsNegative()
    {
        var bytes = BuildMathBytes("42", decimalPos: 0, length: 2, sign: (byte)'-');
        var result = MathNumericParser.ToString(bytes);
        await Assert.That(result).IsEqualTo("-42");
    }

    [Test]
    public async Task ToInt_NoDecimal_StraightParse()
    {
        var bytes = BuildMathBytes("500", decimalPos: 0, length: 3);
        var result = MathNumericParser.ToInt(bytes);
        await Assert.That(result).IsEqualTo(500);
    }

    [Test]
    public async Task ToInt_UnparseableText_ReturnsZero()
    {
        // Set up bytes that will produce a non-numeric string after normalization
        var bytes = new byte[49];
        bytes[0] = (byte)' '; // space only
        Array.Copy(BitConverter.GetBytes((short)0), 0, bytes, 35, 2);
        Array.Copy(BitConverter.GetBytes((short)1), 0, bytes, 37, 2);
        var result = MathNumericParser.ToInt(bytes);
        await Assert.That(result).IsEqualTo(0);
    }

    [Test]
    public async Task NormalizeNumericString_DecimalPosBeyondDigits_PadsWithZeros()
    {
        // digits="5", decimalPos=3 means we need to pad with zeros
        var bytes = BuildMathBytes("5", decimalPos: 3, length: 1);
        var result = MathNumericParser.ToString(bytes);
        // "5" with decimalPos=3: pad = 3-1=2 → digits becomes "0005", then insert decimal at position 1 → "0.005"
        await Assert.That(result).IsEqualTo("0.005");
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
