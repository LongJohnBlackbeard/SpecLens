namespace JdeClient.Core.Models;

/// <summary>
/// Represents metadata for a JDE business view (BSVW).
/// </summary>
public sealed class JdeBusinessViewInfo
{
    public string ViewName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? SystemCode { get; set; }
    public List<JdeBusinessViewTable> Tables { get; } = new();
    public List<JdeBusinessViewColumn> Columns { get; } = new();
    public List<JdeBusinessViewJoin> Joins { get; } = new();
}

/// <summary>
/// Represents a table definition within a business view.
/// </summary>
public sealed class JdeBusinessViewTable
{
    public string TableName { get; set; } = string.Empty;
    public int InstanceCount { get; set; }
    public int PrimaryIndexId { get; set; }
}

/// <summary>
/// Represents a column definition within a business view.
/// </summary>
public sealed class JdeBusinessViewColumn
{
    public int Sequence { get; set; }
    public string DataItem { get; set; } = string.Empty;
    public string TableName { get; set; } = string.Empty;
    public int InstanceId { get; set; }
    public int DataType { get; set; }
    public int Length { get; set; }
    public int Decimals { get; set; }
    public int DisplayDecimals { get; set; }
    public char TypeCode { get; set; }
    public char ClassCode { get; set; }
}

/// <summary>
/// Represents a join definition within a business view.
/// </summary>
public sealed class JdeBusinessViewJoin
{
    public string ForeignTable { get; set; } = string.Empty;
    public string ForeignColumn { get; set; } = string.Empty;
    public int ForeignInstanceId { get; set; }
    public string PrimaryTable { get; set; } = string.Empty;
    public string PrimaryColumn { get; set; } = string.Empty;
    public int PrimaryInstanceId { get; set; }
    public string JoinOperator { get; set; } = string.Empty;
    public string JoinType { get; set; } = string.Empty;
}
