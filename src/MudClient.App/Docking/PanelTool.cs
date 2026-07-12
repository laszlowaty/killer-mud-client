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
    /// Set by <see cref="MudDockFactory"/>; collapses this tool into a tab on the requested
    /// screen edge. Drives the "Schowaj z…" context-menu commands below — the same action
    /// as dragging the panel onto that window edge.
    /// </summary>
    internal Action<Alignment>? PinToEdge { get; set; }

    public PanelTool()
    {
        PinLeftCommand = new RelayCommand(() => PinToEdge?.Invoke(Alignment.Left));
        PinRightCommand = new RelayCommand(() => PinToEdge?.Invoke(Alignment.Right));
        PinTopCommand = new RelayCommand(() => PinToEdge?.Invoke(Alignment.Top));
        PinBottomCommand = new RelayCommand(() => PinToEdge?.Invoke(Alignment.Bottom));
    }

    public IRelayCommand PinLeftCommand { get; }

    public IRelayCommand PinRightCommand { get; }

    public IRelayCommand PinTopCommand { get; }

    public IRelayCommand PinBottomCommand { get; }
}
