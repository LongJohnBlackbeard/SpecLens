using System.Windows.Input;
using Avalonia.ReactiveUI;
using Avalonia.Controls;
using ReactiveUI;
using SpecLens.Avalonia.Services;
using RoutedEventArgs = global::Avalonia.Interactivity.RoutedEventArgs;

namespace SpecLens.Avalonia.Settings;

public partial class SettingsWindow : ReactiveWindow<SettingsViewModel>
{
    public SettingsWindow()
    {
        InitializeComponent();
        this.WhenActivated(_ => { });
    }

    public SettingsWindow(IAppSettingsService settingsService)
    {
        InitializeComponent();
        ViewModel = new SettingsViewModel(settingsService);
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is SettingsViewModel viewModel)
        {
            ((ICommand)viewModel.SaveCommand).Execute(null);
        }
    }
}
