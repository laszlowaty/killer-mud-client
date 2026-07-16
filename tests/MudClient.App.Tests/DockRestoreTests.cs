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

    [Fact]
    public void CreateLayout_UsesSingleCombinedRoomPanel()
    {
        var factory = CreateFactory(out _);

        var room = Assert.Single(factory.AllTools, tool => tool.Title?.Contains("Pokój") == true);
        Assert.Equal("RoomInfo", room.Id);
        Assert.Equal(typeof(MudClient.App.Views.Panels.RoomInfoPanelView), room.ViewType);
    }

    // Close every tool of the right-top dock one by one, then restore each. The dock
    // empties and Dock removes it partway through — the classic "sometimes doesn't work".
    [Fact]
    public void CloseAllThenRestoreAll_RightTop()
    {
        var factory = CreateFactory(out var layout);
        var ids = new[] { "CharInfo", "Condition", "Effects", "Buffs", "Group", "MemSpells" };

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

    // Dragging only performs Dock's regular local split next to the dock under the pointer.
    // It must never be reinterpreted by the app as an auto-hide edge command.
    [Fact]
    public void DragSplit_SnapsToTargetDockWithoutPinning()
    {
        var factory = CreateFactory(out var layout);
        var center = Assert.IsAssignableFrom<IDock>(FindIn(layout, "CenterPane"));

        factory.SplitToDock(center, GetTool(factory, "Notes"), DockOperation.Left);

        Assert.True(Visible(layout, "Notes"));
        Assert.Empty(layout.LeftPinnedDockables ?? Enumerable.Empty<IDockable>());
    }

    [Fact]
    public void PinToolToEdge_MovingPinnedTool_RemovesOldEdgeAndDoesNotDuplicate()
    {
        var factory = CreateFactory(out var layout);
        var tool = GetTool(factory, "Map");

        factory.PinToolToEdge(tool, Alignment.Top);
        factory.PinToolToEdge(tool, Alignment.Left);

        Assert.False(Visible(layout, "Map"));
        Assert.False(tool.CanDrag);
        Assert.DoesNotContain(layout.TopPinnedDockables ?? Enumerable.Empty<IDockable>(), d => d.Id == "Map");
        Assert.Single(layout.LeftPinnedDockables ?? Enumerable.Empty<IDockable>(), d => d.Id == "Map");
        Assert.DoesNotContain(layout.RightPinnedDockables ?? Enumerable.Empty<IDockable>(), d => d.Id == "Map");
        Assert.DoesNotContain(layout.BottomPinnedDockables ?? Enumerable.Empty<IDockable>(), d => d.Id == "Map");
    }

    [Fact]
    public void PinToolToEdge_WhenDockClearsOwners_ReattachesRememberedParent()
    {
        var factory = CreateFactory(out var layout);
        var tool = GetTool(factory, "Map");
        var originalOwner = Assert.IsType<ToolDock>(tool.Owner);
        factory.PinToolToEdge(tool, Alignment.Top);

        // Reproduce Dock 12 losing both references while the tool still remains in a pinned list.
        tool.Owner = null;
        tool.OriginalOwner = null;

        factory.PinToolToEdge(tool, Alignment.Right);

        Assert.DoesNotContain(layout.TopPinnedDockables ?? Enumerable.Empty<IDockable>(), d => d.Id == "Map");
        Assert.Single(layout.RightPinnedDockables ?? Enumerable.Empty<IDockable>(), d => d.Id == "Map");

        factory.Restore(tool);

        Assert.True(Visible(layout, "Map"));
        Assert.True(tool.CanDrag);
        Assert.Same(originalOwner, tool.Owner);
        Assert.DoesNotContain(layout.RightPinnedDockables ?? Enumerable.Empty<IDockable>(), d => d.Id == "Map");
    }

    [Fact]
    public void PinToolToEdge_HiddenTool_RestoresItDirectlyOnSelectedEdge()
    {
        var factory = CreateFactory(out var layout);
        var tool = GetTool(factory, "Gmcp");
        factory.CloseDockable(tool);

        factory.PinToolToEdge(tool, Alignment.Right);

        Assert.DoesNotContain(tool, factory.HiddenTools);
        Assert.Single(layout.RightPinnedDockables ?? Enumerable.Empty<IDockable>(), d => d.Id == "Gmcp");
    }

    [Fact]
    public void ReturnToLayoutCommand_UnpinsIntoRememberedDockAndRestoresDragging()
    {
        var factory = CreateFactory(out var layout);
        var tool = GetTool(factory, "Map");
        var originalOwner = Assert.IsType<ToolDock>(tool.Owner);
        factory.PinToolToEdge(tool, Alignment.Bottom);

        Assert.True(tool.ReturnToLayoutCommand.CanExecute(null));
        tool.ReturnToLayoutCommand.Execute(null);

        Assert.True(Visible(layout, "Map"));
        Assert.Same(originalOwner, tool.Owner);
        Assert.True(tool.CanDrag);
        Assert.False(tool.ReturnToLayoutCommand.CanExecute(null));
        Assert.DoesNotContain(layout.BottomPinnedDockables ?? Enumerable.Empty<IDockable>(), d => d.Id == "Map");
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

    [Fact]
    public void PinnedSideTab_WithEmptyOwnerIdFromOlderPreset_RestoresOnRecordedEdge()
    {
        var factory1 = CreateFactory(out var layout1);
        factory1.PinToolToEdge(GetTool(factory1, "Gmcp"), Alignment.Left);
        var snapshot = factory1.Snapshot(layout1);
        var pin = Assert.Single(snapshot.PinnedTools);

        // Real drag/drop layouts contain anonymous ProportionalDocks and older snapshots stored
        // OwnerId="" for an anonymous ToolDock. The generic id lookup then selected this wrong
        // container and PinDockable silently failed.
        snapshot.Root!.Id = string.Empty;
        pin.OwnerId = string.Empty;

        var factory2 = CreateFactory(out var layout2);
        Assert.True(factory2.TryApplySnapshot(layout2, snapshot));

        Assert.Contains(
            layout2.LeftPinnedDockables ?? Enumerable.Empty<IDockable>(),
            dockable => dockable.Id == "Gmcp");
        Assert.DoesNotContain(factory2.AllTools.First(tool => tool.Id == "Gmcp"), factory2.HiddenTools);
    }

    [Theory]
    [InlineData(Alignment.Left)]
    [InlineData(Alignment.Right)]
    [InlineData(Alignment.Top)]
    [InlineData(Alignment.Bottom)]
    public void ExpandedPinnedPreview_RoundTripsThroughSnapshot(Alignment edge)
    {
        var factory1 = CreateFactory(out var layout1);
        var tool = GetTool(factory1, "Gmcp");
        factory1.PinToolToEdge(tool, edge);
        ((IFactory)factory1).TogglePreviewPinnedDockable(tool);

        var snapshot = factory1.Snapshot(layout1);
        var pin = Assert.Single(snapshot.PinnedTools);
        Assert.Equal(edge.ToString(), pin.Edge);

        var factory2 = CreateFactory(out var layout2);
        Assert.True(factory2.TryApplySnapshot(layout2, snapshot));

        var restoredEdge = edge switch
        {
            Alignment.Left => layout2.LeftPinnedDockables,
            Alignment.Right => layout2.RightPinnedDockables,
            Alignment.Top => layout2.TopPinnedDockables,
            _ => layout2.BottomPinnedDockables,
        };
        Assert.Contains(restoredEdge ?? Enumerable.Empty<IDockable>(), dockable => dockable.Id == "Gmcp");
    }

    [Fact]
    public void PinnedBottomTool_RestoresWhenSnapshotHasNoToolDock()
    {
        var factory = CreateFactory(out var layout);
        var pinnedId = "Gmcp";
        var snapshot = new DockLayoutSnapshot
        {
            Root = new DockNodeSnapshot { Kind = "Splitter", Id = "OnlySplitter" },
            HiddenToolIds = factory.AllTools
                .Select(tool => tool.Id!)
                .Where(id => id != pinnedId)
                .ToList(),
            PinnedTools =
            [
                new PinnedToolSnapshot
                {
                    Id = pinnedId,
                    OwnerId = null,
                    Edge = Alignment.Bottom.ToString(),
                },
            ],
        };

        Assert.True(factory.TryApplySnapshot(layout, snapshot));

        Assert.Contains(
            layout.BottomPinnedDockables ?? Enumerable.Empty<IDockable>(),
            dockable => dockable.Id == pinnedId);
        Assert.DoesNotContain(factory.AllTools.First(tool => tool.Id == pinnedId), factory.HiddenTools);
    }

    [Fact]
    public void Snapshot_WithNoVisibleRootChild_RemainsLoadable()
    {
        var factory1 = CreateFactory(out var layout1);
        layout1.VisibleDockables!.Clear();
        foreach (var tool in factory1.AllTools)
        {
            factory1.HiddenTools.Add(tool);
        }

        var snapshot = factory1.Snapshot(layout1);
        Assert.NotNull(snapshot.Root);

        var factory2 = CreateFactory(out var layout2);
        Assert.True(factory2.TryApplySnapshot(layout2, snapshot));
        Assert.Equal(factory2.AllTools.Count, factory2.HiddenTools.Count);
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
    public void ReclaimUnrenderedPinnedTools_MovesGhostPinToHiddenTools()
    {
        var factory = CreateFactory(out var layout);
        var tool = GetTool(factory, "Gmcp");
        factory.PinToolToEdge(tool, Alignment.Top);

        factory.ReclaimUnrenderedPinnedTools(layout, Array.Empty<PanelTool>());

        Assert.Contains(tool, factory.HiddenTools);
        Assert.DoesNotContain(
            layout.TopPinnedDockables ?? Enumerable.Empty<IDockable>(),
            dockable => ReferenceEquals(dockable, tool));

        factory.RestoreToTopEdge(tool);
        Assert.DoesNotContain(tool, factory.HiddenTools);
        Assert.Contains(
            layout.TopPinnedDockables ?? Enumerable.Empty<IDockable>(),
            dockable => ReferenceEquals(dockable, tool));
    }

    [Fact]
    public void ReclaimUnrenderedPinnedTools_LeavesRenderedPinAlone()
    {
        var factory = CreateFactory(out var layout);
        var tool = GetTool(factory, "Gmcp");
        factory.PinToolToEdge(tool, Alignment.Right);

        factory.ReclaimUnrenderedPinnedTools(layout, new[] { tool });

        Assert.Empty(factory.HiddenTools);
        Assert.Contains(
            layout.RightPinnedDockables ?? Enumerable.Empty<IDockable>(),
            dockable => ReferenceEquals(dockable, tool));
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
