using System;
using System.Runtime.InteropServices;
using System.Text;
using JdeClient.Core.Internal;

namespace JdeClient.Core.UnitTests.Internal;

public class TableLayoutTests
{
    [Test]
    public async Task ReadValueByColumn_ReadsExpectedTypes()
    {
        // Arrange
        var nameField = new TableField("NAME", TableFieldType.JCharArray, 0, 4, "NAME");
        var flagField = new TableField("FLAG", TableFieldType.JCharSingle, 8, 1, "FLAG");
        var idField = new TableField("ID", TableFieldType.Id, 12, 4, "ID");
        var dateField = new TableField("DATE", TableFieldType.JdeDate, 16, 6, "DATE");
        var mathField = new TableField("MATH", TableFieldType.MathNumeric, 24, 49, "MATH");
        var nameBeField = new TableField("NAME_BE", TableFieldType.JCharArray, 80, 2, "NAME_BE");

        var fields = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase)
        {
            ["NAME"] = nameField,
            ["FLAG"] = flagField,
            ["ID"] = idField,
            ["DATE"] = dateField,
            ["MATH"] = mathField,
            ["NAME_BE"] = nameBeField
        };

        var columns = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase)
        {
            ["NAME"] = nameField,
            ["FLAG"] = flagField,
            ["ID"] = idField,
            ["DATE"] = dateField,
            ["MATH"] = mathField,
            ["NAME_BE"] = nameBeField
        };

        var layout = new TableLayout("TEST", 128, fields, columns);

        IntPtr buffer = Marshal.AllocHGlobal(128);
        try
        {
            var zero = new byte[128];
            Marshal.Copy(zero, 0, buffer, zero.Length);

            var nameBytes = Encoding.Unicode.GetBytes("ABCD");
            Marshal.Copy(nameBytes, 0, buffer, nameBytes.Length);

            Marshal.WriteByte(buffer, flagField.Offset, 0x5A);
            Marshal.WriteByte(buffer, flagField.Offset + 1, 0x5A);

            Marshal.WriteInt32(IntPtr.Add(buffer, idField.Offset), 42);

            Marshal.WriteInt16(IntPtr.Add(buffer, dateField.Offset), 2025);
            Marshal.WriteInt16(IntPtr.Add(buffer, dateField.Offset + 2), 1);
            Marshal.WriteInt16(IntPtr.Add(buffer, dateField.Offset + 4), 2);

            var mathBytes = BuildMathBytes("99", decimalPos: 0, length: 2);
            Marshal.Copy(mathBytes, 0, IntPtr.Add(buffer, mathField.Offset), mathBytes.Length);

            var nameBeBytes = Encoding.BigEndianUnicode.GetBytes("XY");
            Marshal.Copy(nameBeBytes, 0, IntPtr.Add(buffer, nameBeField.Offset), nameBeBytes.Length);

            // Act
            var name = layout.ReadJCharString(buffer, "NAME");
            var flag = layout.ReadValueByColumn(buffer, "FLAG");
            var id = layout.ReadValueByColumn(buffer, "ID");
            var date = layout.ReadValueByColumn(buffer, "DATE");
            var math = layout.ReadValueByColumn(buffer, "MATH");
            var nameBe = layout.ReadValueByColumn(buffer, "NAME_BE");
            var missing = layout.ReadValueByColumn(buffer, "MISSING");

            // Assert
            await Assert.That(name).IsEqualTo("ABCD");
            await Assert.That(flag.Value).IsEqualTo("Z");
            await Assert.That(id.Value).IsEqualTo(42);

            var dateValue = date.Value as DateTime?;
            await Assert.That(dateValue is not null).IsTrue();
            await Assert.That(dateValue!.Value.Year).IsEqualTo(2025);
            await Assert.That(dateValue.Value.Month).IsEqualTo(1);
            await Assert.That(dateValue.Value.Day).IsEqualTo(2);

            await Assert.That(math.Value?.ToString()).IsEqualTo("99");
            await Assert.That(nameBe.Value).IsEqualTo("XY");
            await Assert.That(missing.Value).IsEqualTo(string.Empty);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static byte[] BuildMathBytes(string digits, short decimalPos, short length)
    {
        var bytes = new byte[49];
        var digitBytes = Encoding.ASCII.GetBytes(digits);
        Array.Copy(digitBytes, bytes, Math.Min(digitBytes.Length, 33));
        Array.Copy(BitConverter.GetBytes(decimalPos), 0, bytes, 35, 2);
        Array.Copy(BitConverter.GetBytes(length), 0, bytes, 37, 2);
        return bytes;
    }
}
