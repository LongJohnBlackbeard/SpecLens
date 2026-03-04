using System.Collections;

namespace JdeClient.Core.Models;

/// <summary>
/// Typed row container for query results.
/// Values are stored as normalized strings keyed by column name (case-insensitive).
/// </summary>
public sealed class JdeRow : IReadOnlyDictionary<string, string>
{
    private readonly Dictionary<string, string> _values;

    public JdeRow()
        : this(StringComparer.OrdinalIgnoreCase)
    {
    }

    public JdeRow(IEqualityComparer<string> comparer)
    {
        _values = new Dictionary<string, string>(comparer ?? StringComparer.OrdinalIgnoreCase);
    }

    public JdeRow(IDictionary<string, string> values, IEqualityComparer<string>? comparer = null)
        : this(comparer ?? StringComparer.OrdinalIgnoreCase)
    {
        foreach (var pair in values)
        {
            _values[pair.Key] = pair.Value ?? string.Empty;
        }
    }

    public string this[string key]
    {
        get => _values[key];
        set => _values[key] = value ?? string.Empty;
    }

    public IEnumerable<string> Keys => _values.Keys;
    public IEnumerable<string> Values => _values.Values;
    public int Count => _values.Count;

    public bool ContainsKey(string key) => _values.ContainsKey(key);

    public bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);

    public void Add(string key, string value)
    {
        _values.Add(key, value ?? string.Empty);
    }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

}
