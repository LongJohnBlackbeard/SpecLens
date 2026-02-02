namespace JdeClient.Core.Internal;

/// <summary>
/// Debug flags used to control query and spec behavior across the library.
/// </summary>
public static class JdeDebug
{
    public static bool Enabled { get; set; }
    public static bool UseSpecDebug { get; set; }
    public static bool UseQueryDebug { get; set; }
    public static bool UseKeyedFetch { get; set; } = true;
    public static bool UseRowLayoutF9860 { get; set; } = true;
    public static bool UseRowLayoutTables { get; set; }
    public static bool UseFetchCols { get; set; }
    public static bool UseProcessFetchedRecord { get; set; }
}
