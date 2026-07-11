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

        if (DataContext is not PanelTool tool)
        {
            _builtViewType = null;
            host.Content = null;
            return;
        }

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
