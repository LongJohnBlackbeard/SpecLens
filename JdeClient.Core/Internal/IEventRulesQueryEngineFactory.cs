using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.Internal;

/// <summary>
/// Factory for creating event rules query engines.
/// </summary>
internal interface IEventRulesQueryEngineFactory
{
    /// <summary>
    /// Create a new event rules query engine for the given user handle and options.
    /// </summary>
    IEventRulesQueryEngine Create(HUSER hUser, JdeClientOptions options);
}
