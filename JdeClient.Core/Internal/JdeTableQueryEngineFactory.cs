namespace JdeClient.Core.Internal;

/// <summary>
/// Default factory for creating <see cref="JdeTableQueryEngine"/> instances.
/// </summary>
internal sealed class JdeTableQueryEngineFactory : IJdeTableQueryEngineFactory
{
    /// <inheritdoc />
    public IJdeTableQueryEngine Create(JdeClientOptions options)
    {
        return new JdeTableQueryEngine(options);
    }
}
