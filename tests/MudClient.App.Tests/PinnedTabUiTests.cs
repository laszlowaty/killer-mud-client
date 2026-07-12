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
[Collection(AvaloniaUiCollection.Name)]
public sealed class PinnedTabUiTests : IDisposable
{
    private readonly List<Window> _windows = new();

    // All UI test classes share one headless session, and windows left open accumulate in it —
    // enough lingering pinned strips occasionally perturb a restored tab's render. xUnit makes a
    // fresh test-class instance per test and disposes it afterwards, so closing this test's windows
    // here keeps each test's session state clean.
    public void Dispose()
    {
        foreach (var window in _windows)
        {
            window.Close();
        }

        Dispatcher.UIThread.RunJobs();
    }

    private MainWindow ShowWindow(MainWindowViewModel viewModel)
    {
        var window = new MainWindow { DataContext = viewModel, Width = 1400, Height = 900 };
        _windows.Add(window);
        window.Show();
        return window;
    }

    private static void Pump(Window window, int iterations = 10)
    {
        for (var i = 0; i < iterations; i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
    }

    /// <summary>
    /// Pumps layout/render until <paramref name="condition"/> holds or the cap is reached. A fixed
    /// pump count is a race under CPU contention — the pinned tab strip may need more render ticks
    /// to settle — so tests that assert on rendered visual bounds wait for the expected state.
    /// </summary>
    private static void PumpUntil(Window window, Func<bool> condition, int maxIterations = 150)
    {
        for (var i = 0; i < maxIterations && !condition(); i++)
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static List<ToolPinItemControl> RenderedPinItems(Window window) =>
        window.GetVisualDescendants().OfType<ToolPinItemControl>()
            .Where(p => p.IsEffectivelyVisible && p.Bounds is { Width: > 0, Height: > 0 })
            .ToList();

    [AvaloniaTheory]
    [InlineData(Alignment.Left)]
    [InlineData(Alignment.Right)]
    [InlineData(Alignment.Top)]
    [InlineData(Alignment.Bottom)]
    public void PinnedTool_ShowsSideTabInUi(Alignment edge)
    {
        var viewModel = new MainWindowViewModel();
        var window = ShowWindow(viewModel);
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
        PumpUntil(window, () => RenderedPinItems(window).Count > 0);

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

    [AvaloniaTheory]
    [InlineData(Alignment.Left)]
    [InlineData(Alignment.Right)]
    [InlineData(Alignment.Top)]
    [InlineData(Alignment.Bottom)]
    public void PinnedTool_Preview_OpensAtHalfDockArea(Alignment edge)
    {
        var viewModel = new MainWindowViewModel();
        var window = ShowWindow(viewModel);
        Pump(window);

        viewModel.ApplyLayoutCommand.Execute("DEFAULT");
        Pump(window);

        var dockControl = window.GetVisualDescendants().OfType<DockControl>().First();
        PumpUntil(window, () => dockControl.Bounds is { Width: > 0, Height: > 0 });
        Assert.True(dockControl.Bounds is { Width: > 0, Height: > 0 });

        var factory = Assert.IsType<MudDockFactory>(viewModel.Layout.Factory);
        var gmcp = factory.AllTools.First(t => t.Id == "Gmcp");
        factory.PinToolToEdge(gmcp, edge);
        Pump(window);

        gmcp.GetPinnedBounds(out _, out _, out var width, out var height);
        var horizontal = edge is Alignment.Left or Alignment.Right;
        var expected = (horizontal ? dockControl.Bounds.Width : dockControl.Bounds.Height) / 2.0;
        var actual = horizontal ? width : height;
        Assert.True(Math.Abs(actual - expected) <= 1.0,
            $"Pinned preview size for {edge} should be half the dock area (~{expected:F1}) but was {actual:F1}.");
    }

    // Mirrors a real user profile: several tools pinned to different edges, restored via
    // TryApplySnapshot on startup. Every pinned tab must actually render.
    [AvaloniaFact]
    public void RestoredSnapshotWithPinsOnAllEdges_RendersAllTabs()
    {
        var viewModel = new MainWindowViewModel();
        var window = ShowWindow(viewModel);
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
        PumpUntil(window, () => RenderedPinItems(window).Count >= 4);

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
        var window = ShowWindow(viewModel);
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
        PumpUntil(window, () => RenderedPinItems(window)
            .Any(control => ReferenceEquals(control.DataContext, gmcp)));

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
