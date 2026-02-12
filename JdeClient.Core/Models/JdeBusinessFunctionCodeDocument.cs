using JdeClient.Core.Interop;

namespace JdeClient.Core.Models;

/// <summary>
/// Represents raw BUSFUNC spec payload and best-effort source text for a C business function.
/// </summary>
public sealed class JdeBusinessFunctionCodeDocument
{
    /// <summary>
    /// Business function object name (OBNM).
    /// </summary>
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// C business function name (FNNM).
    /// </summary>
    public string FunctionName { get; set; } = string.Empty;

    /// <summary>
    /// Source module/file name (SRCFNM) when available.
    /// </summary>
    public string SourceFileName { get; set; } = string.Empty;

    /// <summary>
    /// JDE version field (JDEVERS) when available.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Native spec data type returned by jdeSpecFetch.
    /// </summary>
    public JdeStructures.JdeSpecDataType DataType { get; set; }

    /// <summary>
    /// Native payload size in bytes.
    /// </summary>
    public int PayloadSize { get; set; }

    /// <summary>
    /// Best-effort decoded source text from the native payload.
    /// </summary>
    public string SourceCode { get; set; } = string.Empty;

    /// <summary>
    /// Best-effort decoded header text from the native payload.
    /// </summary>
    public string HeaderCode { get; set; } = string.Empty;

    /// <summary>
    /// Indicates whether decoded text looks like C source.
    /// </summary>
    public bool SourceLooksLikeCode { get; set; }

    /// <summary>
    /// Raw payload bytes returned by jdeSpecFetch.
    /// </summary>
    public byte[] Payload { get; set; } = Array.Empty<byte>();
}
