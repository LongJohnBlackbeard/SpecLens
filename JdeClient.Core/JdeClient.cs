using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using JdeClient.Core.Internal;
using JdeClient.Core.Exceptions;
using JdeClient.Core.Interop;
using JdeClient.Core.Models;

namespace JdeClient.Core;

/// <summary>
/// Main JDE Client API - provides access to JDE EnterpriseOne metadata
/// This client queries F9860 table using JDB_* C APIs
/// REQUIRES JDE Fat Client (activConsole.exe) to be running
/// </summary>
/// <example>
/// using var client = new JdeClient();
/// await client.ConnectAsync();
///
/// // Get all tables
/// var tables = await client.GetObjectsAsync(JdeObjectType.Table);
///
/// // Get table metadata
/// var tableInfo = await client.GetTableInfoAsync("F0101");
///
/// await client.DisconnectAsync();
/// </example>
public partial class JdeClient : IDisposable
{
    private readonly IJdeSession _session;
    private readonly JdeClientOptions _options;
    private readonly IJdeTableQueryEngineFactory _tableQueryEngineFactory;
    private readonly IEventRulesQueryEngineFactory _eventRulesQueryEngineFactory;
    private readonly IDataSourceResolver _dataSourceResolver;
    private bool _disposed;

    /// <summary>
    /// Whether the client is connected to JDE
    /// </summary>
    public bool IsConnected => _session.IsConnected;

    public JdeClient(JdeClientOptions? options = null)
    {
        _options = options ?? JdeClientOptions.FromLegacyDebug();
        _session = new JdeSession(_options);
        _tableQueryEngineFactory = new JdeTableQueryEngineFactory();
        _eventRulesQueryEngineFactory = new EventRulesQueryEngineFactory();
        _dataSourceResolver = new JdeDataSourceResolver();
    }

    /// <summary>
    /// Internal constructor for unit testing and dependency injection.
    /// </summary>
    internal JdeClient(
        IJdeSession session,
        JdeClientOptions? options = null,
        IJdeTableQueryEngineFactory? tableQueryEngineFactory = null,
        IEventRulesQueryEngineFactory? eventRulesQueryEngineFactory = null,
        IDataSourceResolver? dataSourceResolver = null)
    {
        _options = options ?? JdeClientOptions.FromLegacyDebug();
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _tableQueryEngineFactory = tableQueryEngineFactory ?? new JdeTableQueryEngineFactory();
        _eventRulesQueryEngineFactory = eventRulesQueryEngineFactory ?? new EventRulesQueryEngineFactory();
        _dataSourceResolver = dataSourceResolver ?? new JdeDataSourceResolver();
    }

    #region Connection Management

    /// <summary>
    /// Connect to JDE using JDB_* C APIs
    /// REQUIRES JDE Fat Client (activConsole.exe) to be running
    /// </summary>
    /// <param name="specPath">Reserved for future use (ignored)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="JdeConnectionException">Thrown if connection fails</exception>
    public async Task ConnectAsync(string? specPath = null, CancellationToken cancellationToken = default)
    {
        await _session.ConnectAsync(specPath, cancellationToken);
    }

    /// <summary>
    /// Disconnect from JDE
    /// </summary>
    public async Task DisconnectAsync()
    {
        await _session.DisconnectAsync();
    }

    #endregion

    #region Object Catalog

    /// <summary>
    /// Get list of JDE objects from Object Librarian (F9860)
    /// </summary>
    /// <param name="objectType">Optional: Filter by object type</param>
    /// <param name="searchPattern">Optional: Filter by object name (supports * wildcards)</param>
    /// <param name="descriptionPattern">Optional: Filter by description (supports * wildcards)</param>
    /// <param name="maxResults">Optional: Maximum number of results to return</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of JDE objects</returns>
    public async Task<List<JdeObjectInfo>> GetObjectsAsync(
        JdeObjectType? objectType = null,
        string? searchPattern = null,
        string? descriptionPattern = null,
        int? maxResults = null,
        CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                // Use F9860 query engine to get object catalog
                var result = _session.QueryEngine.QueryObjects(objectType, searchPattern, descriptionPattern, maxResults ?? 0);
                return result;
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("GetObjectsAsync", "Failed to retrieve objects from F9860 table", ex);
            }
        }, cancellationToken);
    }

    #endregion

    #region Project Metadata

    /// <summary>
    /// Retrieve OMW projects from F98220, optionally filtered by status and/or user.
    /// </summary>
    /// <param name="status">Optional project status (e.g., "28").</param>
    /// <param name="dataSourceOverride">Optional data source override for F98220.</param>
    /// <param name="user">Optional OMW user filter (F98221).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<List<JdeProjectInfo>> GetProjectsAsync(
        string? status = null,
        string? dataSourceOverride = null,
        string? user = null,
        CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                using var queryEngine = _tableQueryEngineFactory.Create(_options);
                string? normalizedUser = NormalizeUser(user);
                HashSet<string>? projectsForUser = LoadProjectNamesForUser(queryEngine, normalizedUser, dataSourceOverride);
                if (projectsForUser is { Count: 0 })
                {
                    return new List<JdeProjectInfo>();
                }

                var filters = BuildProjectStatusFilters(status);
                var projects = LoadProjects(queryEngine, filters, dataSourceOverride);

                return projectsForUser == null
                    ? projects
                    : FilterProjectsByUser(projects, projectsForUser);
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("GetProjectsAsync", "Failed to retrieve projects from F98220", ex);
            }
        }, cancellationToken);
    }

    private static string? NormalizeUser(string? user)
    {
        return string.IsNullOrWhiteSpace(user) ? null : user.Trim();
    }

    private static List<JdeFilter> BuildProjectStatusFilters(string? status)
    {
        var filters = new List<JdeFilter>();
        if (!string.IsNullOrWhiteSpace(status))
        {
            filters.Add(new JdeFilter
            {
                ColumnName = "OMWPS",
                Value = status.Trim(),
                Operator = JdeFilterOperator.Equals
            });
        }

        return filters;
    }

    private static HashSet<string>? LoadProjectNamesForUser(
        IJdeTableQueryEngine queryEngine,
        string? user,
        string? dataSourceOverride)
    {
        if (string.IsNullOrWhiteSpace(user))
        {
            return null;
        }

        var userFilters = new List<JdeFilter>();
        AddWildcardFilter(userFilters, "OMWUSER", user);

        var userResults = queryEngine.QueryTable("F98221", maxRows: 0, userFilters, dataSourceOverride);
        var projectsForUser = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in userResults.Rows)
        {
            var projectName = FindFirstValue(row, "OMWPRJID", "PROJECT");
            if (!string.IsNullOrWhiteSpace(projectName))
            {
                projectsForUser.Add(projectName);
            }
        }

        return projectsForUser;
    }

    private static List<JdeProjectInfo> LoadProjects(
        IJdeTableQueryEngine queryEngine,
        IReadOnlyList<JdeFilter> filters,
        string? dataSourceOverride)
    {
        var result = queryEngine.QueryTable("F98220", maxRows: 0, filters, dataSourceOverride);
        return MapProjects(result);
    }

    private static List<JdeProjectInfo> FilterProjectsByUser(
        List<JdeProjectInfo> projects,
        HashSet<string> projectsForUser)
    {
        return projects
            .Where(project => projectsForUser.Contains(project.ProjectName))
            .ToList();
    }

    private static void AddWildcardFilter(List<JdeFilter> filters, string columnName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        bool hasWildcard = trimmed.Contains('*', StringComparison.Ordinal);
        filters.Add(new JdeFilter
        {
            ColumnName = columnName,
            Value = hasWildcard ? trimmed.Replace('*', '%') : trimmed,
            Operator = hasWildcard ? JdeFilterOperator.Like : JdeFilterOperator.Equals
        });
    }

    private static void AddLikeFilter(List<JdeFilter> filters, string columnName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        string trimmed = value.Trim();
        if (trimmed.Length == 0)
        {
            return;
        }

        filters.Add(new JdeFilter
        {
            ColumnName = columnName,
            Value = trimmed.Contains('*', StringComparison.Ordinal) ? trimmed.Replace('*', '%') : trimmed,
            Operator = JdeFilterOperator.Like
        });
    }

    /// <summary>
    /// Retrieve OMW project objects from F98222.
    /// </summary>
    /// <param name="projectName">Project name (OMWPRJID).</param>
    /// <param name="pathCode">Optional path code (PATHCD) filter.</param>
    /// <param name="dataSourceOverride">Optional data source override for F98222.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<List<JdeProjectObjectInfo>> GetProjectObjectsAsync(
        string projectName,
        string? pathCode = null,
        string? dataSourceOverride = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectName))
        {
            throw new ArgumentException("Project name is required.", nameof(projectName));
        }

        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                using var queryEngine = _tableQueryEngineFactory.Create(_options);
                var filters = new List<JdeFilter>
                {
                    new()
                    {
                        ColumnName = "OMWPRJID",
                        Value = projectName.Trim(),
                        Operator = JdeFilterOperator.Equals
                    }
                };

                if (!string.IsNullOrWhiteSpace(pathCode))
                {
                    filters.Add(new JdeFilter
                    {
                        ColumnName = "PATHCD",
                        Value = pathCode.Trim(),
                        Operator = JdeFilterOperator.Equals
                    });
                }

                var result = queryEngine.QueryTable("F98222", maxRows: 0, filters, dataSourceOverride);
                var projectObjects = MapProjectObjects(result);
                return projectObjects;
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("GetProjectObjectsAsync", "Failed to retrieve project objects from F98222", ex);
            }
        }, cancellationToken);
    }

    #endregion

    #region Table Metadata

    /// <summary>
    /// Get table metadata including column information
    /// </summary>
    /// <param name="tableName">Table name (e.g., "F0101")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Table metadata or null if not found</returns>
    public async Task<JdeTableInfo?> GetTableInfoAsync(string tableName, CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                using var queryEngine = _tableQueryEngineFactory.Create(_options);
                var tableInfo = queryEngine.GetTableInfo(tableName, null, null);
                if (tableInfo == null)
                {
                    return null;
                }

                var objectInfo = _session.QueryEngine.GetObjectByName(tableName, JdeObjectType.Table);
                if (objectInfo != null)
                {
                    tableInfo.Description = objectInfo.Description;
                    tableInfo.SystemCode = objectInfo.SystemCode;
                    if (!string.IsNullOrWhiteSpace(objectInfo.ProductCode))
                    {
                        tableInfo.ProductCode = objectInfo.ProductCode;
                    }
                }

                return tableInfo;
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("GetTableInfoAsync", $"Failed to retrieve metadata for table {tableName}", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Get business view metadata including columns, tables, and joins.
    /// </summary>
    /// <param name="viewName">Business view name (e.g., "V0101A")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Business view metadata or null if not found</returns>
    public async Task<JdeBusinessViewInfo?> GetBusinessViewInfoAsync(string viewName, CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                using var queryEngine = _tableQueryEngineFactory.Create(_options);
                return queryEngine.GetBusinessViewInfo(viewName);
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("GetBusinessViewInfoAsync", $"Failed to retrieve metadata for view {viewName}", ex);
            }
        }, cancellationToken);
    }

    #endregion

    #region Table Query

    /// <summary>
    /// Retrieve event rule decode diagnostics for a specific event spec key (EVSK).
    /// </summary>
    public async Task<IReadOnlyList<JdeEventRulesDecodeDiagnostics>> GetEventRulesDecodeDiagnosticsAsync(
        string eventSpecKey,
        CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                var engine = _eventRulesQueryEngineFactory.Create(_session.UserHandle, _options);
                return engine.GetEventRulesDecodeDiagnostics(eventSpecKey);
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("GetEventRulesDecodeDiagnosticsAsync", "Failed to load event rules diagnostics", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Query a JDE table and return a buffered result set.
    /// Use this for small-to-medium result sets. For large result sets, prefer <see cref="QueryTableStream(string,int,CancellationToken)"/>.
    /// </summary>
    /// <param name="tableName">Table name (e.g., "F0101").</param>
    /// <param name="maxRows">Maximum number of rows to return (default: 1000).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<JdeQueryResult> QueryTableAsync(
        string tableName,
        int maxRows = 1000,
        CancellationToken cancellationToken = default)
    {
        return QueryTableAsync(tableName, filters: null, maxRows, dataSourceOverride: null, cancellationToken);
    }

    /// <summary>
    /// Query a JDE table and return a buffered result set with optional filters and data source override.
    /// </summary>
    /// <param name="tableName">Table name (e.g., "F0101").</param>
    /// <param name="filters">Optional filter list (empty or null for all rows).</param>
    /// <param name="maxRows">Maximum number of rows to return (default: 1000).</param>
    /// <param name="dataSourceOverride">Optional data source override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<JdeQueryResult> QueryTableAsync(
        string tableName,
        IReadOnlyList<JdeFilter>? filters,
        int maxRows = 1000,
        string? dataSourceOverride = null,
        CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                using var queryEngine = _tableQueryEngineFactory.Create(_options);
                var safeFilters = filters ?? Array.Empty<JdeFilter>();
                return queryEngine.QueryTable(tableName, maxRows, safeFilters, dataSourceOverride);
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("QueryTableAsync", $"Failed to query table {tableName}", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Count rows for a JDE table.
    /// </summary>
    /// <param name="tableName">Table name (e.g., "F0101").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<int> QueryTableCountAsync(
        string tableName,
        CancellationToken cancellationToken = default)
    {
        return QueryTableCountAsync(tableName, filters: null, dataSourceOverride: null, cancellationToken);
    }

    /// <summary>
    /// Count rows for a JDE table with optional filters and data source override.
    /// </summary>
    /// <param name="tableName">Table name (e.g., "F0101").</param>
    /// <param name="filters">Optional filter list (empty or null for all rows).</param>
    /// <param name="dataSourceOverride">Optional data source override.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<int> QueryTableCountAsync(
        string tableName,
        IReadOnlyList<JdeFilter>? filters,
        string? dataSourceOverride = null,
        CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                using var queryEngine = _tableQueryEngineFactory.Create(_options);
                var safeFilters = filters ?? Array.Empty<JdeFilter>();
                return queryEngine.CountTable(tableName, safeFilters, dataSourceOverride);
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("QueryTableCountAsync", $"Failed to count table {tableName}", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Stream rows from a JDE table without buffering the entire result set.
    /// Use this for large result sets or when you want incremental processing.
    /// </summary>
    /// <param name="tableName">Table name (e.g., "F0101").</param>
    /// <param name="maxRows">Maximum number of rows to return (0 for no limit).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public JdeQueryStream QueryTableStream(
        string tableName,
        int maxRows = 0,
        CancellationToken cancellationToken = default)
    {
        return QueryTableStream(
            tableName,
            filters: null,
            sorts: null,
            maxRows: maxRows,
            dataSourceOverride: null,
            indexId: null,
            allowDataSourceFallback: true,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Stream rows from a JDE table with optional filters, sorts, data source override, and index selection.
    /// </summary>
    /// <param name="tableName">Table name (e.g., "F0101").</param>
    /// <param name="filters">Optional filter list (empty or null for all rows).</param>
    /// <param name="sorts">Optional sort list.</param>
    /// <param name="maxRows">Maximum number of rows to return (0 for no limit).</param>
    /// <param name="dataSourceOverride">Optional data source override.</param>
    /// <param name="indexId">Optional index to force when querying the table.</param>
    /// <param name="allowDataSourceFallback">Allow data source resolution fallback when override is unavailable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public JdeQueryStream QueryTableStream(
        string tableName,
        IReadOnlyList<JdeFilter>? filters = null,
        IReadOnlyList<JdeSort>? sorts = null,
        int maxRows = 0,
        string? dataSourceOverride = null,
        int? indexId = null,
        bool allowDataSourceFallback = true,
        CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        try
        {
            var tableInfo = _session.ExecuteAsync(() =>
            {
                using var metadataEngine = _tableQueryEngineFactory.Create(_options);
                return metadataEngine.GetTableInfo(tableName, null, null);
            }).GetAwaiter().GetResult();

            if (tableInfo == null)
            {
                throw new JdeTableException(tableName, "Table not found.");
            }

            var columns = tableInfo.Columns;
            var columnNames = columns.Select(column => column.Name).ToList();
            var safeFilters = filters ?? Array.Empty<JdeFilter>();
            var rowQueue = new BlockingCollection<Dictionary<string, object>>();
            Exception? streamError = null;
            object errorLock = new();

            _ = _session.ExecuteAsync(() =>
            {
                try
                {
                    using var queryEngine = _tableQueryEngineFactory.Create(_options);
                    foreach (var row in queryEngine.StreamTableRows(tableName, maxRows, safeFilters, columns, dataSourceOverride, sorts, indexId, allowDataSourceFallback, cancellationToken))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            throw new OperationCanceledException(cancellationToken);
                        }
                        rowQueue.Add(row);
                    }
                }
                catch (Exception ex)
                {
                    lock (errorLock)
                    {
                        streamError ??= ex;
                    }
                }
                finally
                {
                    rowQueue.CompleteAdding();
                }

                return true;
            }, cancellationToken);

            IEnumerable<Dictionary<string, object>> Enumerate()
            {
                foreach (var row in rowQueue.GetConsumingEnumerable(cancellationToken))
                {
                    yield return row;
                }

                if (streamError != null)
                {
                    ExceptionDispatchInfo.Capture(streamError).Throw();
                }
            }

            return new JdeQueryStream(
                tableName,
                columnNames,
                maxRows > 0 ? maxRows : null,
                Enumerate);
        }
        catch (Exception ex) when (ex is not JdeException)
        {
            throw new JdeApiException("QueryTableStream", $"Failed to stream table {tableName}", ex);
        }
    }

    /// <summary>
    /// Stream rows from a JDE business view without buffering the entire result set.
    /// </summary>
    /// <param name="viewName">Business view name (e.g., "V0101A").</param>
    /// <param name="filters">Optional filter list (empty or null for all rows).</param>
    /// <param name="sorts">Optional sort list.</param>
    /// <param name="maxRows">Maximum number of rows to return (0 for no limit).</param>
    /// <param name="dataSourceOverride">Optional data source override.</param>
    /// <param name="allowDataSourceFallback">Allow data source resolution fallback when override is unavailable.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public JdeQueryStream QueryViewStream(
        string viewName,
        IReadOnlyList<JdeFilter>? filters = null,
        IReadOnlyList<JdeSort>? sorts = null,
        int maxRows = 0,
        string? dataSourceOverride = null,
        bool allowDataSourceFallback = true,
        CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        try
        {
            var columns = _session.ExecuteAsync(() =>
            {
                using var metadataEngine = _tableQueryEngineFactory.Create(_options);
                return metadataEngine.GetViewColumns(viewName);
            }).GetAwaiter().GetResult();

            if (columns.Count == 0)
            {
                throw new JdeTableException(viewName, "Business view not found.");
            }

            var columnNames = columns.Select(column => column.Name).ToList();
            var safeFilters = filters ?? Array.Empty<JdeFilter>();
            var rowQueue = new BlockingCollection<Dictionary<string, object>>();
            Exception? streamError = null;
            object errorLock = new();

            _ = _session.ExecuteAsync(() =>
            {
                try
                {
                    using var queryEngine = _tableQueryEngineFactory.Create(_options);
                    foreach (var row in queryEngine.StreamViewRows(viewName, maxRows, safeFilters, columns, dataSourceOverride, sorts, allowDataSourceFallback, cancellationToken))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            throw new OperationCanceledException(cancellationToken);
                        }

                        rowQueue.Add(row);
                    }
                }
                catch (Exception ex)
                {
                    lock (errorLock)
                    {
                        streamError ??= ex;
                    }
                }
                finally
                {
                    rowQueue.CompleteAdding();
                }

                return true;
            }, cancellationToken);

            IEnumerable<Dictionary<string, object>> Enumerate()
            {
                foreach (var row in rowQueue.GetConsumingEnumerable(cancellationToken))
                {
                    yield return row;
                }

                if (streamError != null)
                {
                    ExceptionDispatchInfo.Capture(streamError).Throw();
                }
            }

            return new JdeQueryStream(
                viewName,
                columnNames,
                maxRows > 0 ? maxRows : null,
                Enumerate);
        }
        catch (Exception ex) when (ex is not JdeException)
        {
            throw new JdeApiException("QueryViewStream", $"Failed to stream view {viewName}", ex);
        }
    }

    /// <summary>
    /// Retrieve table index metadata from table specs.
    /// </summary>
    /// <param name="tableName">Table name (e.g., "F0101")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Index definitions</returns>
    public async Task<List<JdeIndexInfo>> GetTableIndexesAsync(
        string tableName,
        CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                using var queryEngine = _tableQueryEngineFactory.Create(_options);
                return queryEngine.GetTableIndexes(tableName);
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("GetTableIndexesAsync", $"Failed to retrieve indexes for table {tableName}", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Retrieve data dictionary titles for the provided data item names (F9202).
    /// </summary>
    /// <param name="dataItems">Data dictionary items (DTAI)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Titles for matching items</returns>
    public async Task<List<JdeDataDictionaryTitle>> GetDataDictionaryTitlesAsync(
        IEnumerable<string> dataItems,
        CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                using var queryEngine = _tableQueryEngineFactory.Create(_options);
                return queryEngine.GetDataDictionaryTitles(dataItems);
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("GetDataDictionaryTitlesAsync", "Failed to retrieve data dictionary titles", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Retrieve data dictionary descriptions (row/alpha) for the provided data item names.
    /// </summary>
    /// <param name="dataItems">Data dictionary items (DTAI)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Descriptions for matching items</returns>
    public async Task<List<JdeDataDictionaryTitle>> GetDataDictionaryDescriptionsAsync(
        IEnumerable<string> dataItems,
        CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                using var queryEngine = _tableQueryEngineFactory.Create(_options);
                return queryEngine.GetDataDictionaryTitles(
                    dataItems,
                    new[]
                    {
                        JdeStructures.DDT_GLOSSARY,
                        JdeStructures.DDT_ROW_DESC,
                        JdeStructures.DDT_ALPHA_DESC,
                        JdeStructures.DDT_COL_TITLE
                    });
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("GetDataDictionaryDescriptionsAsync", "Failed to retrieve data dictionary descriptions", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Retrieve data dictionary item names for the provided data item aliases (F9200).
    /// </summary>
    /// <param name="dataItems">Data dictionary items (DTAI)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Names for matching items</returns>
    public async Task<List<JdeDataDictionaryItemName>> GetDataDictionaryItemNamesAsync(
        IEnumerable<string> dataItems,
        CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                using var queryEngine = _tableQueryEngineFactory.Create(_options);
                return queryEngine.GetDataDictionaryItemNames(dataItems);
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("GetDataDictionaryItemNamesAsync", "Failed to retrieve data dictionary names", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Retrieve data dictionary detail records for the provided data item names (DDDICT + DDTEXT).
    /// </summary>
    /// <param name="dataItems">Data dictionary items (DTAI)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Detail records for matching items</returns>
    public async Task<List<JdeDataDictionaryDetails>> GetDataDictionaryDetailsAsync(
        IEnumerable<string> dataItems,
        CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                using var queryEngine = _tableQueryEngineFactory.Create(_options);
                return queryEngine.GetDataDictionaryDetails(dataItems);
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("GetDataDictionaryDetailsAsync", "Failed to retrieve data dictionary details", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Build the event rules tree for the supplied object.
    /// </summary>
    public async Task<JdeEventRulesNode> GetEventRulesTreeAsync(
        JdeObjectInfo jdeObject,
        CancellationToken cancellationToken = default)
    {
        if (jdeObject == null)
        {
            throw new ArgumentNullException(nameof(jdeObject));
        }

        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                var engine = _eventRulesQueryEngineFactory.Create(_session.UserHandle, _options);
                string? type = jdeObject.ObjectType?.Trim();
                if (string.Equals(type, "BSFN", StringComparison.OrdinalIgnoreCase))
                {
                    return engine.GetBusinessFunctionTree(jdeObject.ObjectName);
                }

                if (string.Equals(type, "NER", StringComparison.OrdinalIgnoreCase))
                {
                    return engine.GetNamedEventRuleTree(jdeObject.ObjectName);
                }

                if (string.Equals(type, "APPL", StringComparison.OrdinalIgnoreCase))
                {
                    return engine.GetApplicationEventRulesTree(jdeObject.ObjectName);
                }

                if (string.Equals(type, "UBE", StringComparison.OrdinalIgnoreCase))
                {
                    return engine.GetReportEventRulesTree(jdeObject.ObjectName);
                }

                if (string.Equals(type, "TBLE", StringComparison.OrdinalIgnoreCase))
                {
                    return engine.GetTableEventRulesTree(jdeObject.ObjectName);
                }

                return new JdeEventRulesNode
                {
                    Id = jdeObject.ObjectName,
                    Name = jdeObject.ObjectName,
                    NodeType = JdeEventRulesNodeType.Object,
                    Children = Array.Empty<JdeEventRulesNode>()
                };
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("GetEventRulesTreeAsync", $"Failed to load event rules for {jdeObject.ObjectName}", ex);
            }
        }, cancellationToken);
    }

    
    
    /// <summary>
    /// Retrieve event rule lines for a specific event spec key (EVSK).
    /// </summary>
    public async Task<IReadOnlyList<JdeEventRuleLine>> GetEventRulesLinesAsync(
        string eventSpecKey,
        CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                var engine = _eventRulesQueryEngineFactory.Create(_session.UserHandle, _options);
                return engine.GetEventRulesLines(eventSpecKey);
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("GetEventRulesLinesAsync", "Failed to load event rules", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Retrieve raw XML documents for a specific event spec key (EVSK).
    /// </summary>
    public async Task<IReadOnlyList<JdeEventRulesXmlDocument>> GetEventRulesXmlAsync(
        string eventSpecKey,
        CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                var engine = _eventRulesQueryEngineFactory.Create(_session.UserHandle, _options);
                return engine.GetEventRulesXmlDocuments(eventSpecKey);
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("GetEventRulesXmlAsync", "Failed to load event rules XML", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Retrieve raw XML documents for a data structure template (DSTMPL).
    /// </summary>
    public async Task<IReadOnlyList<JdeSpecXmlDocument>> GetDataStructureXmlAsync(
        string templateName,
        CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                var engine = _eventRulesQueryEngineFactory.Create(_session.UserHandle, _options);
                return engine.GetDataStructureXmlDocuments(templateName);
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("GetDataStructureXmlAsync", "Failed to load data structure XML", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Resolve the default data source for a table.
    /// </summary>
    /// <param name="tableName">Table name (e.g., "F0101")</param>
    /// <returns>Resolved data source name or null if unavailable</returns>
    public async Task<string?> GetDefaultTableDataSourceAsync(
        string tableName,
        CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                return _dataSourceResolver.ResolveTableDataSource(_session.UserHandle, tableName);
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("GetDefaultTableDataSourceAsync", $"Failed to resolve data source for {tableName}", ex);
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Retrieve available data sources from F98611 (System - 920).
    /// </summary>
    /// <param name="dataSourceOverride">Optional override for the F98611 data source (default: System - 920)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of data sources</returns>
    public async Task<List<JdeDataSourceInfo>> GetAvailableDataSourcesAsync(
        string? dataSourceOverride = "System - 920",
        CancellationToken cancellationToken = default)
    {
        _session.EnsureConnected();

        return await _session.ExecuteAsync(() =>
        {
            try
            {
                using var queryEngine = _tableQueryEngineFactory.Create(_options);
                var result = QueryDataSourcesWithFallback(queryEngine, dataSourceOverride);
                return MapDataSources(result);
            }
            catch (Exception ex) when (ex is not JdeException)
            {
                throw new JdeApiException("GetAvailableDataSourcesAsync", "Failed to retrieve data sources from F98611", ex);
            }
        }, cancellationToken);
    }

    private JdeQueryResult QueryDataSourcesWithFallback(IJdeTableQueryEngine queryEngine, string? dataSourceOverride)
    {
        JdeTableInfo tableInfo = queryEngine.GetTableInfo("F98611", null, null);
        var candidates = new List<string?>();
        if (!string.IsNullOrWhiteSpace(dataSourceOverride))
        {
            candidates.Add(dataSourceOverride);
        }

        var resolved = _dataSourceResolver.ResolveTableDataSource(_session.UserHandle, "F98611");
        if (!string.IsNullOrWhiteSpace(resolved) && !candidates.Contains(resolved, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add(resolved);
        }

        if (!candidates.Contains("Object Librarian - 920", StringComparer.OrdinalIgnoreCase))
        {
            candidates.Add("Object Librarian - 920");
        }

        candidates.Add(null);

        foreach (var candidate in candidates)
        {
            var rows = new List<Dictionary<string, object>>();
            foreach (var row in queryEngine.StreamTableRows("F98611", maxRows: 0, Array.Empty<JdeFilter>(), tableInfo.Columns, candidate, sorts: null, indexId: null, allowDataSourceFallback: true, cancellationToken: CancellationToken.None))
            {
                rows.Add(row);
            }

            if (rows.Count > 0)
            {
                return new JdeQueryResult
                {
                    TableName = "F98611",
                    ColumnNames = tableInfo.Columns.Select(column => column.Name).ToList(),
                    Rows = rows
                };
            }
        }

        return new JdeQueryResult { TableName = "F98611" };
    }

    private static List<JdeDataSourceInfo> MapDataSources(JdeQueryResult result)
    {
        var items = new List<JdeDataSourceInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in result.Rows)
        {
            string? databasePath = FindFirstValue(row, "DATP", "DBPATH", "DSDBPATH", "DATABSEPATH", "DATABASEPATH", "DATAPATH", "DBPATHN");
            string? server = FindFirstValue(row, "SRVR", "SERVER", "DBSERVER", "SERVERNAME", "DSDBSRVR", "DSERVER");
            string? database = FindFirstValue(row, "DATB", "DBNAME", "DATABASE", "DATABASE_NAME", "DSDBNAME", "DSDB");
            string? name = FindFirstValue(row, "DATP", "DATB", "DATASOURCE", "DSOURCE", "DSNAME", "NAME", "DSDS", "DATASOURCENAME")
                ?? databasePath;

            if (string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (!seen.Add(name))
            {
                continue;
            }

            items.Add(new JdeDataSourceInfo
            {
                Name = name,
                DatabasePath = databasePath,
                ServerName = server,
                DatabaseName = database
            });
        }

        items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return items;
    }

    private static List<JdeProjectInfo> MapProjects(JdeQueryResult result)
    {
        var items = new List<JdeProjectInfo>();

        foreach (var row in result.Rows)
        {
            string? projectName = FindFirstValue(row, "OMWPRJID");
            if (string.IsNullOrWhiteSpace(projectName))
            {
                continue;
            }

            items.Add(new JdeProjectInfo
            {
                ProjectName = projectName.Trim(),
                Description = FindFirstValue(row, "OMWDESC"),
                Status = FindFirstValue(row, "OMWPS"),
                Type = FindFirstValue(row, "OMWTYP"),
                SourceRelease = FindFirstValue(row, "SRCRLS"),
                TargetRelease = FindFirstValue(row, "TRGRLS"),
                SaveName = FindFirstValue(row, "DSAVNAME")
            });
        }

        items.Sort((a, b) => string.Compare(a.ProjectName, b.ProjectName, StringComparison.OrdinalIgnoreCase));
        return items;
    }
    
    

    private static List<JdeProjectObjectInfo> MapProjectObjects(JdeQueryResult result)
    {
        var items = new List<JdeProjectObjectInfo>();

        foreach (var row in result.Rows)
        {
            var projectName = FindFirstValue(row, "OMWPRJID", "PROJECT");
            var objectId = FindFirstValue(row, "OMWOBJID");
            var objectType = FindFirstValue(row, "OMWOT");

            if (string.IsNullOrWhiteSpace(projectName) || string.IsNullOrWhiteSpace(objectId) || string.IsNullOrWhiteSpace(objectType))
            {
                continue;
            }

            SplitObjectId(objectId, out var objectName, out var versionName);

            items.Add(new JdeProjectObjectInfo
            {
                ProjectName = projectName.Trim(),
                ObjectId = objectId,
                ObjectName = objectName.Trim(),
                VersionName = versionName,
                ObjectType = objectType,
                PathCode = FindFirstValue(row, "PATHCD", "PATHCODE"),
                SourceRelease = FindFirstValue(row, "SRCRLS", "SOURCE_RELEASE"),
                ObjectStatus = FindFirstValue(row, "OMWOST", "OBJECT_STATUS"),
                VersionStatus = FindFirstValue(row, "OMWOVS", "VERSION_STATUS"),
                User = FindFirstValue(row, "OMWUSER", "USER")
            });
        }

        items.Sort((a, b) => string.Compare(a.ObjectId, b.ObjectId, StringComparison.OrdinalIgnoreCase));
        return items;
    }

    private static string? FindFirstValue(Dictionary<string, object> row, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (row.TryGetValue(candidate, out var value))
            {
                var text = value?.ToString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }
            }
        }

        foreach (var entry in row)
        {
            foreach (var candidate in candidates)
            {
                if (entry.Key.Contains(candidate, StringComparison.OrdinalIgnoreCase))
                {
                    var text = entry.Value?.ToString()?.Trim();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
        }

        return null;
    }

    private static void SplitObjectId(string objectId, out string objectName, out string? versionName)
    {
        objectName = string.Empty;
        versionName = null;

        if (string.IsNullOrWhiteSpace(objectId))
        {
            return;
        }

        int marker = objectId.IndexOf('!');
        if (marker < 0)
        {
            objectName = objectId.Trim();
            return;
        }

        objectName = objectId.Substring(0, marker).Trim();
        int versionStart = marker + 1;
        if (versionStart >= objectId.Length)
        {
            return;
        }

        int versionLength = Math.Min(10, objectId.Length - versionStart);
        string version = objectId.Substring(versionStart, versionLength).Trim();
        if (!string.IsNullOrWhiteSpace(version))
        {
            versionName = version;
        }
    }

    #endregion
    
    #region UserDefinedCodes

    /// <summary>
    /// Retrieve User Defined Code Types
    /// </summary>
    /// <param name="productCode">The System/Product code of the UDC Type (optional)</param>
    /// <param name="userDefinedCode">The Code Type of the UDC (optional)</param>
    /// <param name="dataSourceOverride">Optional Datasource over (Will default to a default location)</param>
    /// <param name="maxRows"></param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>Returns a List of JdeUserDefinedCodeTypes</returns>
    public async Task<List<JdeUserDefinedCodeTypes>> GetUserDefinedCodeTypesAsync(
        string? productCode = null,
        string? userDefinedCode = null,
        string? dataSourceOverride = null,
        int maxRows = 0,
        CancellationToken cancellationToken = default)
    {
        var filters = new List<JdeFilter>();
        AddWildcardFilter(filters, "SY", productCode);
        AddWildcardFilter(filters, "RT", userDefinedCode);
        
        var results = await QueryTableAsync(
            "F0004",
            filters,
            maxRows: maxRows,
            dataSourceOverride: dataSourceOverride,
            cancellationToken: cancellationToken);
        var codes = MapUserDefinedCodeTypes(results);
        return codes;
    }

    
    private static List<JdeUserDefinedCodeTypes> MapUserDefinedCodeTypes(JdeQueryResult result)
    {
        var codes = new List<JdeUserDefinedCodeTypes>();

        foreach (var row in result.Rows)
        {
            var productCode = FindFirstValue(row, "SY");
            var userDefinedCodeType = FindFirstValue(row, "RT");
            var description = FindFirstValue(row, "DL01");
            var codeLength = FindFirstValue(row, "CDL");
            codes.Add(new JdeUserDefinedCodeTypes(productCode, userDefinedCodeType, description, codeLength));
        }
        return codes;
    }

    /// <summary>
    /// Retrieve User Defined Codes for the given Product Code and Code Type
    /// </summary>
    /// <param name="productCode">The System/Product code of the UDC Type</param>
    /// <param name="userDefinedCodeType">The Code Type of the UDC</param>
    /// <param name="userDefinedCode"></param>
    /// <param name="description"></param>
    /// <param name="description2"></param>
    /// <param name="dataSourceOverride">Optional Datasource over (Will default to a default location)</param>
    /// <param name="maxRows"></param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>Returns a List of JdeUserDefinedCodes</returns>
    public async Task<List<JdeUserDefinedCodes>> GetUserDefinedCodesAsync(
        string productCode,
        string userDefinedCodeType,
        string? userDefinedCode,
        string? description,
        string? description2,
        string? dataSourceOverride = null,
        int maxRows = 0,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productCode))
        {
            throw new ArgumentNullException(nameof(productCode));
        }
        if (string.IsNullOrWhiteSpace(userDefinedCodeType))
        {
            throw new ArgumentNullException(nameof(userDefinedCodeType));
        }
        
        var filters = new List<JdeFilter>();
        AddWildcardFilter(filters, "SY", productCode);
        AddWildcardFilter(filters, "RT", userDefinedCodeType);

        AddWildcardFilter(filters, "KY", userDefinedCode);
        AddLikeFilter(filters, "DL01", description);
        AddLikeFilter(filters, "DL02", description2);
        
        var results = await QueryTableAsync(
            "F0005",
            filters,
            maxRows: maxRows,
            dataSourceOverride: dataSourceOverride,
            cancellationToken: cancellationToken);
        var codes = MapUserDefinedCodes(results);

        return codes;
    }

    private static List<JdeUserDefinedCodes> MapUserDefinedCodes(JdeQueryResult results)
    {
        var codes = new List<JdeUserDefinedCodes>();

        foreach (var row in results.Rows)
        {
            var productCode = FindFirstValue(row, "SY");
            var userDefinedCodeType = FindFirstValue(row, "RT");
            var userDefinedCode = FindFirstValue(row, "KY");
            var description = FindFirstValue(row, "DL01");
            var description2 = FindFirstValue(row, "DL02");
            var specialHandlingCode = FindFirstValue(row, "SPHD");
            var hardCoded = FindFirstValue(row, "HRDC");
            
            codes.Add(new JdeUserDefinedCodes(productCode, userDefinedCodeType, userDefinedCode, description,
                description2, specialHandlingCode, hardCoded));
        }

        return codes;
    }

    #endregion

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _session.Dispose();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
