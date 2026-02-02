using System.Runtime.InteropServices;

namespace JdeClient.Core.Interop;

/// <summary>
/// P/Invoke declarations for JDE Spec File APIs (jdeSpec* functions)
///
/// WARNING: These functions are NOT documented in jdeapis.chm.
/// Signatures are based on WinDbg analysis and experimentation.
/// </summary>
public static class JdeSpecApi
{
    private const string JDEKRNL = "jdekrnl.dll";

    #region Handle Types

    /// <summary>
    /// Spec repository handle - represents an open connection to the spec directory
    /// </summary>
    public struct HSPECREPOSITORY
    {
        public IntPtr Handle;
        public bool IsValid => Handle != IntPtr.Zero;
    }

    /// <summary>
    /// Spec file handle - represents an open spec file (glbltbl.ddb, dddict.ddb, etc.)
    /// </summary>
    public struct HSPECFILE
    {
        public IntPtr Handle;
        public bool IsValid => Handle != IntPtr.Zero;
    }

    #endregion

    #region Spec Repository Functions

    /// <summary>
    /// Opens a connection to the spec repository (spec directory).
    /// This is the first function to call - establishes connection to spec path.
    ///
    /// EXPERIMENTAL: Signature based on naming convention and typical patterns.
    /// May need adjustment after testing.
    /// </summary>
    /// <param name="specPath">Path to spec directory (e.g., "C:\E920_1\DV920\spec")</param>
    /// <param name="hRepository">Output: Repository handle</param>
    /// <returns>0 on success, non-zero on failure</returns>
    [DllImport(JDEKRNL, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern int jdeSpecOpenRepository(
        [MarshalAs(UnmanagedType.LPWStr)] string specPath,
        out HSPECREPOSITORY hRepository
    );

    /// <summary>
    /// Closes the spec repository connection.
    /// Call this when done with all spec operations.
    /// </summary>
    /// <param name="hRepository">Repository handle from jdeSpecOpenRepository</param>
    /// <returns>0 on success, non-zero on failure</returns>
    [DllImport(JDEKRNL, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern int jdeSpecCloseRepository(
        HSPECREPOSITORY hRepository
    );

    /// <summary>
    /// Opens a spec file using LOCAL access (disk-based).
    /// CORRECT SIGNATURE: Returns int status code, writes handle to pOutputHandle!
    /// </summary>
    /// <param name="pOutputHandle">OUT: Pointer where file handle will be written</param>
    /// <param name="pRepositoryInfo">Pointer to RepositoryInfo structure</param>
    /// <param name="openMode">Open mode - 0xE (14) for read, 0x4 (4) for other mode</param>
    /// <param name="reserved">Reserved - pass IntPtr.Zero</param>
    /// <returns>Status code (0 = success, non-zero = error)</returns>
    [DllImport(JDEKRNL, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern int jdeSpecOpenLocal(
        IntPtr pOutputHandle,
        IntPtr pRepositoryInfo,
        int openMode,
        IntPtr reserved
    );

    /// <summary>
    /// Opens a spec file using REMOTE access (server-based or network).
    /// </summary>
    /// <param name="hUser">User handle from JDB_InitUser</param>
    /// <param name="pRepositoryInfo">Pointer to RepositoryInfo structure (same as Local!)</param>
    /// <param name="openMode">Mode flag - observed: 0x13 (19)</param>
    /// <param name="objectNameOrNID">Pointer to object name string or NID structure (varies per call)</param>
    /// <returns>Spec file handle (IntPtr), or IntPtr.Zero on failure</returns>
    [DllImport(JDEKRNL, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr jdeSpecOpenRemote(
        IntPtr hUser,
        IntPtr pRepositoryInfo,
        int openMode,
        IntPtr objectNameOrNID
    );

    #endregion

    #region Spec File Functions

    /// <summary>
    /// Opens a spec file with indexing support (for keyed lookups).
    /// Use this to open glbltbl.ddb or dddict.ddb for fast table name lookups.
    ///
    /// - hContext: Context/environment handle (from repository or environment)
    /// - pRepositoryInfo: Pointer to RepositoryInfo structure with app name and username
    /// - nOpenMode: Open mode flags (3 or 7 depending on file type)
    /// - nFlags: Additional flags (typically 2)
    ///
    /// Returns: File handle on success, NULL on failure
    /// </summary>
    /// <param name="hContext">Context handle (HENV or HSPECREPOSITORY)</param>
    /// <param name="pRepositoryInfo">Pointer to RepositoryInfo structure</param>
    /// <param name="nOpenMode">Open mode (3=read, 7=read/write?)</param>
    /// <param name="nFlags">Flags (typically 2)</param>
    /// <returns>File handle (HSPECFILE), or NULL on failure</returns>
    [DllImport(JDEKRNL, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr jdeSpecOpenIndexed(
        IntPtr hContext,
        IntPtr pRepositoryInfo,
        int nOpenMode,
        int nFlags
    );

    /// <summary>
    /// Opens a spec file without indexing.
    /// Alternative to jdeSpecOpenIndexed for sequential access.
    /// </summary>
    /// <param name="hRepository">Repository handle</param>
    /// <param name="fileName">Spec file name</param>
    /// <param name="hFile">Output: File handle</param>
    /// <returns>0 on success, non-zero on failure</returns>
    [DllImport(JDEKRNL, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern int jdeSpecOpenFile(
        HSPECREPOSITORY hRepository,
        [MarshalAs(UnmanagedType.LPWStr)] string fileName,
        out HSPECFILE hFile
    );

    /// <summary>
    /// Closes a spec file.
    /// </summary>
    /// <param name="hFile">File handle from jdeSpecOpenIndexed or jdeSpecOpenFile</param>
    /// <returns>0 on success, non-zero on failure</returns>
    [DllImport(JDEKRNL, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern int jdeSpecClose(
        HSPECFILE hFile
    );

    #endregion

    #region Spec Data Access Functions

    /// <summary>
    /// Selects a record by key (e.g., table name "F0101").
    /// Positions the cursor to the matching record.
    ///
    /// This function has TWO calling modes:
    ///
    /// MODE 1 (searchMode=1): Plain Unicode string search
    ///   - pSearchKey points to Unicode string (e.g., "F0101")
    ///   - keyIndexOrSize = 22 (0x16) - typical value for string mode
    ///
    /// MODE 2 (searchMode=2): NID structure search
    ///   - pSearchKey points to NID structure (DWORD length + Unicode string)
    ///   - keyIndexOrSize = 84 (0x54) - typical value for NID mode
    ///
    /// Return codes:
    /// - 0x1a (26) = Success / Found
    /// - 0x1e (30) = Not found / Error
    /// </summary>
    /// <param name="hFile">File handle from jdeSpecOpenIndexed</param>
    /// <param name="pSearchKey">Pointer to search key (string or NID structure depending on mode)</param>
    /// <param name="keyIndexOrSize">Key index or size (22 for strings, 84 for NID)</param>
    /// <param name="searchMode">Search mode (1=plain string, 2=NID structure)</param>
    /// <returns>0x1a on success, 0x1e on not found</returns>
    [DllImport(JDEKRNL, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern int jdeSpecSelectKeyed(
        IntPtr hFile,
        IntPtr pSearchKey,
        int keyIndexOrSize,
        int searchMode
    );

    /// <summary>
    /// Fetches the current record data.
    /// Call after jdeSpecSelectKeyed to retrieve the selected record.
    ///
    /// This function has THREE calling patterns (phases):
    ///
    /// PHASE A - Initial Fetch (allocate buffer):
    ///   - pOutputBuffer: pointer to buffer pointer (NULL before call)
    ///   - bufferSizeOrFlags: 8 (allocate/initialize)
    ///   - pCallbackOrTimestamp: callback function pointer (e.g., 0x00007fffb7d2123d)
    ///
    /// PHASE B - Validation Fetch:
    ///   - pOutputBuffer: same buffer location as Phase A
    ///   - bufferSizeOrFlags: jdekrnl.dll base address (e.g., 0x00007fff26bc0000)
    ///   - pCallbackOrTimestamp: 96 (0x60) or other validation value
    ///
    /// PHASE C - Loop Fetch (for columns):
    ///   - pOutputBuffer: buffer from previous phases
    ///   - bufferSizeOrFlags: -4 (0x7ffffffffffffffc in two's complement)
    ///   - pCallbackOrTimestamp: timestamp or iteration counter
    ///
    /// Return codes:
    /// - 0x1a (26) = Success / More data
    /// - 0x1e (30) = No more data / End of records
    /// </summary>
    /// <param name="hFile">File handle from jdeSpecOpenIndexed</param>
    /// <param name="pOutputBuffer">Pointer to output buffer (or pointer to pointer)</param>
    /// <param name="bufferSizeOrFlags">Buffer size, flags, or mode (8, -4, or module base)</param>
    /// <param name="pCallbackOrTimestamp">Callback function pointer or timestamp/counter</param>
    /// <returns>0x1a for success/more data, 0x1e for no more data</returns>
    [DllImport(JDEKRNL, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern int jdeSpecFetch(
        IntPtr hFile,
        IntPtr pOutputBuffer,
        IntPtr bufferSizeOrFlags,     // Changed from int to IntPtr (can be 0x8, -4, or jdekrnl_base)
        IntPtr pCallbackOrTimestamp
    );

    /// <summary>
    /// Frees data allocated by jdeSpecFetch or other spec functions.
    /// Call this to release memory returned by fetch operations.
    /// </summary>
    /// <param name="pData">Data pointer from jdeSpecFetch</param>
    [DllImport(JDEKRNL, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.Cdecl)]
    public static extern void jdeSpecFreeData(
        IntPtr pData
    );

    #endregion

    #region Data Dictionary Functions

    /// <summary>
    /// Opens the data dictionary (extended version with additional parameters).
    /// Used to access column definitions and field metadata.
    /// </summary>
    [DllImport(JDEKRNL, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr jdeOpenDictionaryX(
        JdeStructures.HUSER hUser
    );

    /// <summary>
    /// Closes the data dictionary.
    /// </summary>
    /// <param name="lpDictionary">Dictionary handle from jdeOpenDictionaryX</param>
    [DllImport(JDEKRNL, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    public static extern void jdeCloseDictionary(
        IntPtr lpDictionary
    );

    /// <summary>
    /// Allocates and fetches a data dictionary item by name.
    /// Used to get column metadata for a specific field.
    /// </summary>
    [DllImport(JDEKRNL, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr jdeAllocFetchDDItemFromDDItemName(
        IntPtr lpDictionary,
        [MarshalAs(UnmanagedType.LPWStr)] string dataItem
    );

    /// <summary>
    /// Allocates and fetches data dictionary text for an item.
    /// </summary>
    [DllImport(JDEKRNL, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr jdeAllocFetchTextFromDDItemNameOvr(
        IntPtr lpDictionary,
        [MarshalAs(UnmanagedType.LPWStr)] string dataItem,
        int textType,
        [MarshalAs(UnmanagedType.LPWStr)] string? language,
        [MarshalAs(UnmanagedType.LPWStr)] string? systemCode
    );

    /// <summary>
    /// Allocates and fetches data dictionary text (DDTEXT) for an item.
    /// </summary>
    [DllImport(JDEKRNL, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    public static extern IntPtr jdeAllocFetchDDTextFromDDItemNameOvr(
        IntPtr lpDictionary,
        [MarshalAs(UnmanagedType.LPWStr)] string dataItem,
        int textType,
        [MarshalAs(UnmanagedType.LPWStr)] string? language,
        [MarshalAs(UnmanagedType.LPWStr)] string? systemCode
    );

    /// <summary>
    /// Frees data dictionary data allocated by jdeAllocFetchDDItemFromDDItemName.
    /// </summary>
    /// <param name="pDDData">DD item pointer</param>
    [DllImport(JDEKRNL, CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    public static extern void jdeDDDictFree(
        IntPtr pDDData
    );

    /// <summary>
    /// Frees text allocated by jdeAllocFetchTextFromDDItemNameOvr or jdeAllocFetchDDTextFromDDItemNameOvr.
    /// </summary>
    /// <param name="textPtr">Pointer to text string</param>
    [DllImport(JDEKRNL, CallingConvention = CallingConvention.StdCall)]
    public static extern void jdeTextFree(
        IntPtr textPtr
    );

    #endregion
    
}
