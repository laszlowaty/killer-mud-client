using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Core;
using MudClient.App.Docking;
using MudClient.App.ViewModels;
using MudClient.App.Views;
using Xunit;

namespace MudClient.App.Tests;

/// <summary>
/// Headless UI checks that a tool pinned to a window edge actually renders as a
/// visible side tab (ToolPinItemControl) in the main window's visual tree.
/// </summary>
public sealed class PinnedTabUiTests
{
    private static void Pump(Window window)
    {
        for (var i = 0; i < 10; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
    }

    [AvaloniaTheory]
    [InlineData(Alignment.Left)]
    [InlineData(Alignment.Right)]
    [InlineData(Alignment.Top)]
    [InlineData(Alignment.Bottom)]
    public void PinnedTool_ShowsSideTabInUi(Alignment edge)
    {
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel, Width = 1400, Height = 900 };
        window.Show();
        Pump(window);

        // Sanity: the dock UI is up.
        var dockControl = window.GetVisualDescendants().OfType<DockControl>().FirstOrDefault();
        Assert.NotNull(dockControl);

        // The view model loads the real user profile's saved layout; reset to the built-in
        // default so this test is independent of whatever is pinned there right now.
        viewModel.ApplyLayoutCommand.Execute("DEFAULT");
        Pump(window);

        // Pin GMCP to the chosen edge, as an edge drag & drop would.
        var factory = Assert.IsType<MudDockFactory>(viewModel.Layout.Factory);
        var gmcp = factory.AllTools.First(t => t.Id == "Gmcp");
        factory.PinToolToEdge(gmcp, edge);
        Pump(window);

        var pinnedList = edge switch
        {
            Alignment.Left => viewModel.Layout.LeftPinnedDockables,
            Alignment.Right => viewModel.Layout.RightPinnedDockables,
            Alignment.Top => viewModel.Layout.TopPinnedDockables,
            _ => viewModel.Layout.BottomPinnedDockables,
        };
        Assert.Contains(pinnedList ?? Enumerable.Empty<IDockable>(), d => d.Id == "Gmcp");

        // The strip control for pinned tools must exist and contain a visible tab item.
        var pinItems = window.GetVisualDescendants().OfType<ToolPinItemControl>().ToList();
        Assert.True(pinItems.Count > 0,
            "No ToolPinItemControl in the visual tree — pinned tab strip is not rendered. " +
            "Visual types present: " + string.Join(", ", window.GetVisualDescendants()
                .Select(v => v.GetType().Name)
                .Where(n => n.Contains("Pin") || n.Contains("Root") || n.Contains("Dock"))
                .Distinct()));

        var tab = pinItems.First();
        Assert.True(tab.IsEffectivelyVisible, "Pinned tab exists but is not visible.");
        Assert.True(tab.Bounds.Width > 0 && tab.Bounds.Height > 0,
            $"Pinned tab has zero size: {tab.Bounds}.");
    }

    // Mirrors a real user profile: several tools pinned to different edges, restored via
    // TryApplySnapshot on startup. Every pinned tab must actually render.
    [AvaloniaFact]
    public void RestoredSnapshotWithPinsOnAllEdges_RendersAllTabs()
    {
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel, Width = 1400, Height = 900 };
        window.Show();
        Pump(window);

        viewModel.ApplyLayoutCommand.Execute("DEFAULT");
        Pump(window);

        var factory = Assert.IsType<MudDockFactory>(viewModel.Layout.Factory);
        factory.PinToolToEdge(factory.AllTools.First(t => t.Id == "Gmcp"), Alignment.Left);
        factory.PinToolToEdge(factory.AllTools.First(t => t.Id == "Autowalk"), Alignment.Right);
        factory.PinToolToEdge(factory.AllTools.First(t => t.Id == "Automation"), Alignment.Top);
        factory.PinToolToEdge(factory.AllTools.First(t => t.Id == "Notes"), Alignment.Bottom);
        var snapshot = factory.Snapshot(viewModel.Layout);
        Assert.Equal(4, snapshot.PinnedTools.Count);

        // Round-trip through the restore path used on startup.
        viewModel.ApplyLayoutCommand.Execute("DEFAULT");
        Pump(window);
        var factory2 = Assert.IsType<MudDockFactory>(viewModel.Layout.Factory);
        Assert.True(factory2.TryApplySnapshot(viewModel.Layout, snapshot));
        Pump(window);

        var pinItems = window.GetVisualDescendants().OfType<ToolPinItemControl>().ToList();
        Assert.True(pinItems.Count == 4,
            $"Expected 4 pinned tabs after restore, found {pinItems.Count}: " +
            string.Join(", ", pinItems.Select(p => (p.DataContext as PanelTool)?.Id)));
        Assert.All(pinItems, p => Assert.True(
            p.IsEffectivelyVisible && p.Bounds.Width > 0 && p.Bounds.Height > 0,
            $"Tab {(p.DataContext as PanelTool)?.Id} not rendered: vis={p.IsEffectivelyVisible} bounds={p.Bounds}"));
    }

    [AvaloniaFact]
    public void ClosedPinnedTool_RestoredFromPanelsMenu_RendersTopEdgeTab()
    {
        var viewModel = new MainWindowViewModel();
        var window = new MainWindow { DataContext = viewModel, Width = 1400, Height = 900 };
        window.Show();
        Pump(window);
        viewModel.ApplyLayoutCommand.Execute("DEFAULT");
        Pump(window);

        var factory = Assert.IsType<MudDockFactory>(viewModel.Layout.Factory);
        var gmcp = factory.AllTools.First(t => t.Id == "Gmcp");
        factory.PinToolToEdge(gmcp, Alignment.Right);
        Pump(window);

        factory.CloseDockable(gmcp);
        Pump(window);
        viewModel.RestorePanelCommand.Execute(gmcp);
        Pump(window);

        Assert.Contains(
            viewModel.Layout.TopPinnedDockables ?? Enumerable.Empty<IDockable>(),
            dockable => ReferenceEquals(dockable, gmcp));
        Assert.DoesNotContain(gmcp, factory.HiddenTools);

        var renderedTab = window.GetVisualDescendants()
            .OfType<ToolPinItemControl>()
            .FirstOrDefault(control => ReferenceEquals(control.DataContext, gmcp));
        Assert.NotNull(renderedTab);
        Assert.True(renderedTab.IsEffectivelyVisible
                    && renderedTab.Bounds.Width > 0
                    && renderedTab.Bounds.Height > 0);
    }
}
