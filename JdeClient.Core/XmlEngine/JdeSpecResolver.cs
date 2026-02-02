using JdeClient.Core.Models;
using JdeClient.Core.XmlEngine.Models;

namespace JdeClient.Core.XmlEngine;

/// <summary>
/// Resolves JDE specs (DSTMPL, table metadata, data dictionary titles) via <see cref="JdeClient"/>.
/// </summary>
/// <remarks>
/// This resolver assumes the JDE runtime is available and the client is connected.
/// </remarks>
public sealed class JdeSpecResolver
{
    private readonly JdeClient _client;
    private readonly Dictionary<string, DataStructureTemplate> _templateCache;
    private readonly Dictionary<string, JdeTableInfo> _tableInfoCache;
    private readonly Dictionary<string, List<JdeIndexInfo>> _indexCache;
    private readonly Dictionary<string, string?> _ddTitleCache;
    private readonly Dictionary<string, string?> _bsfnNameCache;

    public JdeSpecResolver(JdeClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _templateCache = new Dictionary<string, DataStructureTemplate>(StringComparer.OrdinalIgnoreCase);
        _tableInfoCache = new Dictionary<string, JdeTableInfo>(StringComparer.OrdinalIgnoreCase);
        _indexCache = new Dictionary<string, List<JdeIndexInfo>>(StringComparer.OrdinalIgnoreCase);
        _ddTitleCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        _bsfnNameCache = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Resolve a data structure template (DSTMPL) by template name.
    /// </summary>
    public DataStructureTemplate? GetDataStructureTemplate(string templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            return null;
        }

        if (_templateCache.TryGetValue(templateName, out var cached))
        {
            return cached;
        }

        var documents = _client.GetDataStructureXmlAsync(templateName).GetAwaiter().GetResult();
        var document = documents.FirstOrDefault(d => !string.IsNullOrWhiteSpace(d.Xml));
        if (document == null)
        {
            return null;
        }

        if (!TryParseTemplate(templateName, document.Xml, out var template))
        {
            return null;
        }

        _templateCache[templateName] = template;
        return template;
    }

    /// <summary>
    /// Resolve table metadata by table name.
    /// </summary>
    public JdeTableInfo? GetTableInfo(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return null;
        }

        if (_tableInfoCache.TryGetValue(tableName, out var cached))
        {
            return cached;
        }

        var info = _client.GetTableInfoAsync(tableName).GetAwaiter().GetResult();
        if (info == null)
        {
            return null;
        }

        _tableInfoCache[tableName] = info;
        return info;
    }

    /// <summary>
    /// Resolve table index metadata by table name.
    /// </summary>
    public IReadOnlyList<JdeIndexInfo> GetTableIndexes(string tableName)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return Array.Empty<JdeIndexInfo>();
        }

        if (_indexCache.TryGetValue(tableName, out var cached))
        {
            return cached;
        }

        var indexes = _client.GetTableIndexesAsync(tableName).GetAwaiter().GetResult();
        _indexCache[tableName] = indexes;
        return indexes;
    }

    /// <summary>
    /// Resolve data dictionary titles for the provided items.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetDataDictionaryTitles(IEnumerable<string> dataItems)
    {
        var items = dataItems
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (items.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var missing = items
            .Where(item => !_ddTitleCache.ContainsKey(item))
            .ToList();

        if (missing.Count > 0)
        {
            var titles = _client.GetDataDictionaryTitlesAsync(missing).GetAwaiter().GetResult();
            foreach (var title in titles)
            {
                if (string.IsNullOrWhiteSpace(title.DataItem))
                {
                    continue;
                }

                _ddTitleCache[title.DataItem] = title.CombinedTitle;
            }

            foreach (var item in missing)
            {
                _ddTitleCache.TryAdd(item, null);
            }
        }

        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            if (_ddTitleCache.TryGetValue(item, out var title) &&
                !string.IsNullOrWhiteSpace(title))
            {
                resolved[item] = title;
            }
        }

        return resolved;
    }

    /// <summary>
    /// Resolve the business function (BSFN) object name for a template name.
    /// </summary>
    /// <remarks>
    /// DSTMPL names often start with "D"; this resolver maps that prefix to "B" for BSFN lookup.
    /// </remarks>
    public string? ResolveBusinessFunctionName(string templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            return null;
        }

        if (_bsfnNameCache.TryGetValue(templateName, out var cached))
        {
            return cached;
        }

        var candidate = BuildBusinessFunctionSearchPattern(templateName);
        var matches = _client.GetObjectsAsync(
                JdeObjectType.BusinessFunction,
                searchPattern: candidate,
                maxResults: 1)
            .GetAwaiter()
            .GetResult();

        var resolved = matches.FirstOrDefault()?.ObjectName ?? candidate;
        _bsfnNameCache[templateName] = resolved;
        return resolved;
    }

    private static string BuildBusinessFunctionSearchPattern(string templateName)
    {
        if (templateName.Length > 1 && templateName.StartsWith("D", StringComparison.OrdinalIgnoreCase))
        {
            return $"B{templateName.Substring(1)}";
        }

        return templateName;
    }

    private static bool TryParseTemplate(string templateName, string xml, out DataStructureTemplate template)
    {
        template = null!;
        try
        {
            template = DataStructureTemplate.Parse(templateName, xml);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
