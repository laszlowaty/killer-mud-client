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
}
