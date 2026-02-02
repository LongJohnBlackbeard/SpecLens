using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.Internal;

/// <summary>
/// Default factory for creating <see cref="EventRulesQueryEngine"/> instances.
/// </summary>
internal sealed class EventRulesQueryEngineFactory : IEventRulesQueryEngineFactory
{
    /// <inheritdoc />
    public IEventRulesQueryEngine Create(HUSER hUser, JdeClientOptions options)
    {
        return new EventRulesQueryEngine(hUser, options);
    }
}
