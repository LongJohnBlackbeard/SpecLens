using System.Collections.Generic;
using System.Linq;

namespace SpecLens.Avalonia.Models;

public sealed class SpecIndexDisplay
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public List<string> KeyColumns { get; set; } = new();

    public string KeyColumnsDisplay => KeyColumns.Count == 0 ? string.Empty : string.Join(", ", KeyColumns);
    public string PrimaryDisplay => IsPrimary ? "Yes" : string.Empty;
}
