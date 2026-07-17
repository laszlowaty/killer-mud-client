using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MudClient.App.Models;
using MudClient.App.ViewModels;

namespace MudClient.App.Views.Panels;

public sealed partial class GroupPanelView : UserControl
{
    public GroupPanelView()
    {
        InitializeComponent();
    }

    private void GroupMember_OnContextRequested(object? sender, ContextRequestedEventArgs eventArgs)
    {
        if (sender is Border { DataContext: GroupMember member })
        {
            GroupContextMenu.DataContext = member;
        }
    }

    private void GroupContextMenu_OnOpened(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetGroupContextMenuOpen(true);
        }
    }

    private void GroupContextMenu_OnClosed(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.SetGroupContextMenuOpen(false);
        }
    }
}
