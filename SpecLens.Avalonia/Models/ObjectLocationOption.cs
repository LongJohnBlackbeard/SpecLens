using System;

namespace SpecLens.Avalonia.Models;

public sealed class ObjectLocationOption
{
    public static ObjectLocationOption Local { get; } = new("Local", null);

    public ObjectLocationOption(string label, string? pathCode)
    {
        Label = string.IsNullOrWhiteSpace(label) ? "Local" : label.Trim();
        PathCode = string.IsNullOrWhiteSpace(pathCode) ? null : pathCode.Trim();
    }

    public string Label { get; }
    public string? PathCode { get; }

    public bool IsLocal => string.IsNullOrWhiteSpace(PathCode);
    public string DisplayName => IsLocal ? "Local" : PathCode!;
    public string? ObjectLibrarianDataSourceOverride => IsLocal ? null : $"Object Librarian - {PathCode}";
    public string? CentralObjectsDataSourceOverride => IsLocal ? null : $"Central Objects - {PathCode}";

    public static ObjectLocationOption FromPathCode(string? pathCode)
    {
        return string.IsNullOrWhiteSpace(pathCode)
            ? Local
            : new ObjectLocationOption(pathCode.Trim(), pathCode.Trim());
    }

    public bool MatchesPathCode(string? pathCode)
    {
        if (string.IsNullOrWhiteSpace(pathCode))
        {
            return IsLocal;
        }

        return string.Equals(PathCode, pathCode.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    public override string ToString() => Label;
}
