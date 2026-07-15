using Avalonia.Controls;
using Avalonia.Input;
using MudClient.App.ViewModels;

namespace MudClient.App.Views.Panels;

public sealed partial class BuffsPanelView : UserControl
{
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
        }
    }
}
