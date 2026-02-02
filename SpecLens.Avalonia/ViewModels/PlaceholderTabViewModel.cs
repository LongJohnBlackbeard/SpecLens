namespace SpecLens.Avalonia.ViewModels;

/// <summary>
/// Placeholder tab view model for testing tab infrastructure.
/// Will be replaced with QueryTabViewModel and SpecsTabViewModel.
/// </summary>
public class PlaceholderTabViewModel : WorkspaceTabViewModel
{
    public string TabType { get; }

    public PlaceholderTabViewModel(string header, string tabId, string tabType)
        : base(header, tabId)
    {
        TabType = tabType;
    }
}
