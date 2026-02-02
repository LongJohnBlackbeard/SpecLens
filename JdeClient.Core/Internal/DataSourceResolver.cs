using System.Text;
using JdeClient.Core.Interop;
using static JdeClient.Core.Interop.JdeKernelApi;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.Internal;

/// <summary>
/// Resolves table data sources by querying JDE object mappings.
/// </summary>
internal static class DataSourceResolver
{
    private const int DataSourceBufferSize = 256;
    private const int ObjectNameBufferSize = 64;

    internal static string? ResolveTableDataSource(HUSER hUser, string tableName)
    {
        if (!hUser.IsValid || string.IsNullOrWhiteSpace(tableName))
        {
            return null;
        }

        if (tableName.Equals("F98611", StringComparison.OrdinalIgnoreCase))
        {
            return "System - 920";
        }

        var attempts = new[]
        {
            JDEDB_OMAP_TABLE,
            'T'
        };

        foreach (var type in attempts)
        {
            string? resolved = TryResolveObjectDataSource(hUser, tableName, type);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                return resolved;
            }
        }

        return null;
    }

    private static string? TryResolveObjectDataSource(HUSER hUser, string objectName, char objectType)
    {
        var objectBuffer = new StringBuilder(ObjectNameBufferSize);
        objectBuffer.Append(objectName);
        var buffer = new StringBuilder(DataSourceBufferSize);
        int result = JDB_GetObjectDataSource(
            hUser,
            new NID(objectName),
            objectBuffer,
            objectType,
            buffer);

        if (result == JDEDB_PASSED)
        {
            string value = buffer.ToString().Trim();
            if (string.IsNullOrWhiteSpace(value) || value.Length <= 1)
            {
                return null;
            }

            return value;
        }

        return null;
    }
}
