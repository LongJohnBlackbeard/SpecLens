namespace JdeClient.Core.Models;

/// <summary>
/// Represents a filter condition for JDE table queries.
/// </summary>
public sealed class JdeFilter
{
    public JdeFilter()
    {
    }

    public JdeFilter(string columnName, string value, JdeFilterOperator @operator)
    {
        ColumnName = columnName;
        Value = value;
        Operator = @operator;
    }

    /// <summary>
    /// Column name to filter on (data item or SQL name).
    /// </summary>
    public string ColumnName { get; set; } = string.Empty;

    /// <summary>
    /// Filter value, stored as a string and converted based on column metadata.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Comparison operator to apply.
    /// </summary>
    public JdeFilterOperator Operator { get; set; } = JdeFilterOperator.Equals;
}

/// <summary>
/// Comparison operators used in JDE table filters.
/// </summary>
public enum JdeFilterOperator
{
    Equals,
    NotEquals,
    LessThan,
    GreaterThan,
    LessThanOrEqual,
    GreaterThanOrEqual,
    Like
}
