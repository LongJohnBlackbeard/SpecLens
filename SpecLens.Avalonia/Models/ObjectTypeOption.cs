using JdeClient.Core.Models;

namespace SpecLens.Avalonia.Models;

public sealed class ObjectTypeOption
{
    public ObjectTypeOption(string label, JdeObjectType value)
    {
        Label = label;
        Value = value;
    }

    public string Label { get; }
    public JdeObjectType Value { get; }
}
