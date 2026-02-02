namespace JdeClient.Core.Models;

/// <summary>
/// Represents a JDE object (table, BSFN, UBE, etc.) from Object Librarian (F9860)
/// </summary>
public class JdeObjectInfo
{
    /// <summary>
    /// Object name (e.g., "F0101", "N0100041", "R42565")
    /// </summary>
    public string ObjectName { get; set; } = string.Empty;

    /// <summary>
    /// Object type code (e.g., "TBLE", "BSFN", "UBE")
    /// </summary>
    public string ObjectType { get; set; } = string.Empty;

    /// <summary>
    /// User-friendly description
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// System code (e.g., "01", "03", "42")
    /// </summary>
    public string? SystemCode { get; set; }

    /// <summary>
    /// Product code
    /// </summary>
    public string? ProductCode { get; set; }

    /// <summary>
    /// Object status (e.g., active, inactive)
    /// </summary>
    public string? Status { get; set; }

    public override string ToString() => $"{ObjectName} ({ObjectType})";
}

/// <summary>
/// JDE object types
/// </summary>
public enum JdeObjectType
{
    All,
    Table,              // TBLE
    BusinessFunction,   // BSFN
    NamedEventRule,     // NER
    Report,             // UBE
    Application,        // APPL
    DataStructure,      // DSTR
    BusinessView,       // BSVW
    DataDictionary,     // DD
    Unknown
}
