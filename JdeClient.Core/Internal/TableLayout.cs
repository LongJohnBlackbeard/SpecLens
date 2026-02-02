using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

namespace JdeClient.Core.Internal;

/// <summary>
/// Logical field types used by table layout parsing.
/// </summary>
internal enum TableFieldType
{
    JCharArray,
    JCharSingle,
    Id,
    JdeDate,
    MathNumeric
}

/// <summary>
/// Describes a field in a table layout buffer.
/// </summary>
internal sealed class TableField
{
    public TableField(string name, TableFieldType type, int offset, int length, string? columnName)
    {
        Name = name;
        Type = type;
        Offset = offset;
        Length = length;
        ColumnName = columnName;
    }

    public string Name { get; }
    public TableFieldType Type { get; }
    public int Offset { get; }
    public int Length { get; }
    public string? ColumnName { get; }
}

/// <summary>
/// Parses table row buffers based on precomputed layout metadata.
/// </summary>
internal sealed class TableLayout
{
    private static readonly Encoding JCharEncoding = Encoding.Unicode;
    private readonly Dictionary<string, TableField> _fields;
    private readonly Dictionary<string, TableField> _columns;

    public TableLayout(string tableName, int size, Dictionary<string, TableField> fields, Dictionary<string, TableField> columns)
    {
        TableName = tableName;
        Size = size;
        _fields = fields;
        _columns = columns;
    }

    public string TableName { get; }
    public int Size { get; }

    /// <summary>
    /// Try to resolve a field by layout name.
    /// </summary>
    public bool TryGetField(string name, out TableField field) => _fields.TryGetValue(name, out field);

    /// <summary>
    /// Try to resolve a field by column name.
    /// </summary>
    public bool TryGetFieldByColumn(string columnName, out TableField field) => _columns.TryGetValue(columnName, out field);

    /// <summary>
    /// Read a JCHAR field from a row buffer.
    /// </summary>
    public string ReadJCharString(IntPtr buffer, string fieldName)
    {
        if (!TryGetField(fieldName, out var field))
        {
            return string.Empty;
        }

        int byteCount = field.Type switch
        {
            TableFieldType.JCharArray => field.Length * 2,
            TableFieldType.JCharSingle => 2,
            _ => 0
        };

        if (byteCount <= 0)
        {
            return string.Empty;
        }

        var bytes = new byte[byteCount];
        System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(buffer, field.Offset), bytes, 0, byteCount);
        return JCharEncoding.GetString(bytes).TrimEnd('\0').TrimEnd();
    }

    /// <summary>
    /// Read a value based on a column name.
    /// </summary>
    public TableValue ReadValueByColumn(IntPtr buffer, string columnName)
    {
        if (!TryGetFieldByColumn(columnName, out var field))
        {
            return new TableValue(TableFieldType.JCharArray, string.Empty);
        }

        return ReadValue(buffer, field);
    }


    /// <summary>
    /// Read a value based on a layout field name.
    /// </summary>
    public TableValue ReadValueByField(IntPtr buffer, string fieldName)
    {
        if (!TryGetField(fieldName, out var field))
        {
            return new TableValue(TableFieldType.JCharArray, string.Empty);
        }

        return ReadValue(buffer, field);
    }

    private TableValue ReadValue(IntPtr buffer, TableField field)
    {
        return field.Type switch
        {
            TableFieldType.JCharArray => new TableValue(field.Type, ReadJChar(buffer, field, field.Length)),
            TableFieldType.JCharSingle => new TableValue(field.Type, ReadJChar(buffer, field, 1)),
            TableFieldType.Id => new TableValue(field.Type, ReadInt32(buffer, field)),
            TableFieldType.JdeDate => new TableValue(field.Type, ReadJdeDate(buffer, field)),
            TableFieldType.MathNumeric => new TableValue(field.Type, ReadMathNumeric(buffer, field)),
            _ => new TableValue(field.Type, string.Empty)
        };
    }

    private static string ReadJChar(IntPtr buffer, TableField field, int length)
    {
        int byteCount = length * 2;
        var bytes = new byte[byteCount];
        System.Runtime.InteropServices.Marshal.Copy(IntPtr.Add(buffer, field.Offset), bytes, 0, byteCount);
        if (LooksLikeUnicode(bytes, out bool bigEndian))
        {
            return (bigEndian ? Encoding.BigEndianUnicode : JCharEncoding)
                .GetString(bytes)
                .TrimEnd('\0')
                .TrimEnd();
        }

        return Encoding.Default.GetString(bytes, 0, length).TrimEnd('\0').TrimEnd();
    }

    private static int ReadInt32(IntPtr buffer, TableField field)
    {
        return System.Runtime.InteropServices.Marshal.ReadInt32(buffer, field.Offset);
    }

    private static DateTime? ReadJdeDate(IntPtr buffer, TableField field)
    {
        short year = System.Runtime.InteropServices.Marshal.ReadInt16(buffer, field.Offset);
        short month = System.Runtime.InteropServices.Marshal.ReadInt16(buffer, field.Offset + 2);
        short day = System.Runtime.InteropServices.Marshal.ReadInt16(buffer, field.Offset + 4);
        if (year <= 0 || month <= 0 || day <= 0)
        {
            return null;
        }

        try
        {
            return new DateTime(year, month, day);
        }
        catch
        {
            return null;
        }
    }

    private static string ReadMathNumeric(IntPtr buffer, TableField field)
    {
        return MathNumericParser.ToString(IntPtr.Add(buffer, field.Offset));
    }

    private static bool LooksLikeUnicode(byte[] bytes, out bool bigEndian)
    {
        bigEndian = false;
        if (bytes.Length < 2)
        {
            return true;
        }

        int evenZero = 0;
        int oddZero = 0;
        int pairs = bytes.Length / 2;
        for (int i = 0; i < bytes.Length - 1; i += 2)
        {
            if (bytes[i] == 0)
            {
                evenZero++;
            }
            if (bytes[i + 1] == 0)
            {
                oddZero++;
            }
        }

        if (evenZero > pairs / 2 || oddZero > pairs / 2)
        {
            bigEndian = evenZero > oddZero;
            return true;
        }

        return false;
    }
}

internal readonly struct TableValue
{
    public TableValue(TableFieldType fieldType, object? value)
    {
        FieldType = fieldType;
        Value = value;
    }

    public TableFieldType FieldType { get; }
    public object? Value { get; }
}

[ExcludeFromCodeCoverage]
internal static class TableLayoutLoader
{
    private const string IncludeRoot = @"C:\E920_1\DV920\include64";
    private static readonly ConcurrentDictionary<string, TableLayout?> Cache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Regex StructStart = new(@"^\s*typedef\s+struct\s*$", RegexOptions.IgnoreCase);
    private static readonly Regex StructEnd = new(@"^\s*}\s*(\w+)\s*,?", RegexOptions.IgnoreCase);
    private static readonly Regex JCharArray = new(@"^\s*JCHAR\s+(\w+)\s*\[(\d+)\]\s*;", RegexOptions.IgnoreCase);
    private static readonly Regex JCharSingle = new(@"^\s*JCHAR\s+(\w+)\s*;", RegexOptions.IgnoreCase);
    private static readonly Regex IdField = new(@"^\s*ID\s+(\w+)\s*;", RegexOptions.IgnoreCase);
    private static readonly Regex JdeDateField = new(@"^\s*JDEDATE\s+(\w+)\s*;", RegexOptions.IgnoreCase);
    private static readonly Regex MathNumericField = new(@"^\s*MATH_NUMERIC\s+(\w+)\s*;", RegexOptions.IgnoreCase);
    private static readonly Regex NidDefine = new(@"^\s*#define\s+NID_\w+\s+_J\(\""(?<name>[^\""]+)\""\)", RegexOptions.IgnoreCase);
    private static readonly Regex OffsetComment = new(@"/\*\s*(\d+)\s*to\s*(\d+)\s*\*/", RegexOptions.IgnoreCase);

    public static TableLayout? Load(string tableName)
    {
        return Cache.GetOrAdd(tableName, key => LoadInternal(tableName, tableName));
    }

    public static TableLayout? LoadKeyLayout(string tableName, int indexId)
    {
        string key = $"{tableName}#KEY{indexId}";
        string structName = $"KEY{indexId}_{tableName}";
        return Cache.GetOrAdd(key, _ => LoadInternal(tableName, structName));
    }

    private static TableLayout? LoadInternal(string tableName, string structName)
    {
        string headerPath = Path.Combine(IncludeRoot, $"{tableName.ToLowerInvariant()}.h");
        if (!File.Exists(headerPath))
        {
            return null;
        }

        bool inStruct = false;
        int offset = 0;
        int maxEnd = -1;
        string? pendingColumn = null;
        Dictionary<string, TableField>? fields = null;
        Dictionary<string, TableField>? columns = null;

        foreach (string rawLine in File.ReadLines(headerPath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (!inStruct)
            {
                if (StructStart.IsMatch(line))
                {
                    inStruct = true;
                    offset = 0;
                    maxEnd = -1;
                    pendingColumn = null;
                    fields = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase);
                    columns = new Dictionary<string, TableField>(StringComparer.OrdinalIgnoreCase);
                }
                continue;
            }

            var nidMatch = NidDefine.Match(line);
            if (nidMatch.Success)
            {
                pendingColumn = nidMatch.Groups["name"].Value;
                continue;
            }

            if (line.StartsWith("#", StringComparison.Ordinal))
            {
                continue;
            }

            var endMatch = StructEnd.Match(line);
            if (endMatch.Success)
            {
                string foundName = endMatch.Groups[1].Value;
                if (string.Equals(foundName, structName, StringComparison.OrdinalIgnoreCase))
                {
                    int size = maxEnd >= 0 ? maxEnd + 1 : offset;
                    return new TableLayout(tableName, size, fields ?? new Dictionary<string, TableField>(), columns ?? new Dictionary<string, TableField>());
                }
                inStruct = false;
                continue;
            }

            int? commentStart = null;
            int? commentEnd = null;
            var offsetMatch = OffsetComment.Match(line);
            if (offsetMatch.Success)
            {
                commentStart = int.Parse(offsetMatch.Groups[1].Value);
                commentEnd = int.Parse(offsetMatch.Groups[2].Value);
            }

            var match = JCharArray.Match(line);
            if (match.Success)
            {
                string name = match.Groups[1].Value;
                int length = int.Parse(match.Groups[2].Value);
                int byteLength = length * 2;
                int offsetScale = DetermineOffsetScale(length, commentStart, commentEnd);
                ResolveFieldOffsets(commentStart, offset, offsetScale, byteLength, out int fieldOffset, out int fieldEnd);
                var field = new TableField(name, TableFieldType.JCharArray, fieldOffset, length, pendingColumn);
                fields[name] = field;
                if (!string.IsNullOrWhiteSpace(pendingColumn))
                {
                    columns[pendingColumn] = field;
                    pendingColumn = null;
                }
                maxEnd = Math.Max(maxEnd, fieldEnd);
                offset = Math.Max(offset, fieldOffset + byteLength);
                continue;
            }

            match = JCharSingle.Match(line);
            if (match.Success)
            {
                string name = match.Groups[1].Value;
                int byteLength = 2;
                int offsetScale = DetermineOffsetScale(1, commentStart, commentEnd);
                ResolveFieldOffsets(commentStart, offset, offsetScale, byteLength, out int fieldOffset, out int fieldEnd);
                var field = new TableField(name, TableFieldType.JCharSingle, fieldOffset, 1, pendingColumn);
                fields[name] = field;
                if (!string.IsNullOrWhiteSpace(pendingColumn))
                {
                    columns[pendingColumn] = field;
                    pendingColumn = null;
                }
                maxEnd = Math.Max(maxEnd, fieldEnd);
                offset = Math.Max(offset, fieldOffset + byteLength);
                continue;
            }

            match = IdField.Match(line);
            if (match.Success)
            {
                string name = match.Groups[1].Value;
                int byteLength = 4;
                ResolveFieldOffsets(commentStart, offset, 1, byteLength, out int fieldOffset, out int fieldEnd);
                var field = new TableField(name, TableFieldType.Id, fieldOffset, 4, pendingColumn);
                fields[name] = field;
                if (!string.IsNullOrWhiteSpace(pendingColumn))
                {
                    columns[pendingColumn] = field;
                    pendingColumn = null;
                }
                maxEnd = Math.Max(maxEnd, fieldEnd);
                offset = Math.Max(offset, fieldOffset + byteLength);
                continue;
            }

            match = JdeDateField.Match(line);
            if (match.Success)
            {
                string name = match.Groups[1].Value;
                int byteLength = 6;
                ResolveFieldOffsets(commentStart, offset, 1, byteLength, out int fieldOffset, out int fieldEnd);
                var field = new TableField(name, TableFieldType.JdeDate, fieldOffset, 6, pendingColumn);
                fields[name] = field;
                if (!string.IsNullOrWhiteSpace(pendingColumn))
                {
                    columns[pendingColumn] = field;
                    pendingColumn = null;
                }
                maxEnd = Math.Max(maxEnd, fieldEnd);
                offset = Math.Max(offset, fieldOffset + byteLength);
                continue;
            }

            match = MathNumericField.Match(line);
            if (match.Success)
            {
                string name = match.Groups[1].Value;
                int byteLength = 49;
                ResolveFieldOffsets(commentStart, offset, 1, byteLength, out int fieldOffset, out int fieldEnd);
                var field = new TableField(name, TableFieldType.MathNumeric, fieldOffset, 49, pendingColumn);
                fields[name] = field;
                if (!string.IsNullOrWhiteSpace(pendingColumn))
                {
                    columns[pendingColumn] = field;
                    pendingColumn = null;
                }
                maxEnd = Math.Max(maxEnd, fieldEnd);
                offset = Math.Max(offset, fieldOffset + byteLength);
            }
        }

        return null;
    }

    private static int DetermineOffsetScale(int length, int? commentStart, int? commentEnd)
    {
        if (commentStart.HasValue && commentEnd.HasValue)
        {
            int span = commentEnd.Value - commentStart.Value + 1;
            if (span == length)
            {
                return 2;
            }
        }

        return 1;
    }

    private static void ResolveFieldOffsets(
        int? commentStart,
        int fallbackOffset,
        int offsetScale,
        int byteLength,
        out int fieldOffset,
        out int fieldEnd)
    {
        if (commentStart.HasValue)
        {
            fieldOffset = commentStart.Value * offsetScale;
        }
        else
        {
            fieldOffset = fallbackOffset;
        }

        fieldEnd = fieldOffset + byteLength - 1;
    }
}
