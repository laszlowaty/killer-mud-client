using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using MudClient.App.Docking;

namespace MudClient.App.Views.Panels;

/// <summary>
/// The single generic, reusable widget that hosts every dockable panel.
/// Given a <see cref="PanelTool"/> as its DataContext, it instantiates
/// <c>PanelTool.ViewType</c> and binds it to <c>PanelTool.Context</c>.
/// </summary>
public partial class PanelToolView : UserControl
{
    private Type? _builtViewType;

    public PanelToolView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Rebuild();
    }

    private void Rebuild()
    {
        var host = this.FindControl<ContentControl>("Host")!;
        var settingsButton = this.FindControl<Button>("SettingsButton")!;

        if (DataContext is not PanelTool tool)
        {
            Classes.Set("mud-configurable-widget", false);
            _builtViewType = null;
            host.Content = null;
            settingsButton.IsVisible = false;
            return;
        }

        Classes.Set("mud-configurable-widget",
            !string.Equals(tool.Id, "Terminal", StringComparison.Ordinal));

        // Terminal has no per-panel settings; Map already shows its own settings button in this
        // same corner (see MapPanelView) with its menu content wired in, so the generic
        // placeholder here would just sit on top of it.
        settingsButton.IsVisible =
            !string.Equals(tool.Id, "Terminal", StringComparison.Ordinal)
            && !string.Equals(tool.Id, "Map", StringComparison.Ordinal);

        if (host.Content is Control existing && _builtViewType == tool.ViewType)
        {
            existing.DataContext = tool.Context;
            return;
        }

        var view = (Control)Activator.CreateInstance(tool.ViewType)!;
        view.DataContext = tool.Context;
        _builtViewType = tool.ViewType;
        host.Content = view;
    }
}
