using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.Internal;

/// <summary>
/// Default data source resolver that delegates to the JDE kernel APIs.
/// </summary>
internal sealed class JdeDataSourceResolver : IDataSourceResolver
{
    /// <inheritdoc />
    public string? ResolveTableDataSource(HUSER hUser, string tableName)
    {
        return DataSourceResolver.ResolveTableDataSource(hUser, tableName);
    }
}
