using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using MudClient.App.Docking;

namespace MudClient.App.Tests;

public sealed class TerminalOverlayTests
{
    private static MudDockFactory CreateFactory(out IRootDock layout)
    {
        var factory = new MudDockFactory(new object(), new object());
        layout = factory.CreateLayout();
        factory.InitLayout(layout);
        return factory;
    }

    private static PanelTool GetTool(MudDockFactory f, string id) => f.AllTools.First(t => t.Id == id);

    private static IEnumerable<PanelTool> PanelsIn(IDockable d) => d switch
    {
        PanelTool t => new[] { t },
        IDock dock => (dock.VisibleDockables ?? Enumerable.Empty<IDockable>()).SelectMany(PanelsIn),
        _ => Enumerable.Empty<PanelTool>(),
    };

    private static bool Visible(IRootDock layout, string id) =>
        (layout.VisibleDockables ?? Enumerable.Empty<IDockable>()).SelectMany(PanelsIn).Any(p => p.Id == id);

    [Fact]
    public void PinToolAsOverlay_RemovesToolFromDockTree_AndSetsOverlayTool()
    {
        var factory = CreateFactory(out var layout);
        var tool = GetTool(factory, "Gmcp");

        factory.PinToolAsOverlay(tool);

        Assert.Same(tool, factory.OverlayTool);
        Assert.False(Visible(layout, "Gmcp"));
        Assert.DoesNotContain(tool, factory.HiddenTools);
    }

    [Fact]
    public void PinToolAsOverlay_Terminal_IsNoOp()
    {
        var factory = CreateFactory(out var layout);
        var terminal = GetTool(factory, "Terminal");

        factory.PinToolAsOverlay(terminal);

        Assert.Null(factory.OverlayTool);
        Assert.True(Visible(layout, "Terminal"));
    }

    [Fact]
    public void CanPinAsOverlay_Terminal_IsFalse()
    {
        var factory = CreateFactory(out _);
        var terminal = GetTool(factory, "Terminal");
        var gmcp = GetTool(factory, "Gmcp");

        Assert.False(terminal.PinAsOverlayCommand.CanExecute(null));
        Assert.True(gmcp.PinAsOverlayCommand.CanExecute(null));
    }

    [Fact]
    public void PinToolAsOverlay_ReplacesPreviousOverlay_ReturningItToItsRememberedOwner()
    {
        var factory = CreateFactory(out var layout);
        var first = GetTool(factory, "Gmcp");
        var firstOwner = Assert.IsType<ToolDock>(first.Owner);
        var second = GetTool(factory, "Notes");

        factory.PinToolAsOverlay(first);
        factory.PinToolAsOverlay(second);

        Assert.Same(second, factory.OverlayTool);
        Assert.True(Visible(layout, "Gmcp"));
        Assert.Same(firstOwner, first.Owner);
        Assert.False(Visible(layout, "Notes"));
    }

    [Fact]
    public void ReturnOverlayToLayout_RestoresToolToRememberedOwner()
    {
        var factory = CreateFactory(out var layout);
        var tool = GetTool(factory, "Gmcp");
        var originalOwner = Assert.IsType<ToolDock>(tool.Owner);

        factory.PinToolAsOverlay(tool);
        factory.ReturnOverlayToLayout(tool);

        Assert.Null(factory.OverlayTool);
        Assert.True(Visible(layout, "Gmcp"));
        Assert.Same(originalOwner, tool.Owner);
    }

    [Fact]
    public void ReturnToLayoutCommand_UnoverlaysAnOverlaidTool()
    {
        var factory = CreateFactory(out var layout);
        var tool = GetTool(factory, "Gmcp");

        factory.PinToolAsOverlay(tool);
        Assert.True(tool.ReturnToLayoutCommand.CanExecute(null));

        tool.ReturnToLayoutCommand.Execute(null);

        Assert.Null(factory.OverlayTool);
        Assert.True(Visible(layout, "Gmcp"));
    }

    [Fact]
    public void ReturnToLayoutCommand_StillUnpinsEdgePinnedTool_WhenNothingIsOverlaid()
    {
        var factory = CreateFactory(out var layout);
        var tool = GetTool(factory, "Map");
        factory.PinToolToEdge(tool, Alignment.Top);

        Assert.True(tool.ReturnToLayoutCommand.CanExecute(null));
        tool.ReturnToLayoutCommand.Execute(null);

        Assert.True(Visible(layout, "Map"));
        Assert.DoesNotContain(layout.TopPinnedDockables ?? Enumerable.Empty<IDockable>(), d => d.Id == "Map");
    }

    [Fact]
    public void Snapshot_WhileToolIsOverlaid_StillAccountsForItAtItsHomeToolDock()
    {
        var factory = CreateFactory(out var layout);
        var tool = GetTool(factory, "Gmcp");
        var homeOwnerId = Assert.IsType<ToolDock>(tool.Owner).Id;

        factory.PinToolAsOverlay(tool);
        var snapshot = factory.Snapshot(layout);

        var known = factory.AllTools.Select(t => t.Id!).ToHashSet();
        var referenced = new HashSet<string>();
        CollectPanelIds(snapshot.Root!, referenced);
        var hidden = new HashSet<string>(snapshot.HiddenToolIds);
        var pinned = new HashSet<string>(snapshot.PinnedTools.Select(p => p.Id));

        // The consistency check TryApplySnapshot performs: every known tool must appear exactly
        // once across the visible tree / hidden / pinned buckets. An overlaid tool that fell out
        // of all three would fail this and reject the whole snapshot (see MudDockFactory.Snapshot).
        Assert.True(new HashSet<string>(referenced.Union(hidden).Union(pinned)).SetEquals(known));
        Assert.Contains("Gmcp", referenced);

        var toolDockNode = FindToolDockNodeContaining(snapshot.Root!, "Gmcp");
        Assert.NotNull(toolDockNode);
        Assert.Equal(homeOwnerId, toolDockNode!.Id);
    }

    [Fact]
    public void SaveThenLoadSnapshot_WithActiveOverlay_RoundTrips()
    {
        var factory1 = CreateFactory(out var layout1);
        factory1.PinToolAsOverlay(GetTool(factory1, "Gmcp"));

        var snapshot = factory1.Snapshot(layout1);

        var factory2 = CreateFactory(out var layout2);
        Assert.True(factory2.TryApplySnapshot(layout2, snapshot));

        // The preset snapshot describes the static shape as if nothing were overlaid — the
        // overlay itself is reapplied afterward from AppSettings (MainWindowViewModel).
        Assert.True(Visible(layout2, "Gmcp"));
        Assert.Null(factory2.OverlayTool);
    }

    private static void CollectPanelIds(DockNodeSnapshot node, HashSet<string> ids)
    {
        if (node.Kind == "Panel" && node.Id is not null)
        {
            ids.Add(node.Id);
        }

        foreach (var child in node.Children)
        {
            CollectPanelIds(child, ids);
        }
    }

    private static DockNodeSnapshot? FindToolDockNodeContaining(DockNodeSnapshot node, string panelId)
    {
        if (node.Kind == "ToolDock" && node.Children.Any(c => c.Kind == "Panel" && c.Id == panelId))
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            if (FindToolDockNodeContaining(child, panelId) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
