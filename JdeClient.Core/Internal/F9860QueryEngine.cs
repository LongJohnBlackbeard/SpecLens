using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using JdeClient.Core.Exceptions;
using JdeClient.Core;
using JdeClient.Core.Interop;
using JdeClient.Core.Models;
using static JdeClient.Core.Interop.JdeKernelApi;
using static JdeClient.Core.Interop.JdeStructures;
using static JdeClient.Core.Interop.F9860Structures;

namespace JdeClient.Core.Internal;

/// <summary>
/// Query engine for F9860 (Object Librarian Master) table
/// Uses JDB_* C API functions to retrieve JDE objects
/// </summary>
internal class F9860QueryEngine : IF9860QueryEngine
{
    private readonly JdeClientOptions _options;
    private IReadOnlyDictionary<string, int>? _columnLengths;
    private TableLayout? F9860Layout => _options.UseRowLayoutF9860 ? TableLayoutLoader.Load("F9860") : null;
    private HENV _hEnv;
    private HUSER _hUser;
    private bool _isInitialized;
    private bool _ownsEnv;
    private bool _ownsUser;
    private bool _disposed;

    public F9860QueryEngine(JdeClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Initialize JDE environment and user handles
    /// Must be called before querying
    /// </summary>
    public void Initialize()
    {
        if (_isInitialized)
            return;

        InitializeEnvironment();
        InitializeUserContext();
        _isInitialized = true;
    }

    private void InitializeEnvironment()
    {
        var initResult = InitializeEnvironmentCore(
            () =>
            {
                var env = new HENV();
                int result = JDB_GetEnv(ref env);
                return (result, env);
            },
            () => JDB_GetLocalClientEnv(),
            () =>
            {
                var env = new HENV();
                int result = JDB_InitEnv(ref env);
                return (result, env);
            },
            DebugLog);

        if (!initResult.Success)
        {
            throw new JdeConnectionException($"Failed to initialize JDE environment. Error code: {initResult.ResultCode}");
        }

        _hEnv = initResult.Env;
        _ownsEnv = initResult.OwnsEnv;
    }

    private void InitializeUserContext()
    {
        var initResult = InitializeUserContextCore(
            _hEnv,
            env =>
            {
                int result = JDB_InitUser(env, out HUSER user, "", JDEDB_COMMIT_AUTO);
                return (result, user);
            },
            DebugLog);

        if (initResult.Status == UserInitStatus.FailedResult)
        {
            FreeEnvIfOwned();
            throw new JdeConnectionException($"Failed to initialize JDE user context. Error code: {initResult.ResultCode}");
        }

        if (initResult.Status == UserInitStatus.InvalidHandle)
        {
            FreeEnvIfOwned();
            throw new JdeConnectionException("Failed to initialize JDE user context (invalid handle).");
        }

        _hUser = initResult.User;
        _ownsUser = initResult.OwnsUser;
    }

    private void FreeEnvIfOwned()
    {
        if (_ownsEnv)
        {
            JDB_FreeEnv(_hEnv);
        }
    }

    internal static EnvironmentInitResult InitializeEnvironmentCore(
        Func<(int Result, HENV Env)> getEnv,
        Func<HENV> getLocalEnv,
        Func<(int Result, HENV Env)> initEnv,
        Action<string>? log)
    {
        log?.Invoke("[DEBUG] JDB_GetEnv starting...");
        var globalResult = getEnv();
        log?.Invoke($"[DEBUG] JDB_GetEnv result: {globalResult.Result}, handle: 0x{globalResult.Env.Handle.ToInt64():X}");
        if (globalResult.Result == JDEDB_PASSED && globalResult.Env.IsValid)
        {
            return new EnvironmentInitResult(globalResult.Env, ownsEnv: false, EnvironmentSource.Global, globalResult.Result);
        }

        log?.Invoke("[DEBUG] JDB_GetLocalClientEnv starting...");
        var localEnv = getLocalEnv();
        log?.Invoke($"[DEBUG] JDB_GetLocalClientEnv handle: 0x{localEnv.Handle.ToInt64():X}");
        if (localEnv.IsValid)
        {
            return new EnvironmentInitResult(localEnv, ownsEnv: false, EnvironmentSource.Local, JDEDB_PASSED);
        }

        log?.Invoke("[DEBUG] JDB_InitEnv starting...");
        var initResult = initEnv();
        log?.Invoke($"[DEBUG] JDB_InitEnv result: {initResult.Result}, handle: 0x{initResult.Env.Handle.ToInt64():X}");
        if (initResult.Result != JDEDB_PASSED || !initResult.Env.IsValid)
        {
            return new EnvironmentInitResult(initResult.Env, ownsEnv: false, EnvironmentSource.Failed, initResult.Result);
        }

        return new EnvironmentInitResult(initResult.Env, ownsEnv: true, EnvironmentSource.Initialized, initResult.Result);
    }

    internal static UserInitResult InitializeUserContextCore(
        HENV env,
        Func<HENV, (int Result, HUSER User)> initUser,
        Action<string>? log)
    {
        log?.Invoke("[DEBUG] JDB_InitUser starting...");
        var initResult = initUser(env);
        log?.Invoke($"[DEBUG] JDB_InitUser result: {initResult.Result}, handle: 0x{initResult.User.Handle.ToInt64():X}");

        if (initResult.Result != JDEDB_PASSED)
        {
            return new UserInitResult(initResult.User, ownsUser: false, UserInitStatus.FailedResult, initResult.Result);
        }

        if (!initResult.User.IsValid)
        {
            return new UserInitResult(initResult.User, ownsUser: false, UserInitStatus.InvalidHandle, initResult.Result);
        }

        return new UserInitResult(initResult.User, ownsUser: true, UserInitStatus.Success, initResult.Result);
    }

    internal enum EnvironmentSource
    {
        Global,
        Local,
        Initialized,
        Failed
    }

    internal readonly struct EnvironmentInitResult
    {
        public EnvironmentInitResult(HENV env, bool ownsEnv, EnvironmentSource source, int resultCode)
        {
            Env = env;
            OwnsEnv = ownsEnv;
            Source = source;
            ResultCode = resultCode;
        }

        public HENV Env { get; }
        public bool OwnsEnv { get; }
        public EnvironmentSource Source { get; }
        public int ResultCode { get; }
        public bool Success => Source != EnvironmentSource.Failed;
    }

    internal enum UserInitStatus
    {
        Success,
        FailedResult,
        InvalidHandle
    }

    internal readonly struct UserInitResult
    {
        public UserInitResult(HUSER user, bool ownsUser, UserInitStatus status, int resultCode)
        {
            User = user;
            OwnsUser = ownsUser;
            Status = status;
            ResultCode = resultCode;
        }

        public HUSER User { get; }
        public bool OwnsUser { get; }
        public UserInitStatus Status { get; }
        public int ResultCode { get; }
    }

    /// <summary>
    /// JDE user handle for this engine.
    /// </summary>
    public HUSER UserHandle => _hUser;

    /// <summary>
    /// Query F9860 table for all objects of specified type
    /// </summary>
    /// <param name="objectType">Filter by object type (null for all)</param>
    /// <param name="namePattern">Optional: filter by object name (supports * wildcards)</param>
    /// <param name="descriptionPattern">Optional: filter by description (supports * wildcards)</param>
    /// <param name="maxResults">Maximum number of results to return (0 for all)</param>
    /// <returns>List of JDE objects</returns>
    public List<JdeObjectInfo> QueryObjects(
        JdeObjectType? objectType = null,
        string? namePattern = null,
        string? descriptionPattern = null,
        int maxResults = 0)
    {
        if (!_isInitialized)
            throw new InvalidOperationException("F9860QueryEngine not initialized. Call Initialize() first.");

        HREQUEST hRequest = OpenF9860Table();
        try
        {
            if (ShouldApplyFilter(objectType, namePattern, descriptionPattern))
            {
                DebugLog($"[DEBUG] Applying F9860 filter: name='{namePattern}', description='{descriptionPattern}', type='{objectType}'");
                ApplyObjectFilter(hRequest, objectType, namePattern, descriptionPattern);
            }
            SelectKeyed(hRequest);
            return FetchObjects(hRequest, objectType, maxResults);
        }
        finally
        {
            if (hRequest.Handle != IntPtr.Zero)
            {
                JDB_CloseTable(hRequest);
            }
        }
    }

    /// <summary>
    /// Retrieve a single object from F9860 by name and type.
    /// </summary>
    public JdeObjectInfo? GetObjectByName(string objectName, JdeObjectType objectType)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        var matches = QueryObjects(objectType, objectName, null, 5);
        if (matches.Count == 0)
        {
            return null;
        }

        foreach (var match in matches)
        {
            if (string.Equals(match.ObjectName, objectName, StringComparison.OrdinalIgnoreCase))
            {
                return match;
            }
        }

        return matches[0];
    }

    /// <summary>
    /// Extract object information from current F9860 record
    /// </summary>
    private JdeObjectInfo? ExtractObjectInfo(HREQUEST hRequest)
    {
        try
        {
            // Extract each column value
            DebugLog($"[DEBUG]   Getting OBNM column...");
            string objectName = NormalizeText(GetColumnValue(hRequest, Columns.OBNM, GetColumnLength(Columns.OBNM)));
            DebugLog($"[DEBUG]   ObjectName: '{objectName}'");

            DebugLog($"[DEBUG]   Getting FUNO column...");
            string objectType = NormalizeText(GetColumnValue(hRequest, Columns.FUNO, GetColumnLength(Columns.FUNO)));
            DebugLog($"[DEBUG]   ObjectType: '{objectType}'");

            DebugLog($"[DEBUG]   Getting SY column...");
            string systemCode = NormalizeText(GetColumnValue(hRequest, Columns.SY, GetColumnLength(Columns.SY)));
            DebugLog($"[DEBUG]   SystemCode: '{systemCode}'");

            DebugLog($"[DEBUG]   Getting MD column...");
            string description = NormalizeText(GetColumnValue(hRequest, Columns.MD, GetColumnLength(Columns.MD)));
            DebugLog($"[DEBUG]   Description: '{description}'");

            // Validate required fields
            if (string.IsNullOrWhiteSpace(objectName) || string.IsNullOrWhiteSpace(objectType))
            {
                DebugLog($"[DEBUG]   Validation failed - objectName or objectType is empty");
                return null;
            }

            return new JdeObjectInfo
            {
                ObjectName = objectName,
                ObjectType = objectType,
                SystemCode = systemCode,
                Description = description
            };
        }
        catch (Exception ex)
        {
            DebugLog($"[DEBUG]   Exception in ExtractObjectInfo: {ex.Message}");
            DebugLog($"[DEBUG]   Stack trace: {ex.StackTrace}");
            return null; // Skip invalid records
        }
    }


    /// <summary>
    /// Get column value from current record
    /// </summary>
    private string GetColumnValue(HREQUEST hRequest, string columnName, int columnLength)
    {
        DBREF dbRef = CreateColumnDbRef(columnName);
        LogDbRef(dbRef);

        // Call JDB_GetTableColValue - returns pointer to internal buffer
        // DBREF passed BY VALUE (confirmed from official JDE API documentation)
        IntPtr valuePtr = JDB_GetTableColValue(hRequest, dbRef);
        DebugLog($"[DEBUG]     JDB_GetTableColValue returned pointer: 0x{valuePtr.ToInt64():X}");

        if (valuePtr == IntPtr.Zero)
        {
            DebugLog($"[DEBUG]     NULL pointer - column '{columnName}' not found or no data");
            return string.Empty;
        }

        return ReadValueFromPointer(valuePtr, columnLength, DebugLog);
    }

    private static DBREF CreateColumnDbRef(string columnName)
    {
        // NOTE: szTable and szDict must be null-terminated strings (NID format)
        return new DBREF
        {
            szTable = new NID("F9860"),
            idInstance = 0,
            szDict = new NID(columnName)
        };
    }

    private void LogDbRef(DBREF dbRef)
    {
        DebugLog($"[DEBUG]     Creating DBREF: Table='{dbRef.szTable.Value}', Column='{dbRef.szDict.Value}', Instance={dbRef.idInstance}");
        DebugLog($"[DEBUG]     DBREF sizes: szTable={dbRef.szTable.Value.Length}, szDict={dbRef.szDict.Value.Length}");
    }

    internal static string ReadValueFromPointer(IntPtr valuePtr, int columnLength, Action<string>? log)
    {
        LogBufferHex(valuePtr, log);

        string fixedValue = TryReadFixedValue(valuePtr, columnLength, log);
        if (!string.IsNullOrWhiteSpace(fixedValue))
        {
            return fixedValue;
        }

        string uniValue = ReadUnicodeValue(valuePtr, log);
        if (!string.IsNullOrWhiteSpace(uniValue))
        {
            return uniValue;
        }

        return ReadAnsiValue(valuePtr, log);
    }

    private static void LogBufferHex(IntPtr valuePtr, Action<string>? log)
    {
        // Dump first 50 bytes to see what we got
        StringBuilder hexDump = new StringBuilder();
        for (int i = 0; i < 50; i++)
        {
            byte b = Marshal.ReadByte(valuePtr, i);
            if (b == 0) break; // Stop at null terminator
            hexDump.Append($"{b:X2} ");
        }
        log?.Invoke($"[DEBUG]     Buffer hex: {hexDump}");
    }

    private static string TryReadFixedValue(IntPtr valuePtr, int columnLength, Action<string>? log)
    {
        if (columnLength > 0 && columnLength < 4096)
        {
            string fixedValue = ReadJCharString(valuePtr, columnLength);
            log?.Invoke($"[DEBUG]     Fixed-length value: '{fixedValue}' (length: {fixedValue.Length})");
            return fixedValue;
        }

        return string.Empty;
    }

    private static void LogValue(Action<string>? log, string label, string value)
    {
        log?.Invoke($"[DEBUG]     {label} value: '{value}' (length: {value.Length})");
    }

    internal static string ReadUnicodeValue(IntPtr valuePtr, Action<string>? log)
    {
        // Prefer Unicode on Windows (JCHAR is wchar_t unless explicitly built otherwise).
        string uniValue = Marshal.PtrToStringUni(valuePtr) ?? string.Empty;
        LogValue(log, "Unicode", uniValue);
        return uniValue;
    }

    internal static string ReadAnsiValue(IntPtr valuePtr, Action<string>? log)
    {
        string ansiValue = Marshal.PtrToStringAnsi(valuePtr) ?? string.Empty;
        LogValue(log, "ANSI", ansiValue);
        return ansiValue;
    }

    /// <summary>
    /// Check if object type string matches the specified type filter
    /// </summary>
    internal static bool MatchesObjectType(string typeString, JdeObjectType objectType)
    {
        return objectType switch
        {
            JdeObjectType.Table => typeString.Equals(ObjectTypes.Table, StringComparison.OrdinalIgnoreCase),
            JdeObjectType.BusinessFunction => typeString.Equals(ObjectTypes.BusinessFunction, StringComparison.OrdinalIgnoreCase),
            JdeObjectType.NamedEventRule => typeString.Equals(ObjectTypes.NamedEventRule, StringComparison.OrdinalIgnoreCase),
            JdeObjectType.Report => typeString.Equals(ObjectTypes.Report, StringComparison.OrdinalIgnoreCase),
            JdeObjectType.Application => typeString.Equals(ObjectTypes.Application, StringComparison.OrdinalIgnoreCase),
            JdeObjectType.DataStructure => typeString.Equals(ObjectTypes.DataStructure, StringComparison.OrdinalIgnoreCase),
            JdeObjectType.BusinessView => typeString.Equals(ObjectTypes.BusinessView, StringComparison.OrdinalIgnoreCase),
            JdeObjectType.DataDictionary => typeString.Equals(ObjectTypes.DataDictionary, StringComparison.OrdinalIgnoreCase),
            JdeObjectType.All => true,
            _ => false
        };
    }

    internal static bool ShouldSkipObject(JdeObjectInfo objectInfo, JdeObjectType? objectType)
    {
        if (!objectType.HasValue)
        {
            return false;
        }

        return !MatchesObjectType(objectInfo.ObjectType, objectType.Value);
    }

    private void ProcessFetchedRecordSequence(HREQUEST hRequest, int times)
    {
        int flags = RECORD_CONVERT | RECORD_PROCESS | RECORD_TRIGGERS;
        HREQUEST driverRequest = ResolveDriverRequest(hRequest);
        for (int i = 1; i <= times; i++)
        {
            int result = JDB_ProcessFetchedRecord(hRequest, driverRequest, flags);
            DebugLog($"[DEBUG]   JDB_ProcessFetchedRecord #{i} result: {result}");
        }
    }

    private void DebugLog(string message)
    {
        if (_options.EnableDebug)
        {
            _options.WriteLog(message);
        }
    }

    internal static string NormalizeText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        int nullIndex = value.IndexOf('\0');
        if (nullIndex >= 0)
        {
            value = value.Substring(0, nullIndex);
        }

        return value.Trim();
    }

    private void ApplyObjectFilter(
        HREQUEST hRequest,
        JdeObjectType? objectType,
        string? namePattern,
        string? descriptionPattern)
    {
        var filters = CreateFilters(objectType, namePattern, descriptionPattern);
        if (filters.Count == 0)
        {
            return;
        }

        var select = new SELECTSTRUCT[filters.Count];
        var valuePtrs = new IntPtr[filters.Count];

        try
        {
            JDB_ClearSelection(hRequest);
            for (int i = 0; i < filters.Count; i++)
            {
                var filter = filters[i];
                valuePtrs[i] = Marshal.StringToHGlobalUni(filter.Value);
                select[i].Item1 = new DBREF("F9860", filter.Column, 0);
                select[i].Item2 = new DBREF(string.Empty, string.Empty, 0);
                select[i].lpValue = valuePtrs[i];
                select[i].nValues = 1;
                select[i].nCmp = filter.Comparison;
                select[i].nAndOr = JDEDB_ANDOR_AND;
            }

            int setResult = JDB_SetSelection(hRequest, select, (ushort)select.Length, JDEDB_SET_REPLACE);
            if (setResult != JDEDB_PASSED)
            {
                ThrowSelectionError(hRequest, setResult);
            }
        }
        finally
        {
            for (int i = 0; i < valuePtrs.Length; i++)
            {
                if (valuePtrs[i] != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(valuePtrs[i]);
                }
            }
        }
    }

    internal static List<(string Column, string Value, int Comparison)> CreateFilters(
        JdeObjectType? objectType,
        string? namePattern,
        string? descriptionPattern)
    {
        var filters = new List<(string Column, string Value, int Comparison)>();

        AddPatternFilter(filters, Columns.OBNM, namePattern);
        AddPatternFilter(filters, Columns.MD, descriptionPattern);

        if (TryGetObjectTypeCode(objectType, out string typeCode))
        {
            filters.Add((Columns.FUNO, typeCode, JDEDB_CMP_EQ));
        }

        return filters;
    }

    internal static void AddPatternFilter(
        ICollection<(string Column, string Value, int Comparison)> filters,
        string columnName,
        string? pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return;
        }

        string trimmed = pattern.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        bool hasWildcard = trimmed.Contains('*');
        string selectionValue = hasWildcard ? BuildLikePattern(trimmed) : trimmed;
        int comparison = hasWildcard ? JDEDB_CMP_LK : JDEDB_CMP_EQ;

        filters.Add((columnName, selectionValue, comparison));
    }

    internal static bool TryGetObjectTypeCode(JdeObjectType? objectType, out string typeCode)
    {
        typeCode = string.Empty;
        if (!objectType.HasValue)
        {
            return false;
        }

        return objectType.Value switch
        {
            JdeObjectType.Table => SetTypeCode(ObjectTypes.Table, out typeCode),
            JdeObjectType.BusinessFunction => SetTypeCode(ObjectTypes.BusinessFunction, out typeCode),
            JdeObjectType.NamedEventRule => SetTypeCode(ObjectTypes.NamedEventRule, out typeCode),
            JdeObjectType.Report => SetTypeCode(ObjectTypes.Report, out typeCode),
            JdeObjectType.Application => SetTypeCode(ObjectTypes.Application, out typeCode),
            JdeObjectType.DataStructure => SetTypeCode(ObjectTypes.DataStructure, out typeCode),
            JdeObjectType.BusinessView => SetTypeCode(ObjectTypes.BusinessView, out typeCode),
            JdeObjectType.DataDictionary => SetTypeCode(ObjectTypes.DataDictionary, out typeCode),
            _ => false
        };
    }

    private static bool SetTypeCode(string code, out string typeCode)
    {
        typeCode = code;
        return true;
    }

    internal static bool ShouldFilterByObjectType(JdeObjectType? objectType)
    {
        if (!objectType.HasValue)
        {
            return false;
        }

        return objectType.Value != JdeObjectType.All && objectType.Value != JdeObjectType.Unknown;
    }

    internal static bool ShouldApplyFilter(
        JdeObjectType? objectType,
        string? namePattern,
        string? descriptionPattern)
    {
        if (!string.IsNullOrWhiteSpace(namePattern) || !string.IsNullOrWhiteSpace(descriptionPattern))
        {
            return true;
        }

        return ShouldFilterByObjectType(objectType);
    }

    internal static string BuildLikePattern(string pattern)
    {
        return pattern.Replace('*', '%');
    }

    internal static Dictionary<string, int> BuildColumnLengthMap(IEnumerable<JdeColumn> columns)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in columns)
        {
            if (!string.IsNullOrWhiteSpace(column.Name) && column.Length > 0)
            {
                map[column.Name] = column.Length;
            }
            if (!string.IsNullOrWhiteSpace(column.DataDictionaryItem) && column.Length > 0)
            {
                map[column.DataDictionaryItem] = column.Length;
            }
            if (!string.IsNullOrWhiteSpace(column.SqlName) && column.Length > 0)
            {
                map[column.SqlName] = column.Length;
            }
        }

        return map;
    }

    internal static bool ShouldStopAfterMax(int recordCount, int maxResults)
    {
        return maxResults > 0 && recordCount >= maxResults;
    }

    private HREQUEST OpenF9860Table()
    {
        DebugLog("[DEBUG] Opening F9860 table...");

        string? dataSourceOverride = DataSourceResolver.ResolveTableDataSource(_hUser, "F9860");
        DebugLog($"[DEBUG] Using data source override: {dataSourceOverride ?? "<default>"}");

        NID tableNid = new NID("F9860");
        int result = JDB_OpenTable(
            _hUser,
            tableNid,
            new ID(0),
            IntPtr.Zero,
            0,
            dataSourceOverride,
            out HREQUEST hRequest);

        DebugLog($"[DEBUG] JDB_OpenTable result: {result}, handle: 0x{hRequest.Handle:X}");

        if (result != JDEDB_PASSED || !hRequest.IsValid)
        {
            throw new JdeApiException("JDB_OpenTable", $"Failed to open F9860 table (result={result})", result);
        }

        return hRequest;
    }

    private void SelectKeyed(HREQUEST hRequest)
    {
        DebugLog("[DEBUG] Calling JDB_SelectKeyed...");
        int result = JDB_SelectKeyed(hRequest, new ID(0), IntPtr.Zero, 0);
        DebugLog($"[DEBUG] JDB_SelectKeyed result: {result}");
        if (result != JDEDB_PASSED)
        {
            throw new JdeApiException("JDB_SelectKeyed", "Failed to select records from F9860", result);
        }
    }

    private List<JdeObjectInfo> FetchObjects(HREQUEST hRequest, JdeObjectType? objectType, int maxResults)
    {
        DebugLog("[DEBUG] Starting fetch loop (JDB_Fetch)...");
        var results = new List<JdeObjectInfo>();
        int recordCount = 0;
        int nullStreak = 0;
        int failureCount = 0;

        while (true)
        {
            int result = JDB_Fetch(hRequest, IntPtr.Zero, 0);
            var outcome = HandleFetchResult(
                result,
                hRequest,
                objectType,
                maxResults,
                results,
                ref recordCount,
                ref nullStreak,
                ref failureCount);

            if (outcome == FetchOutcome.Break)
            {
                break;
            }
        }

        return results;
    }

    private FetchOutcome HandleFetchResult(
        int result,
        HREQUEST hRequest,
        JdeObjectType? objectType,
        int maxResults,
        List<JdeObjectInfo> results,
        ref int recordCount,
        ref int nullStreak,
        ref int failureCount)
    {
        if (result == JDEDB_NO_MORE_DATA)
        {
            LogNoMoreData(hRequest, recordCount);
            return FetchOutcome.Break;
        }

        if (result == JDEDB_SKIPPED)
        {
            return FetchOutcome.Continue;
        }

        if (result != JDEDB_PASSED)
        {
            return HandleFetchFailure(hRequest, result, ref failureCount)
                ? FetchOutcome.Continue
                : FetchOutcome.Break;
        }

        return HandleFetchedObject(hRequest, objectType, maxResults, results, ref recordCount, ref nullStreak);
    }

    private FetchOutcome HandleFetchedObject(
        HREQUEST hRequest,
        JdeObjectType? objectType,
        int maxResults,
        List<JdeObjectInfo> results,
        ref int recordCount,
        ref int nullStreak)
    {
        var objectInfo = ExtractObjectInfo(hRequest);
        if (objectInfo == null)
        {
            nullStreak++;
            return nullStreak >= 1000 ? FetchOutcome.Break : FetchOutcome.Continue;
        }

        nullStreak = 0;
        if (recordCount < 3)
        {
            DebugLog($"[DEBUG] F9860 row {recordCount + 1}: {objectInfo.ObjectName} ({objectInfo.ObjectType})");
        }

        if (ShouldSkipObject(objectInfo, objectType))
        {
            return FetchOutcome.Continue;
        }

        results.Add(objectInfo);
        recordCount++;

        return ShouldStopAfterMax(recordCount, maxResults) ? FetchOutcome.Break : FetchOutcome.Continue;
    }

    private bool HandleFetchFailure(HREQUEST hRequest, int result, ref int failureCount)
    {
        failureCount++;
        if (JDB_GetLastDBError(hRequest, out int errorNum) == JDEDB_PASSED)
        {
            DebugLog($"[DEBUG] JDB_FetchCols failed (result={result}, error={errorNum})");
        }

        if (failureCount >= 1000)
        {
            DebugLog("[DEBUG] Too many fetch failures; stopping.");
            return false;
        }

        return true;
    }

    private void LogNoMoreData(HREQUEST hRequest, int recordCount)
    {
        if (recordCount == 0 && JDB_GetLastDBError(hRequest, out int errorNum) == JDEDB_PASSED)
        {
            DebugLog($"[DEBUG] No data from F9860 (last error={errorNum})");
        }
    }

    private enum FetchOutcome
    {
        Continue,
        Break
    }


    private static void ThrowSelectionError(HREQUEST hRequest, int result)
    {
        if (JDB_GetLastDBError(hRequest, out int errorNum) == JDEDB_PASSED)
        {
            string message = string.Format("Failed to apply object filter (error={0})", errorNum);
            throw new JdeApiException("JDB_SetSelection", message, result);
        }

        throw new JdeApiException("JDB_SetSelection", "Failed to apply object filter", result);
    }


    private int GetColumnLength(string columnName)
    {
        if (_columnLengths == null)
        {
            using var specProvider = new SpecTableMetadataService(_hUser, _options);
            var columns = specProvider.GetColumns("F9860");
            _columnLengths = BuildColumnLengthMap(columns);
        }

        return _columnLengths.TryGetValue(columnName, out int length) ? length : 0;
    }

    private static string ReadJCharString(IntPtr valuePtr, int length)
    {
        if (valuePtr == IntPtr.Zero || length <= 0)
        {
            return string.Empty;
        }

        int byteCount = length * 2;
        var bytes = new byte[byteCount];
        Marshal.Copy(valuePtr, bytes, 0, byteCount);
        return NormalizeText(Encoding.Unicode.GetString(bytes));
    }



    private static HREQUEST ResolveDriverRequest(HREQUEST hRequest)
    {
        return hRequest;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_isInitialized)
        {
            // Free user handle
            if (_ownsUser && _hUser.Handle != IntPtr.Zero)
            {
                JDB_FreeUser(_hUser);
            }

            // Free environment handle
            if (_ownsEnv && _hEnv.Handle != IntPtr.Zero)
            {
                JDB_FreeEnv(_hEnv);
            }

            _isInitialized = false;
        }

        _disposed = true;
    }
}

