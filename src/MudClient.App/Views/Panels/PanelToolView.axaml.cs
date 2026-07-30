using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using MudClient.App.Controls;
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

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Rebuild();
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

        // Terminal has no per-panel settings; Map already shows its own settings button (here or
        // in TerminalOverlayCard's title bar). A tool rendered inside a Terminal overlay card gets
        // its settings button from the card's own title bar instead (see TerminalOverlayCard.axaml)
        // — this generic one would otherwise duplicate it.
        settingsButton.IsVisible =
            !string.Equals(tool.Id, "Terminal", StringComparison.Ordinal)
            && !string.Equals(tool.Id, "Map", StringComparison.Ordinal)
            && this.FindAncestorOfType<TerminalOverlayCard>() is null;

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
