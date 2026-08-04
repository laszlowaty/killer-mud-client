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
        // "Map" — unlike "Gmcp"/"Notes"/etc. it's still visible by default in DEFAULT (see
        // CreateLayout), so closing it actually adds a NEW hidden entry on top of the baseline
        // instead of being a no-op.
        factory1.CloseDockable(GetTool(factory1, "Map"));

        var service = new DockLayoutService(_tempDir);
        service.Save(factory1.Snapshot(layout1));
        var loaded = service.Load();
        Assert.NotNull(loaded);
        Assert.Contains("Map", loaded!.HiddenToolIds);

        var factory2 = CreateFactory(out var layout2);
        Assert.True(factory2.TryApplySnapshot(layout2, loaded));

        Assert.Contains(factory2.HiddenTools, tool => tool.Id == "Map");
        var panels = (layout2.VisibleDockables ?? Enumerable.Empty<IDockable>()).SelectMany(PanelsIn).ToList();
        Assert.DoesNotContain(panels, p => p.Id == "Map");
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
    public void TryApplySnapshot_RejectsSnapshotMissingKnownPanels()
    {
        var factory1 = CreateFactory(out var layout1);
        var snapshot = factory1.Snapshot(layout1);

        // Simulate a stale file from an older app version: one panel unaccounted for. "Map" is
        // used (not "Notes") since it's the one actually in the visible tree by default (see
        // CreateLayout) — "Notes" starts hidden, so removing it from the tree would be a no-op.
        RemovePanel(snapshot.Root!, "Map");

        var factory2 = CreateFactory(out var layout2);
        Assert.False(factory2.TryApplySnapshot(layout2, snapshot));
    }

    [Fact]
    public void TryApplySnapshot_RejectsPanelBothVisibleAndHidden()
    {
        var factory1 = CreateFactory(out var layout1);
        var snapshot = factory1.Snapshot(layout1);
        // "Map" is visible by default (see CreateLayout); adding it to HiddenToolIds too creates
        // a genuine visible-and-hidden conflict ("Notes" starts hidden already, so adding it
        // again would just be a harmless duplicate within the same set).
        snapshot.HiddenToolIds.Add("Map");

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
