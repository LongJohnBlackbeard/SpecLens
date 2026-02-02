using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.Internal;

/// <summary>
/// Abstraction for resolving data sources for tables.
/// </summary>
internal interface IDataSourceResolver
{
    /// <summary>
    /// Resolve the data source for the specified table using the provided user handle.
    /// </summary>
    string? ResolveTableDataSource(HUSER hUser, string tableName);
}
