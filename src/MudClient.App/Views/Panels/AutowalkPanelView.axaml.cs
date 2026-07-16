using Avalonia.Controls;
using Avalonia.Interactivity;
using MudClient.App.Controls;
using MudClient.App.Models;
using MudClient.App.ViewModels;

namespace MudClient.App.Views.Panels;

public sealed partial class AutowalkPanelView : UserControl
{
    private MainWindowViewModel? _viewModel;
    internal Func<Window, string, string, Task<bool>> ConfirmDeletionAsync { get; set; } =
        DeleteConfirmationDialog.ShowAsync;

    public AutowalkPanelView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => _viewModel = DataContext as MainWindowViewModel;
    }

    private void GoToLocation_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is AutowalkLocation location &&
            _viewModel is not null)
        {
            _viewModel.GoToLocationCommand.Execute(location);
        }
    }

    private void GoToDeath_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is DeathMarkEntry entry &&
            _viewModel is not null)
        {
            _viewModel.GoToDeathCommand.Execute(entry);
        }
    }

    private void DeleteDeath_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is DeathMarkEntry entry &&
            _viewModel is not null)
        {
            _viewModel.DeleteDeathCommand.Execute(entry);
        }
    }

    private async void DeleteLocation_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is AutowalkLocation location &&
            _viewModel is not null)
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }

            button.IsEnabled = false;
            try
            {
                if (await ConfirmDeletionAsync(owner, "cel autowalk", location.Name))
                {
                    _viewModel.DeleteLocationCommand.Execute(location);
                }
            }
            finally
            {
                button.IsEnabled = true;
            }
        }
    }
}
