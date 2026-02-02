namespace JdeClient.Core.Core;

/// <summary>
/// Legacy debug flags. Use <see cref="JdeClientOptions"/> instead.
/// </summary>
[System.Obsolete("Use JdeClientOptions to configure debug/query behavior.")]
public static class JdeDebug
{
    public static bool Enabled
    {
        get => Internal.JdeDebug.Enabled;
        set => Internal.JdeDebug.Enabled = value;
    }

    public static bool UseSpecDebug
    {
        get => Internal.JdeDebug.UseSpecDebug;
        set => Internal.JdeDebug.UseSpecDebug = value;
    }

    public static bool UseQueryDebug
    {
        get => Internal.JdeDebug.UseQueryDebug;
        set => Internal.JdeDebug.UseQueryDebug = value;
    }

    public static bool UseKeyedFetch
    {
        get => Internal.JdeDebug.UseKeyedFetch;
        set => Internal.JdeDebug.UseKeyedFetch = value;
    }

    public static bool UseRowLayoutF9860
    {
        get => Internal.JdeDebug.UseRowLayoutF9860;
        set => Internal.JdeDebug.UseRowLayoutF9860 = value;
    }

    public static bool UseRowLayoutTables
    {
        get => Internal.JdeDebug.UseRowLayoutTables;
        set => Internal.JdeDebug.UseRowLayoutTables = value;
    }

    public static bool UseFetchCols
    {
        get => Internal.JdeDebug.UseFetchCols;
        set => Internal.JdeDebug.UseFetchCols = value;
    }

    public static bool UseProcessFetchedRecord
    {
        get => Internal.JdeDebug.UseProcessFetchedRecord;
        set => Internal.JdeDebug.UseProcessFetchedRecord = value;
    }
}
