using Dock.Model.Controls;
using Dock.Model.Core;
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

    /// <summary>Overlays only work in TRANSPARENCY mode — see <see cref="MudDockFactory.IsTransparencyLayout"/>.</summary>
    private static MudDockFactory CreateTransparencyFactory(out IRootDock layout)
    {
        var factory = new MudDockFactory(new object(), new object());
        layout = factory.CreateTransparencyLayout();
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
    public void CreateTransparencyLayout_TerminalAloneIsVisible_EverythingElseHidden()
    {
        var factory = CreateTransparencyFactory(out var layout);

        Assert.True(Visible(layout, "Terminal"));
        Assert.False(Visible(layout, "Gmcp"));
        Assert.Contains(GetTool(factory, "Gmcp"), factory.HiddenTools);
        Assert.True(factory.IsTransparencyLayout);
    }

    [Fact]
    public void CanPinAsOverlay_OutsideTransparencyMode_IsFalseForEveryTool()
    {
        var factory = CreateFactory(out _);
        var terminal = GetTool(factory, "Terminal");
        var gmcp = GetTool(factory, "Gmcp");

        Assert.False(terminal.PinAsOverlayCommand.CanExecute(null));
        Assert.False(gmcp.PinAsOverlayCommand.CanExecute(null));
    }

    [Fact]
    public void CanPinAsOverlay_InTransparencyMode_IsTrueExceptTerminal()
    {
        var factory = CreateTransparencyFactory(out _);
        var terminal = GetTool(factory, "Terminal");
        var gmcp = GetTool(factory, "Gmcp");

        Assert.False(terminal.PinAsOverlayCommand.CanExecute(null));
        Assert.True(gmcp.PinAsOverlayCommand.CanExecute(null));
    }

    [Fact]
    public void PinToolAsOverlay_OutsideTransparencyMode_IsNoOp()
    {
        var factory = CreateFactory(out var layout);
        var tool = GetTool(factory, "Gmcp");

        factory.PinToolAsOverlay(tool);

        Assert.Empty(factory.OverlayTools);
        Assert.True(Visible(layout, "Gmcp"));
    }

    [Fact]
    public void PinToolAsOverlay_RemovesToolFromDockTree_AndSetsOverlayTool()
    {
        var factory = CreateTransparencyFactory(out var layout);
        var tool = GetTool(factory, "Gmcp");

        factory.PinToolAsOverlay(tool);

        Assert.Contains(tool, factory.OverlayTools);
        Assert.False(Visible(layout, "Gmcp"));
        Assert.DoesNotContain(tool, factory.HiddenTools);
    }

    [Fact]
    public void PinToolAsOverlay_Terminal_IsNoOp()
    {
        var factory = CreateTransparencyFactory(out var layout);
        var terminal = GetTool(factory, "Terminal");

        factory.PinToolAsOverlay(terminal);

        Assert.Empty(factory.OverlayTools);
        Assert.True(Visible(layout, "Terminal"));
    }

    [Fact]
    public void PinToolAsOverlay_SecondTool_BothRemainOverlaidSimultaneously()
    {
        var factory = CreateTransparencyFactory(out var layout);
        var first = GetTool(factory, "Gmcp");
        var second = GetTool(factory, "Notes");

        factory.PinToolAsOverlay(first);
        factory.PinToolAsOverlay(second);

        Assert.Contains(first, factory.OverlayTools);
        Assert.Contains(second, factory.OverlayTools);
        Assert.False(Visible(layout, "Gmcp"));
        Assert.False(Visible(layout, "Notes"));
    }

    [Fact]
    public void SwapOverlayOrder_SwapsTheTwoTools_PositionsInOverlayTools()
    {
        var factory = CreateTransparencyFactory(out _);
        var first = GetTool(factory, "Gmcp");
        var second = GetTool(factory, "Notes");
        factory.PinToolAsOverlay(first);
        factory.PinToolAsOverlay(second);

        factory.SwapOverlayOrder(first, second);

        Assert.Equal([second, first], factory.OverlayTools);
    }

    [Fact]
    public void SwapOverlayOrder_UnknownTool_IsNoOp()
    {
        var factory = CreateTransparencyFactory(out _);
        var overlaid = GetTool(factory, "Gmcp");
        var notOverlaid = GetTool(factory, "Notes");
        factory.PinToolAsOverlay(overlaid);

        factory.SwapOverlayOrder(overlaid, notOverlaid);

        Assert.Equal([overlaid], factory.OverlayTools);
    }

    [Fact]
    public void ReturnOverlayToLayout_InTransparencyMode_DocksToolBackNextToTerminal()
    {
        var factory = CreateTransparencyFactory(out var layout);
        var tool = GetTool(factory, "Gmcp");

        factory.PinToolAsOverlay(tool);
        factory.ReturnOverlayToLayout(tool);

        Assert.DoesNotContain(tool, factory.OverlayTools);
        Assert.True(Visible(layout, "Gmcp"));
    }

    [Fact]
    public void ReturnToLayoutCommand_UnoverlaysAnOverlaidTool()
    {
        var factory = CreateTransparencyFactory(out var layout);
        var tool = GetTool(factory, "Gmcp");

        factory.PinToolAsOverlay(tool);
        Assert.True(tool.ReturnToLayoutCommand.CanExecute(null));

        tool.ReturnToLayoutCommand.Execute(null);

        Assert.DoesNotContain(tool, factory.OverlayTools);
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
    public void CloseOverlay_InTransparencyMode_HidesToolWithoutDockingItNextToTerminal()
    {
        var factory = CreateTransparencyFactory(out var layout);
        var tool = GetTool(factory, "Gmcp");

        factory.PinToolAsOverlay(tool);
        factory.CloseOverlay(tool);

        Assert.DoesNotContain(tool, factory.OverlayTools);
        Assert.False(Visible(layout, "Gmcp"));
        Assert.Contains(tool, factory.HiddenTools);
    }

    [Fact]
    public void CloseOverlayCommand_HidesAnOverlaidTool()
    {
        var factory = CreateTransparencyFactory(out var layout);
        var tool = GetTool(factory, "Gmcp");

        factory.PinToolAsOverlay(tool);
        Assert.True(tool.CloseOverlayCommand.CanExecute(null));

        tool.CloseOverlayCommand.Execute(null);

        Assert.DoesNotContain(tool, factory.OverlayTools);
        Assert.False(Visible(layout, "Gmcp"));
        Assert.Contains(tool, factory.HiddenTools);
    }

    [Fact]
    public void CloseOverlayCommand_CannotExecute_WhenToolIsNotOverlaid()
    {
        var factory = CreateTransparencyFactory(out _);
        var tool = GetTool(factory, "Gmcp");

        Assert.False(tool.CloseOverlayCommand.CanExecute(null));
    }

    [Fact]
    public void Snapshot_WhileToolIsOverlaid_StillAccountsForItSomewhereInTheTree()
    {
        var factory = CreateTransparencyFactory(out var layout);
        var tool = GetTool(factory, "Gmcp");

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
        Assert.True(snapshot.IsTransparencyLayout);
    }

    [Fact]
    public void SaveThenLoadSnapshot_WithActiveOverlay_RoundTrips()
    {
        var factory1 = CreateTransparencyFactory(out var layout1);
        factory1.PinToolAsOverlay(GetTool(factory1, "Gmcp"));

        var snapshot = factory1.Snapshot(layout1);

        var factory2 = CreateFactory(out var layout2);
        Assert.True(factory2.TryApplySnapshot(layout2, snapshot));

        // The preset snapshot describes the static shape as if nothing were overlaid — the
        // overlay itself is reapplied afterward from AppSettings (MainWindowViewModel). The
        // snapshot's own IsTransparencyLayout flag must still come back true so the restored
        // session allows pinning overlays again.
        Assert.True(Visible(layout2, "Gmcp"));
        Assert.Empty(factory2.OverlayTools);
        Assert.True(factory2.IsTransparencyLayout);
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
}
