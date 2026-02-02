namespace JdeClient.Core.Internal;

/// <summary>
/// Factory for creating table query engines.
/// </summary>
internal interface IJdeTableQueryEngineFactory
{
    /// <summary>
    /// Create a new table query engine instance for the given options.
    /// </summary>
    IJdeTableQueryEngine Create(JdeClientOptions options);
}
