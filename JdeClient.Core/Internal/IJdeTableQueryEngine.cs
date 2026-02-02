using JdeClient.Core.Models;

namespace JdeClient.Core.Internal;

/// <summary>
/// Abstraction for table/query metadata and row retrieval logic.
/// </summary>
internal interface IJdeTableQueryEngine : IDisposable
{
    /// <summary>
    /// Query a table into a buffered result set.
    /// </summary>
    JdeQueryResult QueryTable(
        string tableName,
        int maxRows,
        IReadOnlyList<JdeFilter> filters,
        string? dataSourceOverride,
        IReadOnlyList<JdeSort>? sorts = null);

    /// <summary>
    /// Count table rows matching the supplied filters.
    /// </summary>
    int CountTable(
        string tableName,
        IReadOnlyList<JdeFilter> filters,
        string? dataSourceOverride);

    /// <summary>
    /// Stream rows from a table without buffering the full result set.
    /// </summary>
    IEnumerable<Dictionary<string, object>> StreamTableRows(
        string tableName,
        int maxRows,
        IReadOnlyList<JdeFilter> filters,
        IReadOnlyList<JdeColumn>? columns = null,
        string? dataSourceOverride = null,
        IReadOnlyList<JdeSort>? sorts = null,
        int? indexId = null,
        bool allowDataSourceFallback = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stream rows from a business view without buffering the full result set.
    /// </summary>
    IEnumerable<Dictionary<string, object>> StreamViewRows(
        string viewName,
        int maxRows,
        IReadOnlyList<JdeFilter> filters,
        IReadOnlyList<JdeColumn>? columns = null,
        string? dataSourceOverride = null,
        IReadOnlyList<JdeSort>? sorts = null,
        bool allowDataSourceFallback = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetch table metadata from specs.
    /// </summary>
    JdeTableInfo GetTableInfo(string tableName, string? description, string? systemCode);

    /// <summary>
    /// Fetch business view metadata from specs.
    /// </summary>
    JdeBusinessViewInfo? GetBusinessViewInfo(string viewName);

    /// <summary>
    /// Get resolved columns for a business view.
    /// </summary>
    List<JdeColumn> GetViewColumns(string viewName);

    /// <summary>
    /// Get index metadata for a table.
    /// </summary>
    List<JdeIndexInfo> GetTableIndexes(string tableName);

    /// <summary>
    /// Get data dictionary titles for the given data items.
    /// </summary>
    List<JdeDataDictionaryTitle> GetDataDictionaryTitles(IEnumerable<string> dataItems, IReadOnlyList<int>? textTypes = null);

    /// <summary>
    /// Resolve data dictionary item names for the provided data items.
    /// </summary>
    List<JdeDataDictionaryItemName> GetDataDictionaryItemNames(IEnumerable<string> dataItems);

    /// <summary>
    /// Retrieve detail records for data dictionary items.
    /// </summary>
    List<JdeDataDictionaryDetails> GetDataDictionaryDetails(IEnumerable<string> dataItems);
}
