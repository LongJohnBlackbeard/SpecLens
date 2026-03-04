using System.Collections;

namespace JdeClient.Core.Models;

/// <summary>
/// Streams table rows without buffering the full result set in memory.
/// </summary>
public sealed class JdeQueryStream : IEnumerable<JdeRow>
{
    private readonly Func<IEnumerable<JdeRow>> _enumerate;

    internal JdeQueryStream(
        string tableName,
        IReadOnlyList<string> columnNames,
        int? maxRows,
        Func<IEnumerable<JdeRow>> enumerate)
    {
        TableName = tableName;
        ColumnNames = columnNames;
        MaxRows = maxRows;
        _enumerate = enumerate;
    }

    /// <summary>
    /// Table name that will be queried.
    /// </summary>
    public string TableName { get; }

    /// <summary>
    /// Column names in the result set.
    /// </summary>
    public IReadOnlyList<string> ColumnNames { get; }

    /// <summary>
    /// Maximum rows requested, or null for unlimited.
    /// </summary>
    public int? MaxRows { get; }

    public IEnumerator<JdeRow> GetEnumerator()
    {
        return _enumerate().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
