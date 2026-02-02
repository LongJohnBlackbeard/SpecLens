using ReactiveUI;

namespace SpecLens.Avalonia.ViewModels;

/// <summary>
/// Base class for all workspace tab view models.
/// </summary>
public abstract class WorkspaceTabViewModel : ViewModelBase, IActivatableViewModel
{
    private string _header = string.Empty;
    private string _tabId = string.Empty;
    private bool _isActive;

    public ViewModelActivator Activator { get; } = new();

    public string Header
    {
        get => _header;
        set => this.RaiseAndSetIfChanged(ref _header, value);
    }

    public string TabId
    {
        get => _tabId;
        set => this.RaiseAndSetIfChanged(ref _tabId, value);
    }

    public bool IsActive
    {
        get => _isActive;
        set => this.RaiseAndSetIfChanged(ref _isActive, value);
    }

    protected WorkspaceTabViewModel(string header, string tabId)
    {
        Header = header;
        TabId = tabId;
    }

    /// <summary>
    /// Called when the tab becomes active.
    /// Override to perform tab-specific initialization or refresh logic.
    /// </summary>
    public virtual void OnActivated()
    {
        Activator.Activate();
    }

    /// <summary>
    /// Called when the tab becomes inactive.
    /// Override to perform cleanup or save state.
    /// </summary>
    public virtual void OnDeactivated()
    {
        Activator.Deactivate();
    }

    /// <summary>
    /// Called when the tab is being closed.
    /// Return false to cancel the close operation.
    /// </summary>
    public virtual bool OnClosing()
    {
        return true;
    }
}
