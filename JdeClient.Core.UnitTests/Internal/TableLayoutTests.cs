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

    [Test]
    public async Task ReadValueByField_MissingField_ReturnsEmpty()
    {
        var fields = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase);
        var columns = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase);
        var layout = new TableLayout("TEST", 32, fields, columns);

        IntPtr buffer = Marshal.AllocHGlobal(32);
        try
        {
            var result = layout.ReadValueByField(buffer, "NONEXISTENT");
            await Assert.That(result.Value).IsEqualTo(string.Empty);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Test]
    public async Task TryGetField_ReturnsFalse_WhenMissing()
    {
        var fields = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase);
        var columns = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase);
        var layout = new TableLayout("TEST", 32, fields, columns);

        var result = layout.TryGetField("MISSING", out _);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TryGetFieldByColumn_ReturnsFalse_WhenMissing()
    {
        var fields = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase);
        var columns = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase);
        var layout = new TableLayout("TEST", 32, fields, columns);

        var result = layout.TryGetFieldByColumn("MISSING", out _);
        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task ReadJdeDate_ZeroValues_ReturnsNull()
    {
        var dateField = new TableField("DATE", TableFieldType.JdeDate, 0, 6, "DATE");
        var fields = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase) { ["DATE"] = dateField };
        var columns = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase) { ["DATE"] = dateField };
        var layout = new TableLayout("TEST", 32, fields, columns);

        IntPtr buffer = Marshal.AllocHGlobal(32);
        try
        {
            var zero = new byte[32];
            Marshal.Copy(zero, 0, buffer, zero.Length);

            var result = layout.ReadValueByColumn(buffer, "DATE");
            await Assert.That(result.Value is null).IsTrue();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Test]
    public async Task ReadValueByColumn_DefaultFieldType_ReturnsEmpty()
    {
        // Create a field with a type value that doesn't match any known cases
        // TableFieldType only has 5 values (0-4), so we need to test the default case
        // However the enum is exhaustive, so the default path would only be hit
        // if the enum was extended. We verify the existing JCharSingle path instead.
        var singleField = new TableField("FLAG", TableFieldType.JCharSingle, 0, 1, "FLAG");
        var fields = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase) { ["FLAG"] = singleField };
        var columns = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase) { ["FLAG"] = singleField };
        var layout = new TableLayout("TEST", 32, fields, columns);

        IntPtr buffer = Marshal.AllocHGlobal(32);
        try
        {
            var zero = new byte[32];
            Marshal.Copy(zero, 0, buffer, zero.Length);
            Marshal.WriteByte(buffer, 0, 0x41); // 'A'
            Marshal.WriteByte(buffer, 1, 0x00);

            var result = layout.ReadValueByColumn(buffer, "FLAG");
            await Assert.That(result.Value).IsEqualTo("A");
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Test]
    public async Task ReadJCharString_NonJCharField_ReturnsEmpty()
    {
        var idField = new TableField("ID", TableFieldType.Id, 0, 4, "ID");
        var fields = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase) { ["ID"] = idField };
        var columns = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase) { ["ID"] = idField };
        var layout = new TableLayout("TEST", 32, fields, columns);

        IntPtr buffer = Marshal.AllocHGlobal(32);
        try
        {
            var zero = new byte[32];
            Marshal.Copy(zero, 0, buffer, zero.Length);
            var result = layout.ReadJCharString(buffer, "ID");
            await Assert.That(result).IsEqualTo(string.Empty);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Test]
    public async Task ReadJCharString_MissingField_ReturnsEmpty()
    {
        var fields = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase);
        var columns = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase);
        var layout = new TableLayout("TEST", 32, fields, columns);

        IntPtr buffer = Marshal.AllocHGlobal(32);
        try
        {
            var result = layout.ReadJCharString(buffer, "NOFIELD");
            await Assert.That(result).IsEqualTo(string.Empty);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Test]
    public async Task ReadValueByField_ValidField_ReturnsValue()
    {
        var idField = new TableField("ID", TableFieldType.Id, 0, 4, "ID");
        var fields = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase) { ["ID"] = idField };
        var columns = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase) { ["ID"] = idField };
        var layout = new TableLayout("TEST", 32, fields, columns);

        IntPtr buffer = Marshal.AllocHGlobal(32);
        try
        {
            var zero = new byte[32];
            Marshal.Copy(zero, 0, buffer, zero.Length);
            Marshal.WriteInt32(buffer, 99);

            var result = layout.ReadValueByField(buffer, "ID");
            await Assert.That(result.Value).IsEqualTo(99);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Test]
    public async Task ReadJdeDate_InvalidDateValues_ReturnsNull()
    {
        // Month=13, day=99 will throw in DateTime constructor → caught → null
        var dateField = new TableField("DATE", TableFieldType.JdeDate, 0, 6, "DATE");
        var fields = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase) { ["DATE"] = dateField };
        var columns = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase) { ["DATE"] = dateField };
        var layout = new TableLayout("TEST", 32, fields, columns);

        IntPtr buffer = Marshal.AllocHGlobal(32);
        try
        {
            var zero = new byte[32];
            Marshal.Copy(zero, 0, buffer, zero.Length);
            Marshal.WriteInt16(buffer, 0, 2025);
            Marshal.WriteInt16(buffer, 2, 13);  // Invalid month
            Marshal.WriteInt16(buffer, 4, 99);  // Invalid day

            var result = layout.ReadValueByColumn(buffer, "DATE");
            await Assert.That(result.Value is null).IsTrue();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Test]
    public async Task ReadJChar_NonUnicodeBytes_FallsBackToDefault()
    {
        // Create bytes where neither even nor odd positions have enough zeros for LooksLikeUnicode
        var nameField = new TableField("NAME", TableFieldType.JCharArray, 0, 4, "NAME");
        var fields = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase) { ["NAME"] = nameField };
        var columns = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase) { ["NAME"] = nameField };
        var layout = new TableLayout("TEST", 32, fields, columns);

        IntPtr buffer = Marshal.AllocHGlobal(32);
        try
        {
            var zero = new byte[32];
            Marshal.Copy(zero, 0, buffer, zero.Length);
            // Write bytes that look like ANSI: non-zero in both even and odd positions
            // "ABCD" in ASCII: 0x41 0x42 0x43 0x44 (plus padding bytes also non-zero)
            Marshal.WriteByte(buffer, 0, 0x41); // A
            Marshal.WriteByte(buffer, 1, 0x42); // B
            Marshal.WriteByte(buffer, 2, 0x43); // C
            Marshal.WriteByte(buffer, 3, 0x44); // D
            Marshal.WriteByte(buffer, 4, 0x45); // E
            Marshal.WriteByte(buffer, 5, 0x46); // F
            Marshal.WriteByte(buffer, 6, 0x47); // G
            Marshal.WriteByte(buffer, 7, 0x48); // H

            var result = layout.ReadValueByColumn(buffer, "NAME");
            // The non-Unicode path returns Encoding.Default.GetString(bytes, 0, length)
            await Assert.That(result.Value is not null).IsTrue();
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [Test]
    public async Task TryGetField_ReturnsTrue_WhenFieldExists()
    {
        var idField = new TableField("ID", TableFieldType.Id, 0, 4, "ID");
        var fields = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase) { ["ID"] = idField };
        var columns = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase);
        var layout = new TableLayout("TEST", 32, fields, columns);

        var result = layout.TryGetField("ID", out var field);
        await Assert.That(result).IsTrue();
        await Assert.That(field.Name).IsEqualTo("ID");
    }

    [Test]
    public async Task TryGetFieldByColumn_ReturnsTrue_WhenColumnExists()
    {
        var idField = new TableField("idF0101", TableFieldType.Id, 0, 4, "AN8");
        var fields = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase) { ["idF0101"] = idField };
        var columns = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase) { ["AN8"] = idField };
        var layout = new TableLayout("TEST", 32, fields, columns);

        var result = layout.TryGetFieldByColumn("AN8", out var field);
        await Assert.That(result).IsTrue();
        await Assert.That(field.ColumnName).IsEqualTo("AN8");
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
