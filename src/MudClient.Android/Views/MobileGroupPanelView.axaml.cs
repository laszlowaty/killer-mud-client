using Avalonia.Controls;
using Avalonia.Interactivity;
using MudClient.App.ViewModels;

namespace MudClient.Android.Views;

public sealed partial class MobileGroupPanelView : UserControl
{
    public MobileGroupPanelView()
    {
        InitializeComponent();
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
