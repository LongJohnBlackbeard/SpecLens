using System.Runtime.InteropServices;

namespace JdeClient.Core.Interop;

/// <summary>
/// Native C structures used by JDE APIs
/// </summary>
public static class JdeStructures
{
    #region Constants

    // JDB_* API Return codes (db_defines.h)
    public const int JDEDB_PASSED = 1;
    public const int JDEDB_FAILED = 0;
    public const int JDEDB_NO_MORE_DATA = 0;
    public const int JDEDB_SKIPPED = 3;

    // jdeSpec* API Return codes
    public const int JDESPEC_SUCCESS = 0x1a;        // 26 - Success / Found / More data
    public const int JDESPEC_NOT_FOUND = 0x1e;      // 30 - Not found / Error / No more data

    // Commit modes
    public const int JDEDB_COMMIT_AUTO = 0;
    public const int JDEDB_COMMIT_MANUAL = 1;

    // JDB_SetSelection comparison operators (db_defines.h)
    public const int JDEDB_CMP_LE = 0;      // <=
    public const int JDEDB_CMP_GE = 1;      // >=
    public const int JDEDB_CMP_EQ = 2;      // ==
    public const int JDEDB_CMP_LT = 3;      // <
    public const int JDEDB_CMP_GT = 4;      // >
    public const int JDEDB_CMP_NE = 5;      // !=
    public const int JDEDB_CMP_IN = 6;      // IN
    public const int JDEDB_CMP_NI = 7;      // NOT IN
    public const int JDEDB_CMP_BW = 8;      // BETWEEN
    public const int JDEDB_CMP_NB = 9;      // NOT BETWEEN
    public const int JDEDB_CMP_LK = 10;     // LIKE
    public const int JDEDB_CMP_NL = 11;     // NOT LIKE

    // JDB_SetSelection AND/OR operators (db_defines.h)
    public const int JDEDB_ANDOR_AND = 0;   // AND condition
    public const int JDEDB_ANDOR_OR = 1;    // OR condition

    // JDB_SetSelection set modes (from JDE API docs)
    public const int JDEDB_SET_REPLACE = 0;    // Replace existing selections
    public const int JDEDB_SET_APPEND = 1;     // Append to existing selections
    public const int JDEDB_SORT_ASC = 0;       // Ascending order
    public const int JDEDB_SORT_DESC = 1;      // Descending order

    // JDB_ProcessFetchedRecord flags (JDEKDFN.H)
    public const int RECORD_CONVERT = 0x00000001;
    public const int RECORD_PROCESS = 0x00000002;
    public const int RECORD_TRIGGERS = 0x00000004;

    // SQL name lengths (JDEKDFN.H)
    public const int JDE_SQL_COLUMN_LENGTH = 31;
    public const int JDE_SQL_TABLE_LENGTH = 31;

    // Everest data types (JDEKDFN.H)
    public const int EVDT_CHAR = 1;
    public const int EVDT_STRING = 2;
    public const int EVDT_SHORT = 3;
    public const int EVDT_USHORT = 4;
    public const int EVDT_LONG = 5;
    public const int EVDT_ULONG = 6;
    public const int EVDT_ID = 7;
    public const int EVDT_ID2 = 8;
    public const int EVDT_MATH_NUMERIC = 9;
    public const int EVDT_JDEDATE = 11;
    public const int EVDT_BYTE = 13;
    public const int EVDT_BOOL = 14;
    public const int EVDT_INT = 15;
    public const int EVDT_HANDLE = 16;
    public const int EVDT_LONGVARCHAR = 17;
    public const int EVDT_LONGVARBINARY = 18;
    public const int EVDT_BINARY = 19;
    public const int EVDT_VARSTRING = 20;
    public const int EVDT_TEXT = 21;
    public const int EVDT_NID = 22;
    public const int EVDT_UINT = 24;
    public const int EVDT_TIMESTAMP = 50;
    public const int EVDT_JDEUTIME = 55;

    // Data dictionary text types (JDEKDCL.H)
    public const int DDT_ALPHA_DESC = 'A';
    public const int DDT_ROW_DESC = 'R';
    public const int DDT_COL_TITLE = 'C';
    public const int DDT_GLOSSARY = 'H';

    // Object mapping types for JDB_GetObjectDataSource (from JDEKDCL.H)
    public const char JDEDB_OMAP_TABLE = (char)0;
    public const char JDEDB_OMAP_OBJECT = (char)1;
    public const char JDEDB_OMAP_BSFN = (char)2;
    public const char JDEDB_OMAP_UBE = (char)3;
    public const char JDEDB_OMAP_GT = (char)4;

    // Table index IDs (from DV920 headers)
    public const int ID_F0101_ADDRESS = 1;

    #endregion

    #region Enums
    public enum JdeSpecType
    {
        // Matches JDESPECTYPE_GBRSPEC in specencapstructures.h (0-based enum).
        GbrSpec = 13,
        // Legacy spec table index used by some tools releases (SPEC_GBRSPEC/RDB_GBRSPEC).
        GbrSpecLegacy = 9
    }

    public enum JdeSpecFileType
    {
        Undefined = 0,
        BusFunc = 1,
        Dstmpl = 9,
        GbrLink = 12,
        GbrSpec = 13
    }

    public enum JdeSpecLocation
    {
        Undefined = 0,
        CentralObjects = 1,
        LocalUser = 2,
        RemoteUser = 3,
        Temper = 4
    }

    public enum JdeSpecDataType
    {
        Undefined = 0,
        RawBlob = 1,
        ResolvedTam = 2,
        Xml = 3,
        ErrorData = 4
    }

    public enum JdeSpecDbType
    {
        Undefined = 0,
        Tam = 1,
        Rdb = 2,
        File = 3,
        Cache = 4,
        None = 5
    }

    public enum JdeByteOrder
    {
        LittleEndian = 0,
        BigEndian = 1
    }

    public enum JdeUnpackSpecStatus
    {
        Success = 0,
        InvalidNullInput = 1,
        CheckFails = 2,
        OutOfMemory = 3,
        UnresolvedBlobFormat = 4,
        UnknownSpecType = 5,
        InvalidErType = 6,
        InvalidDsObjType = 7,
        InvalidDrType = 8,
        InvalidSectionType = 9,
        ConvertBlobError = 10
    }

    public enum JdeB733UnpackSpecStatus
    {
        Success = 0,
        UnknownSpecType = 1
    }
    #endregion

    #region Spec Encapsulation Structures
    [StructLayout(LayoutKind.Sequential)]
    public struct JdeSpecData
    {
        public uint DataLen;
        public JdeSpecDataType DataType;
        public IntPtr SpecData;
        public IntPtr SpecInfo;
        public IntPtr RdbRecord;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct JdeSpecLastError
    {
        public int Result;
        public JdeSpecDbType DbType;
        public int HasExtraInfo;
        public int ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct JdeSpecKeyGbrSpec
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 37)]
        public string EventSpecKey;

        public int Sequence;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct JdeSpecKeyDstmpl
    {
        public NID TemplateName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct JdeSpecKeyBusFuncByFunction
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
        public string FunctionName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct JdeSpecKeyBusFuncByObject
    {
        public NID ObjectName;
    }
    #endregion

    #region Handle Structures
    /// <summary>
    /// Environment handle - represents a JDE environment context
    /// </summary>
    public struct HENV
    {
        public IntPtr Handle;

        public bool IsValid => Handle != IntPtr.Zero;
    }

    /// <summary>
    /// User handle - represents a JDE user session
    /// </summary>
    public struct HUSER
    {
        public IntPtr Handle;

        public bool IsValid => Handle != IntPtr.Zero;
    }

    /// <summary>
    /// Request handle - represents an open table cursor
    /// </summary>
    public struct HREQUEST
    {
        public IntPtr Handle;

        public bool IsValid => Handle != IntPtr.Zero;
    }

    /// <summary>
    /// NID (Name ID) - used for table and column names
    /// Fixed-size 11-character Unicode string (UTF-16LE) per JDEKDCL.H
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public struct NID
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 11)]
        public string Value;

        public NID(string value)
        {
            Value = value ?? string.Empty;
        }

        public override string ToString() => Value;
    }

    /// <summary>
    /// ID - used for index IDs and numeric identifiers
    /// </summary>
    public struct ID
    {
        public int Value;

        public ID(int value)
        {
            Value = value;
        }

        public override string ToString() => Value.ToString();
    }

    /// <summary>
    /// DBREF - Database reference structure for identifying table/column.
    /// On Windows x64, JDE_NATIVE_ALIGNMENT is enabled (JDENV.H), so this uses native alignment
    /// and includes the alignment padding member.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DBREF
    {
        public NID szTable;      // Table name (NID = 11 Unicode chars)
        public int idInstance;   // Instance ID (usually 0) - 4 bytes
        public NID szDict;       // Dictionary/column name (NID = 11 Unicode chars)
        public IntPtr nativeAlignmentPadding;

        public DBREF(string tableName, string columnName, int instance = 0)
        {
            szTable = new NID(tableName ?? string.Empty);
            szDict = new NID(columnName ?? string.Empty);
            idInstance = instance;
            nativeAlignmentPadding = IntPtr.Zero;
        }
    }

    /// <summary>
    /// LVARLEN - variable-length spec record size (TAM).
    /// </summary>
    public struct LVARLEN
    {
        public uint Value;
    }

    /// <summary>
    /// BLOBVALUE - binary large object value (JDETYPES.H).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct JdeBlobValue
    {
        public IntPtr lpValue;
        public uint lSize;
        public uint lMaxSize;
        public uint lCharSize;
    }

    /// <summary>
    /// Business View spec header (BOBSPEC).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct BOB_HEADER
    {
        public LVARLEN lVarLen;
        public ID idFormatNum;
        public NID szView;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 61)]
        public string szDescription;
        public ushort nTableCount;
        public ushort nPrimaryKeyColumnCount;
        public ushort nColumnCount;
        public ushort nJoinCount;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 5)]
        public string szSystemCode;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 5)]
        public string szBusinessViewUse;
        public ID idStyle;
        public short nType;
        public NID szTable;
        public IntPtr nativeAlignmentPadding;
    }

    /// <summary>
    /// Business View table entry (BOBSPEC).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct BOB_TABLE
    {
        public ID idFormatNum;
        public ID idPrimaryIndex;
        public ushort nNumInstances;
        public NID szTable;
        public IntPtr nativeAlignmentPadding;
    }

    /// <summary>
    /// Business View column entry (BOBSPEC).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct BOB_COLUMN
    {
        public ID idFormatNum;
        public ID idInstance;
        public ushort iFlags;
        public ushort nSeq;
        public char cType;
        public ID idEvType;
        public char cClass;
        public ID idLength;
        public ushort nDecimals;
        public ushort nDispDecimals;
        public ID idHelpText;
        public NID szTable;
        public NID szDict;
        public IntPtr nativeAlignmentPadding;
    }

    /// <summary>
    /// Business View join entry (BOBSPEC).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct BOB_JOIN
    {
        public ID idFormatNum;
        public ID idFInstance;
        public ID idPInstance;
        public byte chOperator;
        public byte chType;
        public NID szFTable;
        public NID szFDict;
        public NID szPTable;
        public NID szPDict;
        public IntPtr nativeAlignmentPadding;
    }

    /// <summary>
    /// Business View grid table header (BOBSPEC).
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct BOB_GTHEADER
    {
        public uint nGTCount;
        public IntPtr nativeAlignmentPadding;
    }

    /// <summary>
    /// Business View grid table entry (BOBSPEC).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct BOB_GT
    {
        public ID idFormatNum;
        public NID szGT;
        public IntPtr nativeAlignmentPadding;
    }

    /// <summary>
    /// Business View runtime structure (returned by JDBRS_GetBOBSpecs).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct BOB
    {
        public ID idVersion;
        public IntPtr lpHeader;
        public IntPtr lpTables;
        public IntPtr lpDBRefs;
        public IntPtr lpColumns;
        public IntPtr lpJoins;
        public IntPtr lpszQuery;
        public uint lTotalSize;
        public IntPtr hNode;
        public NID szView;
        public uint nGTCount;
        public IntPtr lpGTs;
    }

    /// <summary>
    /// Table specification cache structure
    /// WARNING: This is a simplified version - actual structure has many more fields
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct TABLECACHE
    {
        public IntPtr lpszTableName;  // Pointer to table name string
        public int nNumColumns;       // Number of columns
        public IntPtr lpColumnList;   // Pointer to column array
        // Note: Many more fields exist in actual structure
    }

    /// <summary>
    /// Column specification cache structure
    /// WARNING: This is a simplified version - actual structure has many more fields
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct COLUMNCACHE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
        public string szColumnName;
        public int nDataType;
        public int nLength;
        public int nDecimals;
        // Note: Many more fields exist in actual structure
    }

    /// <summary>
    /// Repository Info structure - used with jdeSpecOpenLocal/Remote
    /// Contains application name and username for spec file access
    ///
    /// CORRECTED LAYOUT (from rdx_inspect.log):
    /// - Offset 0x00: dwFlags (4 bytes) = 0x03d1
    /// - Offset 0x04: szAppName (40 bytes = 20 Unicode chars)
    /// - Offset 0x2C (44): szUserName (40 bytes = 20 Unicode chars)
    /// Total size: 84 bytes
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct RepositoryInfo
    {
        public uint dwFlags;                                     // Offset +0: 0x03d1 (977 decimal)

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string szAppName;                                 // Offset +4: Application name (40 bytes)

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 20)]
        public string szUserName;                                // Offset +44 (0x2C): Username (40 bytes)

        public RepositoryInfo(string appName, string userName, uint flags = 0x03d1)
        {
            dwFlags = flags;
            szAppName = appName ?? string.Empty;
            szUserName = userName ?? string.Empty;
        }
    }

    /// <summary>
    /// MATH_NUMERIC - numeric value structure (JDETYPES.H)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MATH_NUMERIC
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 33)]
        public byte[] String;

        public byte Sign;
        public byte EditCode;
        public short nDecimalPosition;
        public short nLength;
        public ushort wFlags;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public byte[] szCurrency;

        public short nCurrencyDecimals;
        public short nPrecision;

        public static MATH_NUMERIC Create()
        {
            return new MATH_NUMERIC
            {
                String = new byte[33],
                szCurrency = new byte[4]
            };
        }
    }

    /// <summary>
    /// Data dictionary item (DDDICT) definition (ddspec.h).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DDDICT
    {
        public uint lVarLen;
        public int idFormatNum;
        public NID szDict;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 5)]
        public string szSystemCode;

        public char cGlossaryGroup;
        public char cErrorLevel;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 41)]
        public string szAlias;

        public char cType;
        public int idEverestType;
        public NID szAS400Class;
        public int idLength;
        public ushort nDecimals;
        public ushort nDispDecimals;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 41)]
        public string szDfltValue;

        public ushort nControlType;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 7)]
        public string szAS400EditRule;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 41)]
        public string szAS400EditParm1;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 41)]
        public string szAS400EditParm2;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 7)]
        public string szAS400DispRule;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 41)]
        public string szAS400DispParm;

        public int idEditBhvr;
        public int idDispBhvr;
        public char cSecurityFlag;
        public ushort nNextNumberIndex;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 5)]
        public string szNextNumberSystem;

        public int idStyle;
        public int idBehavior;
        public NID szDsTmplName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
        public string szDispRuleBFName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
        public string szEditRuleBFName;

        public NID szSearchFormName;
    }

    /// <summary>
    /// Data dictionary text (DDTEXT) definition (ddspec.h).
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DDTEXT
    {
        public uint lVarLen;
        public int idFormatNum;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 3)]
        public string szLanguage;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 5)]
        public string szSystemCode;

        public char cTextType;
        public NID szDict;
        public char szText;
    }

    /// <summary>
    /// SELECTSTRUCT - Selection criteria for JDB_SetSelection
    /// Discovered via JDE source code (xx0901.c) and official API documentation
    ///
    /// This structure defines WHERE clause conditions for database queries.
    /// Multiple SELECTSTRUCT elements can be combined to create complex filters.
    ///
    /// Example from JDE source code:
    ///   Select[0].Item1 = Column to filter (e.g., F0101.AN8)
    ///   Select[0].Item2 = Second column for comparison (empty if comparing to value)
    ///   Select[0].lpValue = Pointer to filter value (e.g., "500")
    ///   Select[0].nCmp = JDEDB_CMP_EQ (equals operator)
    ///   Select[0].nAndOr = JDEDB_ANDOR_AND (AND with next condition)
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SELECTSTRUCT
    {
        /// <summary>
        /// Primary item to compare - the column being filtered
        /// Example: F0101.AN8 (Address Number)
        /// </summary>
        public DBREF Item1;

        /// <summary>
        /// Secondary item to compare - used for column-to-column comparisons
        /// For value comparisons (most common), set to empty DBREF with empty strings
        /// Example: When filtering AN8=500, this should be empty
        /// </summary>
        public DBREF Item2;

        /// <summary>
        /// Comparison operator (JDEDB_CMP_*)
        /// </summary>
        public int nCmp;

        /// <summary>
        /// Logical operator to combine with next selection (JDEDB_ANDOR_*)
        /// </summary>
        public int nAndOr;

        /// <summary>
        /// Pointer to the filter value (for value comparisons)
        /// Example: Pointer to Unicode string "500" for AN8=500
        /// Set to NULL for column-to-column comparisons
        /// </summary>
        public IntPtr lpValue;

        /// <summary>
        /// Number of values in lpValue array
        /// Usually 1 for single value comparisons
        /// Can be > 1 for IN operator with multiple values
        /// </summary>
        public ushort nValues;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public struct GLOBALCOLS
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = JDE_SQL_COLUMN_LENGTH)]
        public string szSQLName;

        public ushort cSpecial;
        public IntPtr lpColumn;
        public int bColHasSecurity;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct GLOBALCOLS_NATIVE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = JDE_SQL_COLUMN_LENGTH)]
        public string szSQLName;

        public ushort cSpecial;
        public IntPtr lpColumn;
        public int bColHasSecurity;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public struct TABLECACHE_HEADER
    {
        public NID szTable;
        public uint lAddedSize;
        public ushort nNumIndex;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = JDE_SQL_TABLE_LENGTH)]
        public string szSQLTableName;

        public ushort nNumCols;
        public IntPtr hNode;
        public IntPtr lpGlobalIndex;
        public IntPtr lpColumns;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
    public struct TABLECACHE_HEADER_NATIVE
    {
        public NID szTable;
        public uint lAddedSize;
        public ushort nNumIndex;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = JDE_SQL_TABLE_LENGTH)]
        public string szSQLTableName;

        public ushort nNumCols;
        public IntPtr hNode;
        public IntPtr lpGlobalIndex;
        public IntPtr lpColumns;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public struct COLUMNCACHE_HEADER
    {
        public NID szDict;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 5)]
        public string szSystemCode;

        public int idEverestType;
        public uint nLength;
        public ushort nDecimals;
        public ushort nDispDecimals;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct COLUMNCACHE_HEADER_NATIVE
    {
        public NID szDict;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 5)]
        public string szSystemCode;

        public int idEverestType;
        public uint nLength;
        public ushort nDecimals;
        public ushort nDispDecimals;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public struct GLOBALINDEXDETAIL
    {
        public NID szDict;
        public ushort cSort;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, CharSet = CharSet.Unicode)]
    public struct GLOBALINDEX
    {
        public ID idIndex;
        public ushort nPrimary;
        public ushort nUnique;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 31)]
        public string szIndexName;

        public ushort nNumCols;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        public GLOBALINDEXDETAIL[] lpGlobalIndexDetail;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct KEYINFO
    {
        public NID szTable;
        public ID idInstance;
        public NID szDict;
        public ID nLength;
        public ID idType;
        public IntPtr lpJDEValue;
    }

    /// <summary>
    /// NEWSELECTSTRUCT - Selection criteria for JDB_SetSelectionX
    /// Includes paren fields used by the newer selection API.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct NEWSELECTSTRUCT
    {
        public DBREF Item1;
        public DBREF Item2;
        public int nCmp;
        public int nAndOr;
        public IntPtr lpValue;
        public ushort nValues;
        public short nParen;
        public ushort cFuture1;
        public char cFuture2;
    }

    /// <summary>
    /// SORTSTRUCT - Sequencing criteria for JDB_SetSequencing
    /// </summary>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct SORTSTRUCT
    {
        public DBREF Item;
        public int nSort;
    }

    #endregion
}
