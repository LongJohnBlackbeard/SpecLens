namespace JdeClient.Core.Models;

/// <summary>
/// Controls where BUSFUNC specs are loaded from.
/// </summary>
public enum JdeBusinessFunctionCodeLocation
{
    /// <summary>
    /// Try local user specs first, then central objects.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Read only local user specs.
    /// </summary>
    Local = 1,

    /// <summary>
    /// Read only central objects specs.
    /// </summary>
    Central = 2
}
