using System;

namespace JdeClient.Core;

/// <summary>
/// Configures JDE client behavior (debugging and query options).
/// </summary>
public sealed class JdeClientOptions
{
    /// <summary>
    /// Enable general debug logging.
    /// </summary>
    public bool EnableDebug { get; set; }

    /// <summary>
    /// Enable spec (spec file) debug logging.
    /// </summary>
    public bool EnableSpecDebug { get; set; }

    /// <summary>
    /// Enable query debug logging.
    /// </summary>
    public bool EnableQueryDebug { get; set; }

    /// <summary>
    /// Optional log sink for debug output (defaults to Console.WriteLine).
    /// </summary>
    public Action<string>? LogSink { get; set; }
    internal bool UseKeyedFetch { get; set; } = true;
    internal bool UseRowLayoutF9860 { get; set; }
    internal bool UseRowLayoutTables { get; set; }
    internal bool UseFetchCols { get; set; }
    internal bool UseProcessFetchedRecord { get; set; }

    internal void WriteLog(string message)
    {
        try
        {
            if (LogSink != null)
            {
                LogSink(message);
                return;
            }

            Console.WriteLine(message);
        }
        catch
        {
            // Swallow to avoid failing on logging sinks.
        }
    }

    internal static JdeClientOptions FromLegacyDebug()
    {
        return new JdeClientOptions
        {
            EnableDebug = Internal.JdeDebug.Enabled,
            EnableSpecDebug = Internal.JdeDebug.UseSpecDebug,
            EnableQueryDebug = Internal.JdeDebug.UseQueryDebug,
            UseKeyedFetch = Internal.JdeDebug.UseKeyedFetch,
            UseRowLayoutF9860 = Internal.JdeDebug.UseRowLayoutF9860,
            UseRowLayoutTables = Internal.JdeDebug.UseRowLayoutTables,
            UseFetchCols = Internal.JdeDebug.UseFetchCols,
            UseProcessFetchedRecord = Internal.JdeDebug.UseProcessFetchedRecord
        };
    }
}
