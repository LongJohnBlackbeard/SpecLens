using JdeClient.Core.Interop;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.Internal;

/// <summary>
/// Abstraction for a JDE session, used to enable unit testing without a live JDE runtime.
/// </summary>
internal interface IJdeSession : IDisposable
{
    /// <summary>
    /// Whether the underlying session is connected and usable.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Object librarian query engine for F9860 access.
    /// </summary>
    IF9860QueryEngine QueryEngine { get; }

    /// <summary>
    /// JDE user handle for API calls.
    /// </summary>
    HUSER UserHandle { get; }

    /// <summary>
    /// Connect to JDE and initialize required handles.
    /// </summary>
    Task ConnectAsync(string? specPath = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Disconnect and cleanup session resources.
    /// </summary>
    Task DisconnectAsync();

    /// <summary>
    /// Ensure the session is connected; throws when not connected.
    /// </summary>
    void EnsureConnected();

    /// <summary>
    /// Execute a unit of work on the session worker thread.
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<T> action, CancellationToken cancellationToken = default);

    /// <summary>
    /// Execute a unit of work on the session worker thread.
    /// </summary>
    Task ExecuteAsync(Action action, CancellationToken cancellationToken = default);
}
