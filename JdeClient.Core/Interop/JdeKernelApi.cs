using System;
using System.Runtime.InteropServices;
using System.Text;
using static JdeClient.Core.Interop.JdeStructures;

namespace JdeClient.Core.Interop;

/// <summary>
/// P/Invoke declarations for jdekrnl.dll - JDE EnterpriseOne Kernel API
/// Provides direct access to JDE's native C API functions (JDB_* functions)
/// </summary>
public static class JdeKernelApi
{
    private const string DllName = "jdekrnl.dll";
    private const string JdelDll = "jdel.dll";
    public static bool UseCdeclForProcessFetchedRecord { get; set; }

    #region Environment and Session Management
    /// <summary>
    /// Initialize JDE environment - creates HENV handle
    /// Must be called before any other JDE API functions
    /// </summary>
    /// <param name="hEnv">Output: Environment handle</param>
    /// <returns>JDEDB_PASSED (0) on success, JDEDB_FAILED (-1) on failure</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "JDB_InitEnv")]
    private static extern int JDB_InitEnvStd(ref HENV hEnv);
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "JDB_InitEnv")]
    private static extern int JDB_InitEnvCdecl(ref HENV hEnv);

    public static int JDB_InitEnv(ref HENV hEnv)
    {
        int result = JDB_InitEnvStd(ref hEnv);
        if ((result == JDEDB_PASSED || result == 1) && hEnv.IsValid)
            return result;
        // Fallback for platforms where JDB_InitEnv uses cdecl
        HENV temp = new HENV();
        int result2 = JDB_InitEnvCdecl(ref temp);
        if ((result2 == JDEDB_PASSED || result2 == 1) && temp.IsValid)
        {
            hEnv = temp;
            return result2;
        }
        return result;
    }
    /// <summary>
    /// Free JDE environment - releases HENV handle
    /// Must be called when done with JDE operations
    /// </summary>
    /// <param name="hEnv">Environment handle to free</param>
    /// <returns>JDEDB_PASSED (0) on success</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int JDB_FreeEnv(HENV hEnv);
    /// <summary>
    /// Initialize user context - creates HUSER handle
    /// Used by applications
    /// </summary>
    /// <param name="hEnv">Environment handle from JDB_InitEnv</param>
    /// <param name="hUser">Output: User handle</param>
    /// <param name="szApp">Application name (can be empty)</param>
    /// <param name="nCommitMode">Commit mode: JDEDB_COMMIT_AUTO or JDEDB_COMMIT_MANUAL</param>
    /// <returns>JDEDB_PASSED (0) on success</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern int JDB_InitUser(
        HENV hEnv,
        out HUSER hUser,
        [MarshalAs(UnmanagedType.LPStr)] string szApp,
        int nCommitMode);
    /// <summary>
    /// Initialize user/behavior context - creates HUSER handle
    /// Can be used to set up user session for business function calls
    /// </summary>
    /// <param name="lpBhvr">Pointer to behavior structure (can be HENV.Handle or IntPtr.Zero)</param>
    /// <param name="hUser">Output: User handle</param>
    /// <param name="szApp">Application name (can be empty)</param>
    /// <param name="nCommitMode">Commit mode: JDEDB_COMMIT_AUTO or JDEDB_COMMIT_MANUAL</param>
    /// <returns>JDEDB_PASSED (0) on success</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern int JDB_InitBhvr(
        IntPtr lpBhvr,
        out HUSER hUser,
        [MarshalAs(UnmanagedType.LPStr)] string szApp,
        int nCommitMode);
    /// <summary>
    /// Free user handle - releases HUSER handle
    /// </summary>
    /// <param name="hUser">User handle to free</param>
    /// <returns>JDEDB_PASSED (0) on success</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int JDB_FreeUser(HUSER hUser);
    /// <summary>
    /// Get global environment handle from activConsole.exe
    ///
    /// Workflow
    ///   1. activConsole.exe calls JDB_InitEnv during startup
    ///   2. The client calls JDB_GetEnv to retrieve that global HENV
    ///   3. The client passes HENV to JDB_InitUser
    ///
    /// This retrieves the global HENV that activConsole already created.
    /// Do NOT call JDB_InitEnv - use this to get the existing global handle!
    /// </summary>
    /// <param name="hEnv">Output: Global environment handle (passed by reference)</param>
    /// <returns>JDEDB_PASSED (0) on success, JDEDB_FAILED (-1) if no global env exists</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, EntryPoint = "JDB_GetEnv")]
    private static extern int JDB_GetEnvStd(ref HENV hEnv);
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi, EntryPoint = "JDB_GetEnv")]
    private static extern int JDB_GetEnvCdecl(ref HENV hEnv);

    public static int JDB_GetEnv(ref HENV hEnv)
    {
        int result = JDB_GetEnvStd(ref hEnv);
        if ((result == JDEDB_PASSED || result == 1) && hEnv.IsValid)
            return result;
        // Fallback for platforms where JDB_GetEnv uses cdecl
        HENV temp = new HENV();
        int result2 = JDB_GetEnvCdecl(ref temp);
        if ((result2 == JDEDB_PASSED || result2 == 1) && temp.IsValid)
        {
            hEnv = temp;
            return result2;
        }
        return result;
    }
    /// <summary>
    /// Get local client environment handle (fat client).
    /// </summary>
    /// <returns>Environment handle, or invalid handle if not available.</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern HENV JDB_GetLocalClientEnv();
    /// <summary>
    /// Get global user handle from activConsole.exe
    /// Calls this with a NULL parameter during startup.
    ///
    /// JDB_GetUser(rcx=0000000000000000)
    /// This retrieves the global HUSER if it exists.
    /// </summary>
    /// <param name="hUser">Output: Global user handle</param>
    /// <returns>JDEDB_PASSED (0) on success, JDEDB_FAILED (-1) if no global user exists</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "JDB_GetUser")]
    private static extern int JDB_GetUserStd(out HUSER hUser);
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "JDB_GetUser")]
    private static extern int JDB_GetUserCdecl(out HUSER hUser);

    public static int JDB_GetUser(out HUSER hUser)
    {
        int result = JDB_GetUserStd(out hUser);
        if ((result == JDEDB_PASSED || result == 1) && hUser.IsValid)
            return result;
        HUSER temp;
        int result2 = JDB_GetUserCdecl(out temp);
        if ((result2 == JDEDB_PASSED || result2 == 1) && temp.IsValid)
        {
            hUser = temp;
            return result2;
        }
        return result;
    }
    #endregion
    #region Table Operations
    /// <summary>
    /// Open a JDE table for data access - creates HREQUEST handle
    /// CORRECT SIGNATURE (Session 14+): From official JDE API documentation
    /// Signature: JDEDB_RESULT JDB_OpenTable(HUSER hUser, NID szTable, ID idIndex,
    ///                                        NID *lpColSelect, unsigned short nNumCols,
    ///                                        char * szOverrideDS, HREQUEST * hRequest)
    /// </summary>
    /// <param name="hUser">User handle</param>
    /// <param name="szTable">Table name as NID structure (passed by value)</param>
    /// <param name="idIndex">Index ID (use 0 for primary)</param>
    /// <param name="lpColSelect">Pointer to array of column NIDs (IntPtr.Zero for all columns)</param>
    /// <param name="nNumCols">Number of columns in array (0 for all)</param>
    /// <param name="szOverrideDS">Data source override (null for default)</param>
    /// <param name="hRequest">Output: Request handle</param>
    /// <returns>JDEDB_PASSED (0) on success, JDEDB_FAILED (-1) on failure</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern int JDB_OpenTable(
        HUSER hUser,
        NID szTable,                                             // NID passed by value
        ID idIndex,
        IntPtr lpColSelect,
        ushort nNumCols,
        [MarshalAs(UnmanagedType.LPWStr)] string? szOverrideDS,
        out HREQUEST hRequest);
    /// <summary>
    /// Open a JDE business view for data access - creates HREQUEST handle.
    /// Signature: JDEDB_RESULT JDB_OpenView(HUSER hUser, NID szView, JCHAR * szDS, HREQUEST * hRequest)
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern int JDB_OpenView(
        HUSER hUser,
        NID szView,
        [MarshalAs(UnmanagedType.LPWStr)] string? szOverrideDS,
        out HREQUEST hRequest);
    /// <summary>
    /// Close a table - releases HREQUEST handle
    /// Must be called for every JDB_OpenTable
    /// </summary>
    /// <param name="hRequest">Request handle to close</param>
    /// <returns>JDEDB_PASSED (0) on success</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int JDB_CloseTable(HREQUEST hRequest);
    /// <summary>
    /// Close a business view - releases HREQUEST handle.
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int JDB_CloseView(HREQUEST hRequest);
    #endregion
    #region Math Numeric Helpers
    /// <summary>
    /// Returns the raw digit string for a math numeric value (no formatting).
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi)]
    public static extern IntPtr jdeZMathGetRawString(
        IntPtr mathNumeric
    );
    /// <summary>
    /// Returns the decimal position for a math numeric value.
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern short jdeMathGetDecimalPosition(
        IntPtr mathNumeric
    );
    /// <summary>
    /// Returns the sign for a math numeric value ('-' or ' ').
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern byte jdeMathGetSign(
        IntPtr mathNumeric
    );
    #endregion
    #region Query Execution
    /// <summary>
    /// Select all records from table (no WHERE clause)
    /// </summary>
    /// <param name="hRequest">Request handle</param>
    /// <returns>JDEDB_PASSED (0) on success</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int JDB_SelectAll(HREQUEST hRequest);
    /// <summary>
    /// Execute keyed select with optional key values
    /// CORRECTED SIGNATURE (Session 14+): Based on Tools Release 8.98 API Reference (still valid for 9.2)
    /// Signature: JDEDB_RESULT JDB_SelectKeyed(HREQUEST hRequest, ID idIndex, void * lpKey, short nNumKeys)
    /// </summary>
    /// <param name="hRequest">Request handle</param>
    /// <param name="idIndex">Index ID to use (0 = use index from OpenTable)</param>
    /// <param name="lpKey">Pointer to key structure or NULL</param>
    /// <param name="nNumKeys">Number of keys to use (0 = select all with WHERE clause)</param>
    /// <returns>JDEDB_PASSED (0) on success</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int JDB_SelectKeyed(
        HREQUEST hRequest,
        ID idIndex,
        IntPtr lpKey,
        short nNumKeys);
    /// <summary>
    /// Get row count for the current selection (WHERE clause) or keyed selection.
    /// Signature: unsigned int JDB_SelectKeyedGetCount(HREQUEST hRequest, ID idIndex, void * lpKey, short nNumKeys)
    /// </summary>
    /// <param name="hRequest">Request handle</param>
    /// <param name="idIndex">Index ID to use (0 = use index from OpenTable)</param>
    /// <param name="lpKey">Pointer to key structure or NULL</param>
    /// <param name="nNumKeys">Number of keys to use (0 = select count with WHERE clause)</param>
    /// <returns>Row count</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern uint JDB_SelectKeyedGetCount(
        HREQUEST hRequest,
        ID idIndex,
        IntPtr lpKey,
        short nNumKeys);
    /// <summary>
    /// Signature: JDEDB_RESULT JDB_SetSequencing(HREQUEST hRequest, LPSORT lpSort, ushort nNum, JDEDB_SET nSet)
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern int JDB_SetSequencing(
        HREQUEST hRequest,
        [In] SORTSTRUCT[] lpSort,
        ushort nNum,
        int nSet);
    /// <summary>
    /// Fetch a single record using key values
    /// SESSION 18: Discovered from JDE source code (b0500132.c:168)
    ///
    /// This is the CORRECT way to fetch data from JDE tables!
    /// Pattern from JDE source:
    ///   JDB_FetchKeyed(hRequest, (ID)0, &keyStruct, numKeys, (void*)NULL, 0)
    ///
    /// Unlike JDB_SelectKeyed + JDB_Fetch, this fetches immediately with a key.
    /// Use this for single-record lookups (like fetching by primary key).
    ///
    /// Workflow:
    ///   1. JDB_OpenTable/OpenView → HREQUEST
    ///   2. JDB_FetchKeyed(hRequest, indexID, &key, numKeys, NULL, 0)
    ///   3. JDB_GetTableColValue(hRequest, dbref) to read columns
    ///   4. JDB_CloseTable/CloseView
    /// </summary>
    /// <param name="hRequest">Request handle from JDB_OpenTable/OpenView</param>
    /// <param name="idIndex">Index ID to use (0 = primary index from OpenTable)</param>
    /// <param name="lpKey">Pointer to key structure (e.g., KEY1_F0101)</param>
    /// <param name="nNumKeys">Number of key fields in lpKey structure</param>
    /// <param name="lpOutputBuffer">Output buffer for record data (pass NULL if using GetTableColValue)</param>
    /// <param name="nFlags">Flags (usually 0)</param>
    /// <returns>JDEDB_PASSED (0) if found, JDEDB_NO_MORE_DATA if not found</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int JDB_FetchKeyed(
        HREQUEST hRequest,
        ID idIndex,
        IntPtr lpKey,
        short nNumKeys,
        IntPtr lpOutputBuffer,
        int nFlags);
    /// <summary>
    /// Add an entry to a KEYINFO array for building key buffers.
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern int jdeK2AddtoKeyStruct(
        [In, Out] KEYINFO[] lpKeyInfo,
        short pos,
        DBREF dbRef,
        IntPtr lpJDEValue,
        ID idType,
        int nLength);
    /// <summary>
    /// Build a key buffer for JDB_FetchKeyed from KEYINFO array.
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr JDB_BuildKeyBuffer(
        [In] KEYINFO[] lpKeyInfo,
        ushort nKeys);
    /// <summary>
    /// Free a key buffer allocated by JDB_BuildKeyBuffer.
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int JDB_FreeKeyBuffer(
        IntPtr lpKey);
    /// <summary>
    /// Set selection criteria (WHERE clause conditions) for database query
    /// CORRECTED SIGNATURE from JDE source code (xx0901.c) and official API docs
    ///
    /// This function accepts an ARRAY of SELECTSTRUCT to define complex WHERE clauses.
    /// Each SELECTSTRUCT defines one condition (e.g., AN8=500, ALPH LIKE "Enter%")
    ///
    /// Example from JDE source code:
    ///   SELECTSTRUCT Select[2];
    ///   Select[0].Item1 = F0012.ITEM, lpValue = "SP%", nCmp = JDEDB_CMP_LK
    ///   Select[1].Item1 = F0012.CO, lpValue = "00000", nCmp = JDEDB_CMP_EQ
    ///   JDB_SetSelection(hRequest, Select, 2, JDEDB_SET_REPLACE);
    /// </summary>
    /// <param name="hRequest">Request handle from JDB_OpenTable</param>
    /// <param name="lpSelect">Pointer to array of SELECTSTRUCT elements</param>
    /// <param name="nNum">Number of SELECTSTRUCT elements in array</param>
    /// <param name="nSet">JDEDB_SET_REPLACE (0) or JDEDB_SET_APPEND (1)</param>
    /// <returns>JDEDB_PASSED (0) on success, JDEDB_FAILED (-1) on failure</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern int JDB_SetSelection(
        HREQUEST hRequest,
        [In] SELECTSTRUCT[] lpSelect,
        ushort nNum,
        int nSet);
    /// <summary>
    /// New selection API with extended selection structure.
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern int JDB_SetSelectionX(
        HREQUEST hRequest,
        [In] NEWSELECTSTRUCT[] lpSelect,
        ushort nNum,
        int nSet);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern int JDBRS_GetTableSpecsByName(
        HUSER hUser,
        string tableName,
        out IntPtr lpTableCache,
        char bUseCache,
        IntPtr reserved);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int JDBRS_FreeTableSpecs(
        IntPtr lpTableCache);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern int JDBRS_GetBOBSpecs(
        HUSER hUser,
        NID szView,
        out IntPtr lpBob,
        char bUseCache,
        IntPtr reserved);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int JDBRS_FreeBOBSpecs(
        IntPtr lpBob);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern int JDBRS_GetColumnSpecs(
        HUSER hUser,
        NID dictItem,
        out IntPtr lpColumnCache,
        char bUseCache,
        IntPtr reserved);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int JDBRS_FreeColumnSpecs(
        IntPtr lpColumnCache);
    /// <summary>
    /// Get last DB error for a request handle.
    /// </summary>
    /// <param name="hRequest">Request handle</param>
    /// <param name="errorNumber">Output: error number</param>
    /// <returns>JDEDB_PASSED (0) on success</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int JDB_GetLastDBError(HREQUEST hRequest, out int errorNumber);
    /// <summary>
    /// Get the data source name for an open request handle.
    /// </summary>
    /// <param name="hRequest">Request handle</param>
    /// <param name="szDS">Output buffer for data source name</param>
    /// <returns>JDEDB_PASSED (1) on success</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern int JDB_GetDSName(
        HREQUEST hRequest,
        [Out] StringBuilder szDS);
    /// <summary>
    /// Clear all selection criteria
    /// </summary>
    /// <param name="hRequest">Request handle</param>
    /// <returns>JDEDB_PASSED (0) on success</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int JDB_ClearSelection(HREQUEST hRequest);
    /// <summary>
    /// Get the data source for an object based on OCM.
    /// </summary>
    /// <param name="hUser">User handle</param>
    /// <param name="szObject">Object NID</param>
    /// <param name="szObj">Object name (e.g., table name)</param>
    /// <param name="cType">Object type (JDEDB_OMAP_*)</param>
    /// <param name="szDs">Output buffer for data source name</param>
    /// <returns>JDEDB_PASSED (0) on success</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern int JDB_GetObjectDataSource(
        HUSER hUser,
        NID szObject,
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder szObj,
        char cType,
        [Out] StringBuilder szDs);
    /// <summary>
    /// Parse a numeric string into a MATH_NUMERIC.
    /// </summary>
    /// <param name="t">Output math numeric</param>
    /// <param name="s">Input numeric string</param>
    /// <returns>0 on success</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern int ParseNumericString(ref MATH_NUMERIC t, [MarshalAs(UnmanagedType.LPWStr)] string s);
    #endregion
    #region Data Retrieval
    /// <summary>
    /// Fetch next row from result set
    /// </summary>
    /// <param name="hRequest">Request handle</param>
    /// <param name="lpValue">Reserved for future use, pass IntPtr.Zero</param>
    /// <param name="nLock">Lock mode: 0=no lock, 1=lock</param>
    /// <returns>JDEDB_PASSED (0) on success, JDEDB_NO_MORE_DATA when no more rows</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int JDB_Fetch(HREQUEST hRequest, IntPtr lpValue, int nLock);
    /// <summary>
    /// Fetch next row and return only specified columns.
    /// </summary>
    /// <param name="hRequest">Request handle</param>
    /// <param name="lpValue">Pointer to results structure or NULL</param>
    /// <param name="nLock">Lock mode</param>
    /// <param name="pId">Pointer to list of column NIDs</param>
    /// <param name="nNum">Number of columns in pId</param>
    /// <returns>JDEDB_PASSED (1) on success</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int JDB_FetchCols(
        HREQUEST hRequest,
        IntPtr lpValue,
        int nLock,
        [In] NID[] pId,
        short nNum);
    /// <summary>
    /// Process fetched record to prepare column values for extraction
    /// SESSION 18+: CRITICAL - Must call AFTER JDB_Fetch, BEFORE JDB_GetTableColValue!
    ///
    /// From WinDbg logs (f0101_query.log):
    ///   Legacy pattern: Fetch -> ProcessFetchedRecord (x3) -> GetTableColValue
    ///
    /// This function processes the raw fetched row data and makes it accessible
    /// to JDB_GetTableColValue calls.
    /// </summary>
    /// <param name="hRequest">Request handle</param>
    /// <returns>JDEDB_PASSED (0) on success, JDEDB_FAILED (-1) on failure</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, EntryPoint = "JDB_ProcessFetchedRecord")]
    private static extern int JDB_ProcessFetchedRecordStd(
        HREQUEST hUserRequest,
        HREQUEST hDriverRequest,
        int flags);
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, EntryPoint = "JDB_ProcessFetchedRecord")]
    private static extern int JDB_ProcessFetchedRecordCdecl(
        HREQUEST hUserRequest,
        HREQUEST hDriverRequest,
        int flags);

    public static int JDB_ProcessFetchedRecord(
        HREQUEST hUserRequest,
        HREQUEST hDriverRequest,
        int flags)
    {
        if (UseCdeclForProcessFetchedRecord)
        {
            return JDB_ProcessFetchedRecordCdecl(hUserRequest, hDriverRequest, flags);
        }
        return JDB_ProcessFetchedRecordStd(hUserRequest, hDriverRequest, flags);
    }
    /// <summary>
    /// Get column value from current row
    /// Returns pointer to column value in internal buffer
    ///
    /// OFFICIAL JDE API DOC: void * JDB_GetTableColValue(HREQUEST hRequest, DBREF dbItem);
    /// DBREF is passed BY VALUE (confirmed from official documentation)
    ///
    /// CharSet = Unicode because DBREF contains Unicode strings
    /// </summary>
    /// <param name="hRequest">Request handle</param>
    /// <param name="dbRef">Database reference (table, instance, column) - passed BY VALUE</param>
    /// <returns>Pointer to column value, or IntPtr.Zero if not found</returns>
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern IntPtr JDB_GetTableColValue(
        HREQUEST hRequest,
        DBREF dbRef);
    #endregion
    #region Spec Helpers
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern int JDEGetOSType();
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
    public static extern uint JDEGetProcessCodePage(string? processName);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern void jdeUnpackSpec(
        IntPtr pointer_to_pack_spec,
        JdeSpecType spec_type,
        uint fromCodePage,
        int fromOsType,
        uint toCodePage,
        int toOsType,
        JdeByteOrder from_byte_order,
        out IntPtr pointer_pointer_to_unpack_spec,
        out JdeUnpackSpecStatus pointer_to_status);
    [DllImport(DllName, CallingConvention = CallingConvention.StdCall)]
    public static extern void jdeB733UnpackSpec(
        IntPtr pointer_to_pack_spec,
        JdeSpecType spec_type,
        uint fromCodePage,
        int fromOsType,
        uint toCodePage,
        int toOsType,
        JdeByteOrder from_byte_order,
        out IntPtr pointer_pointer_to_unpack_spec,
        out JdeB733UnpackSpecStatus pointer_to_status);
    [DllImport(JdelDll, CallingConvention = CallingConvention.StdCall)]
    public static extern int jdeBufferUncompress(
        ref IntPtr pDestination,
        ref UIntPtr uncompLength,
        IntPtr pSource,
        UIntPtr sourceLength);
    [DllImport(JdelDll, CallingConvention = CallingConvention.StdCall, EntryPoint = "jdeFreeInternal")]
    public static extern void jdeFreeInternal(IntPtr pointer);
    #endregion
}
