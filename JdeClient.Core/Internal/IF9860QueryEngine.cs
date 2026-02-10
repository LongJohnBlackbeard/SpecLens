using JdeClient.Core.Interop;
using static JdeClient.Core.Interop.JdeStructures;
using JdeClient.Core.Models;

namespace JdeClient.Core.Internal;

/// <summary>
/// Abstraction for querying the F9860 Object Librarian table.
/// </summary>
internal interface IF9860QueryEngine : IDisposable
{
    /// <summary>
    /// Initialize the engine before querying.
    /// </summary>
    void Initialize();

    /// <summary>
    /// JDE user handle associated with the engine.
    /// </summary>
    HUSER UserHandle { get; }

    /// <summary>
    /// JDE environment handle associated with the engine.
    /// </summary>
    HENV EnvironmentHandle { get; }

    /// <summary>
    /// Query objects from F9860 with optional filters.
    /// </summary>
    List<JdeObjectInfo> QueryObjects(
        JdeObjectType? objectType = null,
        string? namePattern = null,
        string? descriptionPattern = null,
        int maxResults = 0);

    /// <summary>
    /// Retrieve a single object by name and type.
    /// </summary>
    JdeObjectInfo? GetObjectByName(string objectName, JdeObjectType objectType);
}
