using Dock.Model.Controls;
using Dock.Model.Core;
using MudClient.App.Docking;
using MudClient.App.Services;

namespace MudClient.App.Tests;

/// <summary>
/// Persistence tests for the dock layout: JSON round-trips through
/// <see cref="DockLayoutService"/> and validation of stale snapshots.
/// </summary>
public sealed class DockLayoutPersistenceTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("dock-layout-tests-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static MudDockFactory CreateFactory(out IRootDock layout)
    {
        var factory = new MudDockFactory(new object(), new object());
        layout = factory.CreateLayout();
        factory.InitLayout(layout);
        return factory;
    }

    private static PanelTool GetTool(MudDockFactory factory, string id) =>
        factory.AllTools.First(t => t.Id == id);

    private static IEnumerable<PanelTool> PanelsIn(IDockable dockable) => dockable switch
    {
        PanelTool tool => new[] { tool },
        IDock dock => (dock.VisibleDockables ?? Enumerable.Empty<IDockable>()).SelectMany(PanelsIn),
        _ => Enumerable.Empty<PanelTool>(),
    };

    private static IDockable? FindById(IDock dock, string id) =>
        (dock.VisibleDockables ?? Enumerable.Empty<IDockable>())
        .Select(child => child.Id == id ? child : child is IDock nested ? FindById(nested, id) : null)
        .FirstOrDefault(found => found is not null);

    [Fact]
    public void Snapshot_RoundTripsThroughSaveAndLoad()
    {
        var factory1 = CreateFactory(out var layout1);
        factory1.CloseDockable(GetTool(factory1, "Gmcp"));

        var service = new DockLayoutService(_tempDir);
        service.Save(factory1.Snapshot(layout1));
        var loaded = service.Load();
        Assert.NotNull(loaded);
        Assert.Equal(new[] { "Gmcp" }, loaded!.HiddenToolIds);

        var factory2 = CreateFactory(out var layout2);
        Assert.True(factory2.TryApplySnapshot(layout2, loaded));

        Assert.Equal("Gmcp", Assert.Single(factory2.HiddenTools).Id);
        var panels = (layout2.VisibleDockables ?? Enumerable.Empty<IDockable>()).SelectMany(PanelsIn).ToList();
        Assert.DoesNotContain(panels, p => p.Id == "Gmcp");
        Assert.Contains(panels, p => p.Id == "Terminal");
    }

    [Fact]
    public void Restore_ReaddsClosedPanelToItsPreviousDock()
    {
        var factory = CreateFactory(out var layout);
        var tool = GetTool(factory, "Gmcp");

        factory.CloseDockable(tool);
        Assert.Contains(tool, factory.HiddenTools);
        Assert.DoesNotContain(PanelsIn(layout), panel => panel.Id == tool.Id);

        factory.Restore(tool);

        Assert.DoesNotContain(tool, factory.HiddenTools);
        Assert.Contains(PanelsIn(layout), panel => panel.Id == tool.Id);
    }

    [Fact]
    public void Restore_ReaddsPanelWhenClosingItAlsoRemovedItsEmptyParent()
    {
        var factory = CreateFactory(out var layout);
        var terminal = GetTool(factory, "Terminal");

        factory.CloseDockable(terminal);
        factory.Restore(terminal);

        Assert.DoesNotContain(terminal, factory.HiddenTools);
        Assert.Contains(PanelsIn(layout), panel => panel.Id == terminal.Id);
    }

    [Fact]
    public void ClosingParent_HidesAndRestoresItsNestedPanels()
    {
        var factory = CreateFactory(out var layout);
        var parent = Assert.IsAssignableFrom<IDockable>(FindById(layout, "RightTopPane"));
        var nestedIds = PanelsIn(parent).Select(panel => panel.Id).ToHashSet();

        factory.CloseDockable(parent);
        Assert.All(nestedIds, id => Assert.Contains(factory.HiddenTools, panel => panel.Id == id));

        foreach (var panel in factory.HiddenTools.Where(panel => nestedIds.Contains(panel.Id)).ToList())
        {
            factory.Restore(panel);
        }

        Assert.All(nestedIds, id => Assert.Contains(PanelsIn(layout), panel => panel.Id == id));
        Assert.DoesNotContain(factory.HiddenTools, panel => nestedIds.Contains(panel.Id));
    }

    [Fact]
    public void Snapshot_RoundTripsAutoHiddenPinnedTool()
    {
        var factory1 = CreateFactory(out var layout1);
        var gmcp = GetTool(factory1, "Gmcp");

        // "Auto hide" a widget: Dock moves it out of the visible tree into a pinned edge.
        factory1.PinDockable(gmcp);
        Assert.DoesNotContain(PanelsIn(layout1), panel => panel.Id == "Gmcp");

        var snapshot = factory1.Snapshot(layout1);
        var pin = Assert.Single(snapshot.PinnedTools);
        Assert.Equal("Gmcp", pin.Id);

        var service = new DockLayoutService(_tempDir);
        service.Save(snapshot);
        var loaded = service.Load();
        Assert.NotNull(loaded);

        var factory2 = CreateFactory(out var layout2);
        Assert.True(factory2.TryApplySnapshot(layout2, loaded!));

        // The tool stays auto-hidden (not in the visible tree) after restore.
        Assert.DoesNotContain(PanelsIn(layout2), panel => panel.Id == "Gmcp");
        var pinnedAgain = factory2.Snapshot(layout2).PinnedTools;
        Assert.Contains(pinnedAgain, p => p.Id == "Gmcp");
    }

    [Fact]
    public void TryApplySnapshot_RejectsSnapshotMissingKnownPanels()
    {
        var factory1 = CreateFactory(out var layout1);
        var snapshot = factory1.Snapshot(layout1);

        // Simulate a stale file from an older app version: one panel unaccounted for.
        RemovePanel(snapshot.Root!, "Notes");

        var factory2 = CreateFactory(out var layout2);
        Assert.False(factory2.TryApplySnapshot(layout2, snapshot));
    }

    [Fact]
    public void TryApplySnapshot_RejectsPanelBothVisibleAndHidden()
    {
        var factory1 = CreateFactory(out var layout1);
        var snapshot = factory1.Snapshot(layout1);
        snapshot.HiddenToolIds.Add("Notes");

        var factory2 = CreateFactory(out var layout2);
        Assert.False(factory2.TryApplySnapshot(layout2, snapshot));
    }

    private static void RemovePanel(DockNodeSnapshot node, string id)
    {
        node.Children.RemoveAll(c => c.Kind == "Panel" && c.Id == id);
        foreach (var child in node.Children)
        {
            RemovePanel(child, id);
        }
    }
}
