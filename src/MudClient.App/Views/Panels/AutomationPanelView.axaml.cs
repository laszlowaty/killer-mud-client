using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using MudClient.App.Models;
using MudClient.App.ViewModels;

namespace MudClient.App.Views.Panels;

public sealed partial class AutomationPanelView : UserControl
{
    private MainWindowViewModel? _viewModel;

    public AutomationPanelView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => _viewModel = DataContext as MainWindowViewModel;
    }

    private void EditTimer_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is TimerEntry timer &&
            _viewModel is not null)
        {
            _viewModel.EditTimerCommand.Execute(timer);
        }
    }

    private void ToggleTimer_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is ToggleButton button &&
            button.DataContext is TimerEntry timer &&
            _viewModel is not null)
        {
            _viewModel.ToggleTimerCommand.Execute(timer);
        }
    }

    private void DeleteTimer_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is TimerEntry timer &&
            _viewModel is not null)
        {
            _viewModel.DeleteTimerCommand.Execute(timer);
        }
    }

    private void ToggleRule_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is ToggleButton button &&
            button.DataContext is AutomationRuleEntry rule &&
            _viewModel is not null)
        {
            _viewModel.ToggleRuleCommand.Execute(rule);
        }
    }

    private void DeleteRule_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is AutomationRuleEntry rule &&
            _viewModel is not null)
        {
            _viewModel.DeleteRuleCommand.Execute(rule);
        }
    }

    private void EditRule_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is AutomationRuleEntry rule &&
            _viewModel is not null)
        {
            _viewModel.EditRuleCommand.Execute(rule);
        }
    }
}
