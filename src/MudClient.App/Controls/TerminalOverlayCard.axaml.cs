using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using MudClient.App.Docking;
using MudClient.App.Models;
using MudClient.App.ViewModels;

namespace MudClient.App.Controls;

/// <summary>
/// One translucent card rendering a panel pinned as a Terminal overlay in TRANSPARENCY mode. Purely
/// presentational — see the .axaml file for the fixed right-aligned stacking that positions these,
/// and <see cref="MudClient.App.Docking.MudDockFactory.IsTransparencyLayout"/> for why there is no
/// drag/resize here (deliberately not combined with free-form docking).
/// </summary>
public sealed partial class TerminalOverlayCard : UserControl
{
    /// <summary>Overridable in tests — see AutomationDeletionConfirmationUiTests.</summary>
    internal Func<Window, string, string, Task<bool>> ConfirmDeletionAsync { get; set; } =
        DeleteConfirmationDialog.ShowAsync;

    /// <summary>Set by <see cref="TerminalOverlayHost"/> alongside <see cref="UserControl.DataContext"/>
    /// (which stays the plain <see cref="PanelTool"/> so every existing binding in this file keeps
    /// working unchanged) — this is the one place the card's ▲▼◀▶ move buttons reach the
    /// move commands from, via a `$parent[TerminalOverlayCard].Overlay...` binding. Set once at
    /// construction and never changed afterward, so a plain CLR property is enough.</summary>
    public TerminalOverlayViewModel? Overlay { get; set; }

    public TerminalOverlayCard()
    {
        InitializeComponent();
    }

    private void MoveUp_OnClick(object? sender, RoutedEventArgs eventArgs) => Overlay?.MoveUpCommand.Execute(null);

    private void MoveDown_OnClick(object? sender, RoutedEventArgs eventArgs) => Overlay?.MoveDownCommand.Execute(null);

    private void MoveLeft_OnClick(object? sender, RoutedEventArgs eventArgs) => Overlay?.MoveLeftCommand.Execute(null);

    private void MoveRight_OnClick(object? sender, RoutedEventArgs eventArgs) => Overlay?.MoveRightCommand.Execute(null);

    // ========================================================================
    // Map's Autowalk locations / death marks — this card's DataContext is the
    // PanelTool itself, whose Context is the MapViewModel instance for the Map
    // tool specifically (see MapPanelView.axaml.cs for the non-overlay copy of
    // these same handlers).
    // ========================================================================

    private void GoToLocation_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is AutowalkLocation location &&
            DataContext is PanelTool { Context: MapViewModel { MainViewModel: { } mainViewModel } })
        {
            mainViewModel.GoToLocationCommand.Execute(location);
        }
    }

    private void GoToDeath_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is DeathMarkEntry entry &&
            DataContext is PanelTool { Context: MapViewModel { MainViewModel: { } mainViewModel } })
        {
            mainViewModel.GoToDeathCommand.Execute(entry);
        }
    }

    private void DeleteDeath_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is DeathMarkEntry entry &&
            DataContext is PanelTool { Context: MapViewModel { MainViewModel: { } mainViewModel } })
        {
            mainViewModel.DeleteDeathCommand.Execute(entry);
        }
    }

    private async void DeleteLocation_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is AutowalkLocation location &&
            DataContext is PanelTool { Context: MapViewModel { MainViewModel: { } mainViewModel } })
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
                    mainViewModel.DeleteLocationCommand.Execute(location);
                }
            }
            finally
            {
                button.IsEnabled = true;
            }
        }
    }

    // ========================================================================
    // Mem's settings — buff-set management moved here from the former Buffs
    // panel. DataContext is the PanelTool itself; Context is MainWindowViewModel
    // (shared with most other panels, unlike Map's own dedicated ViewModel) —
    // see PanelToolView.axaml.cs for the non-overlay copy of these same
    // handlers.
    // ========================================================================

    private void NewBuffSetBox_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key is not (Key.Enter or Key.Return)
            || DataContext is not PanelTool { Context: MainWindowViewModel viewModel })
        {
            return;
        }

        eventArgs.Handled = true;
        if (viewModel.CreateBuffSetCommand.CanExecute(null))
        {
            viewModel.CreateBuffSetCommand.Execute(null);
        }
    }

    private void CreateBuffSet_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is PanelTool { Context: MainWindowViewModel viewModel }
            && viewModel.CreateBuffSetCommand.CanExecute(null))
        {
            viewModel.CreateBuffSetCommand.Execute(null);
        }
    }

    private void BuffSetNameBox_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key is not (Key.Enter or Key.Return)
            || DataContext is not PanelTool { Context: MainWindowViewModel viewModel })
        {
            return;
        }

        eventArgs.Handled = true;
        if (viewModel.RenameBuffSetCommand.CanExecute(null))
        {
            viewModel.RenameBuffSetCommand.Execute(null);
        }
    }

    private void RenameBuffSet_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is PanelTool { Context: MainWindowViewModel viewModel }
            && viewModel.RenameBuffSetCommand.CanExecute(null))
        {
            viewModel.RenameBuffSetCommand.Execute(null);
        }
    }

    private async void DeleteBuffSet_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not PanelTool { Context: MainWindowViewModel viewModel }
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
            }
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    private void NewBuffBox_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key is not (Key.Enter or Key.Return)
            || DataContext is not PanelTool { Context: MainWindowViewModel viewModel })
        {
            return;
        }

        eventArgs.Handled = true;
        if (viewModel.AddBuffCommand.CanExecute(null))
        {
            viewModel.AddBuffCommand.Execute(null);
        }
    }

    private void AddBuff_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is PanelTool { Context: MainWindowViewModel viewModel }
            && viewModel.AddBuffCommand.CanExecute(null))
        {
            viewModel.AddBuffCommand.Execute(null);
        }
    }
}
