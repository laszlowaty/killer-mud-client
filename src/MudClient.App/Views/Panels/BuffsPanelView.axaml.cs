using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MudClient.App.Controls;
using MudClient.App.ViewModels;

namespace MudClient.App.Views.Panels;

public sealed partial class BuffsPanelView : UserControl
{
    internal Func<Window, string, string, Task<bool>> ConfirmDeletionAsync { get; set; } =
        DeleteConfirmationDialog.ShowAsync;

    public BuffsPanelView()
    {
        InitializeComponent();
    }

    private void NewBuffBox_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key is not (Key.Enter or Key.Return)
            || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        eventArgs.Handled = true;
        if (viewModel.AddBuffCommand.CanExecute(null))
        {
            viewModel.AddBuffCommand.Execute(null);
            AddBuffButton.Flyout?.Hide();
        }
    }

    private void AddBuff_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel
            && viewModel.AddBuffCommand.CanExecute(null))
        {
            viewModel.AddBuffCommand.Execute(null);
            AddBuffButton.Flyout?.Hide();
        }
    }

    private void NewBuffSetBox_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key is not (Key.Enter or Key.Return)
            || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        eventArgs.Handled = true;
        if (viewModel.CreateBuffSetCommand.CanExecute(null))
        {
            viewModel.CreateBuffSetCommand.Execute(null);
            AddSetButton.Flyout?.Hide();
        }
    }

    private void CreateBuffSet_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel
            && viewModel.CreateBuffSetCommand.CanExecute(null))
        {
            viewModel.CreateBuffSetCommand.Execute(null);
            AddSetButton.Flyout?.Hide();
        }
    }

    private void BuffSetNameBox_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key is not (Key.Enter or Key.Return)
            || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        eventArgs.Handled = true;
        if (viewModel.RenameBuffSetCommand.CanExecute(null))
        {
            viewModel.RenameBuffSetCommand.Execute(null);
            ManageSetButton.Flyout?.Hide();
        }
    }

    private void RenameBuffSet_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel
            && viewModel.RenameBuffSetCommand.CanExecute(null))
        {
            viewModel.RenameBuffSetCommand.Execute(null);
            ManageSetButton.Flyout?.Hide();
        }
    }

    private async void DeleteBuffSet_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainWindowViewModel viewModel
            || viewModel.SelectedBuffSet is not { } selected
            || !viewModel.DeleteBuffSetCommand.CanExecute(null)
            || TopLevel.GetTopLevel(this) is not Window owner
            || sender is not Button button)
        {
            return;
        }

        button.IsEnabled = false;
        try
        {
            if (await ConfirmDeletionAsync(owner, "zestaw buffów", selected.Name))
            {
                viewModel.DeleteBuffSetCommand.Execute(null);
                ManageSetButton.Flyout?.Hide();
            }
        }
        finally
        {
            button.IsEnabled = true;
        }
    }
}
