using System.Runtime.InteropServices;

namespace JdeClient.Core.Interop;

/// <summary>
/// F9860 - Object Librarian Master File
/// This table contains all JDE objects (tables, business functions, UBEs, applications, etc.)
/// </summary>
internal static class F9860Structures
{
    /// <summary>
    /// F9860 column names
    /// Used with JDB_GetTableColValue to extract specific columns
    /// </summary>
    internal static class Columns
    {
        public const string OBNM = "OBNM";           // Object Name
        public const string FUNO = "FUNO";           // Object Type
        public const string SY = "SY";               // Product/System Code
        public const string MD = "MD";               // Description
        public const string USER = "USER";           // User ID
        public const string PID = "PID";             // Program ID
        public const string JOBN = "JOBN";           // Work Station ID
        public const string UPMJ = "UPMJ";           // Date - Updated (Julian)
        public const string UPMT = "UPMT";           // Time - Last Updated
    }

    /// <summary>
    /// Valid object types in F9860.FUNO
    /// </summary>
    internal static class ObjectTypes
    {
        public const string Table = "TBLE";
        public const string BusinessFunction = "BSFN";
        public const string NamedEventRule = "NER";
        public const string Report = "UBE";
        public const string Application = "APPL";
        public const string DataStructure = "DSTR";
        public const string BusinessView = "BSVW";
        public const string DataDictionary = "DD";
        public const string MediaObject = "MDBF";
    }
}
