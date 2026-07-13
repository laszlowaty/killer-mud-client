using CommunityToolkit.Mvvm.Input;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;

namespace MudClient.App.Docking;

/// <summary>
/// Generic dockable tool: hosts an arbitrary panel view (<see cref="ViewType"/>)
/// whose DataContext is <see cref="Context"/>. One instance of this class backs
/// every panel in the app (map, room info, terminal, character, automation, ...).
/// </summary>
public sealed class PanelTool : Tool
{
    public required Type ViewType { get; init; }

    /// <summary>
    /// Set by <see cref="MudDockFactory"/>; moves this tool into a collapsed tab on the
    /// requested edge. Used by the explicit edge choices in panel menus.
    /// </summary>
    internal Action<Alignment>? PinToEdge { get; set; }

    internal Action? ReturnToLayout { get; set; }

    internal Func<bool>? CanReturnToLayout { get; set; }

    public PanelTool()
    {
        PinLeftCommand = new RelayCommand(() => PinToEdge?.Invoke(Alignment.Left));
        PinRightCommand = new RelayCommand(() => PinToEdge?.Invoke(Alignment.Right));
        PinTopCommand = new RelayCommand(() => PinToEdge?.Invoke(Alignment.Top));
        PinBottomCommand = new RelayCommand(() => PinToEdge?.Invoke(Alignment.Bottom));
        ReturnToLayoutCommand = new RelayCommand(
            () => ReturnToLayout?.Invoke(),
            () => CanReturnToLayout?.Invoke() == true);
    }

    public IRelayCommand PinLeftCommand { get; }

    public IRelayCommand PinRightCommand { get; }

    public IRelayCommand PinTopCommand { get; }

    public IRelayCommand PinBottomCommand { get; }

    public IRelayCommand ReturnToLayoutCommand { get; }

    internal void RefreshDockCommands() => ReturnToLayoutCommand.NotifyCanExecuteChanged();
}
