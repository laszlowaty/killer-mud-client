using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using MudClient.App.Docking;

namespace MudClient.App.Tests;

public sealed class DockRestoreTests
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

    // Close every tool of the right-top dock one by one, then restore each. The dock
    // empties and Dock removes it partway through — the classic "sometimes doesn't work".
    [Fact]
    public void CloseAllThenRestoreAll_RightTop()
    {
        var factory = CreateFactory(out var layout);
        var ids = new[] { "CharInfo", "Condition", "Effects", "Buffs", "RoomPeople", "Group", "MemSpells" };

        foreach (var id in ids)
        {
            factory.CloseDockable(GetTool(factory, id));
        }

        foreach (var id in ids)
        {
            factory.Restore(GetTool(factory, id));
        }

        var missing = ids.Where(id => !Visible(layout, id)).ToList();
        Assert.True(missing.Count == 0, "Not restored: " + string.Join(", ", missing));
    }

    // Close tools across DIFFERENT docks interleaved, then restore in a different order.
    [Fact]
    public void CloseInterleavedThenRestore()
    {
        var factory = CreateFactory(out var layout);
        var ids = new[] { "Gmcp", "Terminal", "Map", "Settings", "Notes" };

        foreach (var id in ids)
        {
            factory.CloseDockable(GetTool(factory, id));
        }

        foreach (var id in ids.Reverse())
        {
            factory.Restore(GetTool(factory, id));
        }

        var missing = ids.Where(id => !Visible(layout, id)).ToList();
        Assert.True(missing.Count == 0, "Not restored: " + string.Join(", ", missing));
    }

    [Fact]
    public void ShowTool_SelectsRequestedTab()
    {
        var factory = CreateFactory(out _);
        var map = GetTool(factory, "Map");
        var roomInfo = GetTool(factory, "RoomInfo");
        var owner = Assert.IsType<ToolDock>(map.Owner);
        factory.SetActiveDockable(roomInfo);

        var shown = factory.ShowTool("Map");

        Assert.True(shown);
        Assert.Same(map, owner.ActiveDockable);
    }

    // Dragging a panel onto the main window's outer edge (global dock → SplitToDock on the
    // root-level target) must become a collapsed tab on that edge, not a layout split.
    [Theory]
    [InlineData(DockOperation.Left)]
    [InlineData(DockOperation.Right)]
    [InlineData(DockOperation.Top)]
    [InlineData(DockOperation.Bottom)]
    public void EdgeDrop_BecomesPinnedTabOnThatEdge(DockOperation operation)
    {
        var factory = CreateFactory(out var layout);
        var tool = GetTool(factory, "Gmcp");

        factory.SplitToDock(layout, tool, operation);

        Assert.False(Visible(layout, "Gmcp"));
        var pinned = operation switch
        {
            DockOperation.Left => layout.LeftPinnedDockables,
            DockOperation.Right => layout.RightPinnedDockables,
            DockOperation.Top => layout.TopPinnedDockables,
            _ => layout.BottomPinnedDockables,
        };
        Assert.Contains(pinned ?? Enumerable.Empty<IDockable>(), d => d.Id == "Gmcp");
    }

    // Real drags don't pass the bare PanelTool: Dock's DockService first moves the tool
    // into a fresh DETACHED ToolDock and passes that wrapper to SplitToDock. The wrapper
    // variant must pin just the same.
    [Theory]
    [InlineData(DockOperation.Left)]
    [InlineData(DockOperation.Bottom)]
    public void EdgeDrop_WrappedInDetachedToolDock_BecomesPinnedTab(DockOperation operation)
    {
        var factory = CreateFactory(out var layout);
        var main = Assert.IsAssignableFrom<IDock>(
            (layout.VisibleDockables ?? Enumerable.Empty<IDockable>()).First(d => d.Id == "MainLayout"));
        var tool = GetTool(factory, "Gmcp");

        // Mimic DockService.SplitToolDockable: new detached ToolDock + MoveDockable + SplitToDock.
        var wrapper = factory.CreateToolDock();
        wrapper.VisibleDockables = factory.CreateList<IDockable>();
        var sourceDock = Assert.IsAssignableFrom<IDock>(tool.Owner);
        factory.MoveDockable(sourceDock, wrapper, tool, null);
        factory.SplitToDock(main, wrapper, operation);

        Assert.False(Visible(layout, "Gmcp"));
        var pinned = operation == DockOperation.Left
            ? layout.LeftPinnedDockables
            : layout.BottomPinnedDockables;
        Assert.Contains(pinned ?? Enumerable.Empty<IDockable>(), d => d.Id == "Gmcp");
    }

    // The same drop on the layout's outermost proportional dock (what global docking
    // actually resolves to) must behave identically.
    [Fact]
    public void EdgeDrop_OnMainLayout_BecomesPinnedTab()
    {
        var factory = CreateFactory(out var layout);
        var main = Assert.IsAssignableFrom<IDock>(
            (layout.VisibleDockables ?? Enumerable.Empty<IDockable>()).First(d => d.Id == "MainLayout"));

        factory.SplitToDock(main, GetTool(factory, "Notes"), DockOperation.Right);

        Assert.False(Visible(layout, "Notes"));
        Assert.Contains(layout.RightPinnedDockables ?? Enumerable.Empty<IDockable>(), d => d.Id == "Notes");
    }

    // An inner split (target deeper in the tree, not touching that edge) keeps default split
    // behavior. CenterPane is the middle child, so it hugs neither the left nor right edge.
    [Fact]
    public void InnerSplit_StillSplits()
    {
        var factory = CreateFactory(out var layout);
        var center = Assert.IsAssignableFrom<IDock>(FindIn(layout, "CenterPane"));

        factory.SplitToDock(center, GetTool(factory, "Notes"), DockOperation.Left);

        Assert.True(Visible(layout, "Notes"));
        Assert.Empty(layout.LeftPinnedDockables ?? Enumerable.Empty<IDockable>());
    }

    // Near-miss: Dock 12 resolves a drop aimed at the window edge as a LOCAL split of the pane
    // hugging that edge (the more tabs there, the more often). That edge-most inner target must
    // pin, not split — RightTopPane sits flush against the right edge, so a rightward split pins.
    [Fact]
    public void EdgeMostInnerSplit_TowardOwnEdge_Pins()
    {
        var factory = CreateFactory(out var layout);
        var rightPane = Assert.IsAssignableFrom<IDock>(FindIn(layout, "RightTopPane"));

        factory.SplitToDock(rightPane, GetTool(factory, "Map"), DockOperation.Right);

        Assert.False(Visible(layout, "Map"));
        Assert.Contains(layout.RightPinnedDockables ?? Enumerable.Empty<IDockable>(), d => d.Id == "Map");
    }

    // Same edge-most pane, but the split points AWAY from its window edge: a genuine inner split
    // that must NOT be hijacked into a pin.
    [Fact]
    public void EdgeMostInnerSplit_TowardOppositeEdge_StillSplits()
    {
        var factory = CreateFactory(out var layout);
        var rightPane = Assert.IsAssignableFrom<IDock>(FindIn(layout, "RightTopPane"));

        factory.SplitToDock(rightPane, GetTool(factory, "Map"), DockOperation.Left);

        Assert.True(Visible(layout, "Map"));
        Assert.Empty(layout.LeftPinnedDockables ?? Enumerable.Empty<IDockable>());
    }

    // Per-edge tab round-trips through layout snapshots (auto-save and named presets).
    [Fact]
    public void PinnedEdgeTab_RoundTripsThroughSnapshot()
    {
        var factory1 = CreateFactory(out var layout1);
        factory1.PinToolToEdge(GetTool(factory1, "Gmcp"), Alignment.Top);

        var snapshot = factory1.Snapshot(layout1);
        Assert.Equal("Top", Assert.Single(snapshot.PinnedTools).Edge);

        var factory2 = CreateFactory(out var layout2);
        Assert.True(factory2.TryApplySnapshot(layout2, snapshot));

        Assert.Contains(layout2.TopPinnedDockables ?? Enumerable.Empty<IDockable>(), d => d.Id == "Gmcp");
        Assert.Equal("Top", Assert.Single(factory2.Snapshot(layout2).PinnedTools).Edge);
    }

    private static IDockable? FindIn(IDock dock, string id) =>
        (dock.VisibleDockables ?? Enumerable.Empty<IDockable>())
        .Select(child => child.Id == id ? child : child is IDock nested ? FindIn(nested, id) : null)
        .FirstOrDefault(found => found is not null);

    // A tool that fell out of every collection (Dock's drag pipeline can lose one when a
    // drag ends over non-dock chrome) must be reclaimed into HiddenTools for the restore menu.
    [Fact]
    public void ReclaimLostTools_MovesOrphanedToolToHidden()
    {
        var factory = CreateFactory(out var layout);
        var tool = GetTool(factory, "Gmcp");

        // Simulate the loss: rip the tool out of its dock without any close event.
        var owner = Assert.IsAssignableFrom<IDock>(tool.Owner);
        owner.VisibleDockables!.Remove(tool);

        factory.ReclaimLostTools(layout);

        Assert.Contains(tool, factory.HiddenTools);
        // Everything still visible or pinned must stay untouched.
        Assert.Single(factory.HiddenTools);

        // And the reclaimed panel restores fine.
        factory.Restore(tool);
        Assert.True(Visible(layout, "Gmcp"));
        Assert.Empty(factory.HiddenTools);
    }

    // Pinned tools are reachable — reclaim must NOT touch them.
    [Fact]
    public void ReclaimLostTools_LeavesPinnedToolsAlone()
    {
        var factory = CreateFactory(out var layout);
        factory.PinToolToEdge(GetTool(factory, "Gmcp"), Alignment.Top);

        factory.ReclaimLostTools(layout);

        Assert.Empty(factory.HiddenTools);
        Assert.Contains(layout.TopPinnedDockables ?? Enumerable.Empty<IDockable>(), d => d.Id == "Gmcp");
    }

    [Fact]
    public void ClosePinnedTool_ThenRestore_MakesItVisibleAgain()
    {
        var factory = CreateFactory(out var layout);
        var tool = GetTool(factory, "Gmcp");
        factory.PinToolToEdge(tool, Alignment.Right);

        factory.CloseDockable(tool);
        Assert.Contains(tool, factory.HiddenTools);

        factory.Restore(tool);

        Assert.True(Visible(layout, "Gmcp"));
        Assert.IsType<ToolDock>(tool.Owner);
        Assert.DoesNotContain(tool, factory.HiddenTools);
        Assert.DoesNotContain(
            layout.RightPinnedDockables ?? Enumerable.Empty<IDockable>(),
            dockable => ReferenceEquals(dockable, tool));
    }

    // Close EVERY tool so all ToolDocks get removed and the tree is empty, then restore.
    [Fact]
    public void CloseEverythingThenRestoreAll()
    {
        var factory = CreateFactory(out var layout);
        var ids = factory.AllTools.Select(t => t.Id!).ToList();

        foreach (var id in ids)
        {
            factory.CloseDockable(GetTool(factory, id));
        }
        foreach (var id in ids)
        {
            factory.Restore(GetTool(factory, id));
        }

        var missing = ids.Where(id => !Visible(layout, id)).ToList();
        Assert.True(missing.Count == 0, "Not restored: " + string.Join(", ", missing));
    }

    // Close the whole right-bottom dock's tools so the dock dies, restore ONE, then
    // close it again and restore again.
    [Fact]
    public void CloseRestoreCloseRestore_Cycle()
    {
        var factory = CreateFactory(out var layout);
        var ids = new[] { "Automation", "Autowalk", "Notes", "Gmcp", "Settings" };

        foreach (var id in ids)
        {
            factory.CloseDockable(GetTool(factory, id));
        }
        foreach (var id in ids)
        {
            factory.Restore(GetTool(factory, id));
        }
        Assert.True(ids.All(id => Visible(layout, id)), "first cycle failed");

        factory.CloseDockable(GetTool(factory, "Gmcp"));
        factory.Restore(GetTool(factory, "Gmcp"));
        Assert.True(Visible(layout, "Gmcp"), "second cycle failed");
    }
}
