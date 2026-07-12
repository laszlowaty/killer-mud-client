using System.Collections.ObjectModel;
using System.Linq;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm;
using Dock.Model.Mvvm.Controls;

namespace MudClient.App.Docking;

/// <summary>
/// Builds the default dock layout (map / room info | terminal | six right-side panels)
/// and tracks tools the user has closed so they can be restored from the "Panele" menu.
/// </summary>
public sealed class MudDockFactory : Factory
{
    private readonly object _mapContext;
    private readonly object _mainContext;
    private readonly Dictionary<PanelTool, IDock> _lastOwners = new();
    private readonly Dictionary<IDock, IDock> _lastDockOwners = new();
    private IRootDock? _root;

    public MudDockFactory(object mapContext, object mainContext)
    {
        _mapContext = mapContext;
        _mainContext = mainContext;
    }

    /// <summary>Id of the required-buffs tool; its tab title carries a live x/y badge.</summary>
    public const string BuffsToolId = "Buffs";

    public List<PanelTool> AllTools { get; } = new();

    public ObservableCollection<PanelTool> HiddenTools { get; } = new();

    /// <summary>
    /// Supplies the desired expanded size (in DIPs) for a freshly pinned edge tab's preview:
    /// half the dock area's width for <see cref="Alignment.Left"/>/<see cref="Alignment.Right"/>,
    /// half its height for <see cref="Alignment.Top"/>/<see cref="Alignment.Bottom"/>. Wired from
    /// the view, which knows the live control bounds; left null in headless tests, where the
    /// preview keeps Dock's content-sized default.
    /// </summary>
    public Func<Alignment, double>? PinnedPreviewSizeProvider { get; set; }

    private PanelTool NewTool(string id, string title, Type viewType, object context)
    {
        var tool = new PanelTool
        {
            Id = id,
            Title = title,
            ViewType = viewType,
            Context = context,
            CanClose = true,
            // Floating panel windows are disabled: Dock 12 corrupts the layout when
            // a floated window loses activation (panels vanish from the tree).
            CanFloat = false,
            // CanPin must stay true — Dock's PinDockable is a no-op otherwise, and both the
            // edge drag & drop below and the "Schowaj z…" tab menu pin programmatically.
            // The chrome's own pin button is hidden in Styles/Docking.axaml.
            CanPin = true,
            // "Dock as tabbed document" misbehaves in Dock 12 (panels get lost) — disabled,
            // which also drops its entry from the tab context menu.
            CanDockAsDocument = false,
        };
        tool.PinToEdge = edge => PinToolToEdge(tool, edge);
        AllTools.Add(tool);
        return tool;
    }

    /// <summary>
    /// Auto-hides <paramref name="tool"/> as a collapsed tab on the chosen screen
    /// <paramref name="edge"/>. Dock 12 derives the pin edge from the owner ToolDock's
    /// <see cref="Alignment"/>, so we flip that alignment for the duration of the pin, then
    /// restore it (the tool now lives in the root's pinned collection for the edge and is no
    /// longer governed by the owner alignment). The edge is persisted in layout snapshots.
    /// </summary>
    public void PinToolToEdge(PanelTool tool, Alignment edge)
    {
        if (tool.Owner is not ToolDock owner)
        {
            return;
        }

        var saved = owner.Alignment;
        owner.Alignment = edge;
        SetActiveDockable(tool);
        PinDockable(tool);
        owner.Alignment = saved;

        ApplyDefaultPinnedSize(tool, edge);
    }

    /// <summary>
    /// Gives a newly pinned tab a preview that opens at half the dock area — half the width for
    /// a side (Left/Right) tab, half the height for a top/bottom tab. Dock reads the expanded
    /// size from the tool's <c>PinnedBounds</c>; <see cref="PinDockable"/> seeds those bounds with
    /// the collapsed pane's own (much smaller) size, so this must run <em>after</em> pinning and
    /// overwrite it. Both axes are stored (only the edge's axis is applied to the preview) so Dock
    /// treats the size as explicit and won't reset it to the content's desired size on later passes.
    /// </summary>
    private void ApplyDefaultPinnedSize(PanelTool tool, Alignment edge)
    {
        if (PinnedPreviewSizeProvider is not { } provider)
        {
            return;
        }

        var size = provider(edge);
        if (!IsValidSize(size))
        {
            return;
        }

        // Off-axis dimension is irrelevant to the preview but must stay valid so Dock keeps the
        // on-axis size fixed; reuse the same value rather than leaving it NaN.
        tool.SetPinnedBounds(0, 0, size, size);
    }

    private static bool IsValidSize(double size) => !double.IsNaN(size) && !double.IsInfinity(size) && size > 0;

    /// <summary>
    /// Dropping a panel on the main window's outer edge (Dock's "global docking") normally
    /// splits the whole layout, which users found broken and useless. Instead, turn an edge
    /// drop into a pinned tab on that edge — drag a panel to the window border and it becomes
    /// a collapsed side tab.
    /// </summary>
    /// <remarks>
    /// Dock 12 only routes a drop through global docking (→ outermost target) when the pointer
    /// lands on the small, fixed-size global edge selector at release time; otherwise it falls
    /// back to a <em>local</em> split of the ToolDock under the cursor. The edge-most panes are
    /// the last thing between the cursor and the window border, so the more tabs pile up there
    /// the more often an intended edge-pin was resolved as a split instead — the reported bug.
    /// We close that gap: a directional split whose target dock actually sits against that window
    /// edge (<see cref="IsAgainstWindowEdge"/>) is treated as an edge-pin too, not just the
    /// outermost/global target. Genuine inner splits (targets not touching the edge) are untouched.
    /// </remarks>
    public override void SplitToDock(IDock dock, IDockable dockable, DockOperation operation)
    {
        if (ToPinEdge(operation) is { } edge && ExtractPanels(dockable) is { Count: > 0 } panels)
        {
            // Global drops target the outermost dock thanks to GlobalDockingPreset.GlobalFirst
            // (set in App.Initialize); near-miss local splits arrive with a deeper target that
            // may still be the pane hugging this window edge.
            var isOuterTarget = dock is IRootDock || dock.Owner is IRootDock;
            if (isOuterTarget || IsAgainstWindowEdge(dock, edge))
            {
                foreach (var panel in panels)
                {
                    PinPanelToEdge(panel, edge);
                }

                return;
            }
        }

        base.SplitToDock(dock, dockable, operation);
    }

    /// <summary>
    /// True when <paramref name="dock"/> sits flush against the window's <paramref name="edge"/>
    /// in the live layout tree. Every ProportionalDock ancestor that splits <em>along</em> the
    /// edge's axis must be entered through its first (Left/Top) or last (Right/Bottom) non-splitter
    /// child; ancestors that split along the other axis span the full edge and add no constraint.
    /// </summary>
    private bool IsAgainstWindowEdge(IDock dock, Alignment edge)
    {
        if (LiveOrNull(dock) is null)
        {
            return false;
        }

        var edgeAxisIsHorizontal = edge is Alignment.Left or Alignment.Right;
        var wantFirstChild = edge is Alignment.Left or Alignment.Top;

        IDockable current = dock;
        for (var owner = dock.Owner as IDock; owner is not null; current = owner, owner = owner.Owner as IDock)
        {
            if (owner is not ProportionalDock proportional)
            {
                // Root and tool-dock wrappers impose no positional constraint.
                continue;
            }

            if ((proportional.Orientation == Orientation.Horizontal) != edgeAxisIsHorizontal)
            {
                // Splits along the other axis — this ancestor spans the whole edge.
                continue;
            }

            var siblings = (proportional.VisibleDockables ?? Enumerable.Empty<IDockable>())
                .Where(child => child is not ProportionalDockSplitter)
                .ToList();
            var edgeSibling = wantFirstChild ? siblings.FirstOrDefault() : siblings.LastOrDefault();
            if (!ReferenceEquals(edgeSibling, current))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// The panels carried by an edge drop. Dock's DockService wraps the dragged tool in a
    /// fresh, detached ToolDock before calling <see cref="SplitToDock"/>, so unwrap both a
    /// bare <see cref="PanelTool"/> and any dock of them.
    /// </summary>
    private static List<PanelTool> ExtractPanels(IDockable dockable) => DescendantTools(dockable).ToList();

    /// <summary>
    /// Pins <paramref name="panel"/> to <paramref name="edge"/> even when it currently sits in
    /// a detached wrapper dock (the DockService one): the panel is first docked into a live
    /// tool dock so Dock can find its root, then collapsed into the edge tab.
    /// </summary>
    private void PinPanelToEdge(PanelTool panel, Alignment edge)
    {
        if (_root is null)
        {
            return;
        }

        if (LiveOrNull(panel.Owner as IDock) is null)
        {
            if (FindFirstToolDock(_root) is not { } live)
            {
                return;
            }

            AddDockable(live, panel);
        }

        PinToolToEdge(panel, edge);
    }

    private static Alignment? ToPinEdge(DockOperation operation) => operation switch
    {
        DockOperation.Left => Alignment.Left,
        DockOperation.Right => Alignment.Right,
        DockOperation.Top => Alignment.Top,
        DockOperation.Bottom => Alignment.Bottom,
        _ => null,
    };

    public override IRootDock CreateLayout()
    {
        var mapTool = NewTool("Map", "🗺 Mapa", typeof(Views.Panels.MapPanelView), _mapContext);
        var roomInfoTool = NewTool("RoomInfo", "📋 Pokój", typeof(Views.Panels.RoomInfoPanelView), _mapContext);
        var terminalTool = NewTool("Terminal", "Terminal", typeof(Views.Panels.TerminalPanelView), _mainContext);
        var infoTool = NewTool("CharInfo", "👤 Postać", typeof(Views.Panels.CharacterInfoPanelView), _mainContext);
        var conditionTool = NewTool("Condition", "♥ Kondycja", typeof(Views.Panels.ConditionPanelView), _mainContext);
        var effectsTool = NewTool("Effects", "✨ Efekty", typeof(Views.Panels.EffectsPanelView), _mainContext);
        var buffsTool = NewTool(BuffsToolId, "🛡 Buffy", typeof(Views.Panels.BuffsPanelView), _mainContext);
        var roomPeopleTool = NewTool("RoomPeople", "👁 Pokój", typeof(Views.Panels.RoomPeoplePanelView), _mainContext);
        var groupTool = NewTool("Group", "👥 Drużyna", typeof(Views.Panels.GroupPanelView), _mainContext);
        var memSpellsTool = NewTool("MemSpells", "📜 Mem", typeof(Views.Panels.MemSpellsPanelView), _mainContext);
        var automationTool = NewTool("Automation", "⚙ Automaty", typeof(Views.Panels.AutomationPanelView), _mainContext);
        var autowalkTool = NewTool("Autowalk", "🧭 Autowalk", typeof(Views.Panels.AutowalkPanelView), _mainContext);
        var notesTool = NewTool("Notes", "✎ Notatki", typeof(Views.Panels.NotesPanelView), _mainContext);
        var gmcpTool = NewTool("Gmcp", "⇅ GMCP", typeof(Views.Panels.GmcpPanelView), _mainContext);
        var settingsTool = NewTool("Settings", "🛠 Ustawienia", typeof(Views.Panels.SettingsPanelView), _mainContext);

        var leftDock = new ToolDock
        {
            Id = "LeftPane",
            Proportion = 0.25,
            ActiveDockable = mapTool,
            VisibleDockables = CreateList<IDockable>(mapTool, roomInfoTool),
            Alignment = Alignment.Left,
        };

        var centerDock = new ToolDock
        {
            Id = "CenterPane",
            Proportion = 0.5,
            ActiveDockable = terminalTool,
            VisibleDockables = CreateList<IDockable>(terminalTool),
            Alignment = Alignment.Left,
        };

        // Character sections are individual tools so the user can drag any of
        // them anywhere (tabs, splits, floating windows) like every other panel.
        var rightTopDock = new ToolDock
        {
            Id = "RightTopPane",
            Proportion = 0.5,
            ActiveDockable = infoTool,
            VisibleDockables = CreateList<IDockable>(
                infoTool, conditionTool, effectsTool, buffsTool, roomPeopleTool, groupTool, memSpellsTool),
            Alignment = Alignment.Right,
        };

        var rightBottomDock = new ToolDock
        {
            Id = "RightBottomPane",
            Proportion = 0.5,
            ActiveDockable = automationTool,
            VisibleDockables = CreateList<IDockable>(
                automationTool, autowalkTool, notesTool, gmcpTool, settingsTool),
            Alignment = Alignment.Right,
        };

        var rightDock = new ProportionalDock
        {
            Id = "RightPane",
            Proportion = 0.25,
            Orientation = Orientation.Vertical,
            VisibleDockables = CreateList<IDockable>(
                rightTopDock,
                new ProportionalDockSplitter { Id = "SplitterRight" },
                rightBottomDock),
        };

        var mainLayout = new ProportionalDock
        {
            Id = "MainLayout",
            Orientation = Orientation.Horizontal,
            VisibleDockables = CreateList<IDockable>(
                leftDock,
                new ProportionalDockSplitter { Id = "Splitter1" },
                centerDock,
                new ProportionalDockSplitter { Id = "Splitter2" },
                rightDock),
        };

        var rootDock = CreateRootDock();
        rootDock.Id = "Root";
        rootDock.VisibleDockables = CreateList<IDockable>(mainLayout);
        rootDock.ActiveDockable = mainLayout;
        rootDock.DefaultDockable = mainLayout;

        _root = rootDock;

        return rootDock;
    }

    public override void OnDockableClosed(IDockable? dockable)
    {
        base.OnDockableClosed(dockable);

        foreach (var tool in DescendantTools(dockable))
        {
            if (!HiddenTools.Contains(tool))
            {
                HiddenTools.Add(tool);
            }
        }
    }

    /// <summary>
    /// Safety net run after every drag: any tool that is no longer reachable — not in the
    /// visible tree, not pinned to an edge, not already in <see cref="HiddenTools"/> — is
    /// moved to <see cref="HiddenTools"/> so it shows up in the "Panele" restore menu.
    /// Dock 12's drag pipeline can drop a tool on the floor when a drag ends over
    /// non-dock chrome (e.g. the top bar); this guarantees nothing is ever lost silently.
    /// </summary>
    public void ReclaimLostTools(IRootDock root)
    {
        var reachable = new HashSet<PanelTool>(DescendantTools(root));
        foreach (var list in new[]
        {
            root.LeftPinnedDockables,
            root.RightPinnedDockables,
            root.TopPinnedDockables,
            root.BottomPinnedDockables,
        })
        {
            foreach (var pinned in (list ?? Enumerable.Empty<IDockable>()).OfType<PanelTool>())
            {
                reachable.Add(pinned);
            }
        }

        foreach (var tool in AllTools)
        {
            if (!reachable.Contains(tool) && !HiddenTools.Contains(tool))
            {
                HiddenTools.Add(tool);
            }
        }
    }

    public override bool OnDockableClosing(IDockable? dockable)
    {
        foreach (var tool in DescendantTools(dockable))
        {
            if (tool.Owner is IDock owner)
            {
                _lastOwners[tool] = owner;
            }
        }

        if (dockable is IDock dock && dock.Owner is IDock dockOwner)
        {
            _lastDockOwners[dock] = dockOwner;
        }

        return base.OnDockableClosing(dockable);
    }

    /// <summary>
    /// Re-adds a previously closed panel to a live dock. Prefers the panel's original group
    /// (its current owner, or the recorded owner reattached via its parent chain); if none of
    /// those are reachable it falls back to any visible tool dock so the panel <em>always</em>
    /// reappears. The old code could bail out here and leave the panel silently hidden — that
    /// was the intermittent "restore doesn't work".
    /// </summary>
    public void Restore(PanelTool tool)
    {
        if (_root is null)
        {
            return;
        }

        // Closing the chrome of an expanded auto-hide preview does not fully clear Dock
        // 12's pinned/preview state. If we AddDockable immediately, the model changes but
        // the presenter keeps treating the tool as a closed side tab and renders nothing.
        // Unpin first; this also puts the tool back in its original ToolDock when Dock still
        // has enough ownership information to do so.
        if (IsPinned(_root, tool))
        {
            UnpinDockable(tool);
            if (ContainsDockable(_root, tool) && tool.Owner is ToolDock restoredOwner)
            {
                SetActiveDockable(tool);
                SetFocusedDockable(restoredOwner, tool);
                HiddenTools.Remove(tool);
                return;
            }
        }

        var owner =
            LiveToolDockOrNull(tool.Owner as IDock)
            ?? (_lastOwners.TryGetValue(tool, out var lastOwner) ? Reattach(lastOwner) as ToolDock : null)
            ?? LiveToolDockOrNull(tool.OriginalOwner as IDock)
            ?? FindFirstToolDock(_root);
        if (owner is null)
        {
            return;
        }

        AddDockable(owner, tool);
        SetActiveDockable(tool);
        SetFocusedDockable(owner, tool);
        HiddenTools.Remove(tool);
    }

    /// <summary>
    /// Restores a panel selected from the "Panele" menu as a top-edge auto-hide tab.
    /// This deliberately ignores the previous owner: Dock 12 can leave that owner detached
    /// after closing an expanded pinned preview, making parent-based restoration unreliable.
    /// </summary>
    public void RestoreToTopEdge(PanelTool tool)
    {
        if (_root is null)
        {
            return;
        }

        if (IsPinned(_root, tool))
        {
            UnpinDockable(tool);
        }

        // UnpinDockable does not always remove a closed preview wrapper in Dock 12.
        // Remove every stale pinned entry carrying this tool before pinning it anew.
        foreach (var pinned in new[]
        {
            _root.LeftPinnedDockables,
            _root.RightPinnedDockables,
            _root.TopPinnedDockables,
            _root.BottomPinnedDockables,
        })
        {
            foreach (var entry in (pinned ?? Enumerable.Empty<IDockable>())
                         .Where(entry => ReferenceEquals(entry, tool) || DescendantTools(entry).Contains(tool))
                         .ToList())
            {
                pinned!.Remove(entry);
            }
        }

        var stagingDock = FindFirstToolDock(_root);
        if (stagingDock is null)
        {
            return;
        }

        if (!ReferenceEquals(tool.Owner, stagingDock)
            || stagingDock.VisibleDockables?.Contains(tool) != true)
        {
            AddDockable(stagingDock, tool);
        }

        PinToolToEdge(tool, Alignment.Top);
        HiddenTools.Remove(tool);
    }

    private static bool IsPinned(IRootDock root, PanelTool tool) =>
        new[]
        {
            root.LeftPinnedDockables,
            root.RightPinnedDockables,
            root.TopPinnedDockables,
            root.BottomPinnedDockables,
        }.Any(list => list?.Any(item => ReferenceEquals(item, tool) || DescendantTools(item).Contains(tool)) == true);

    /// <summary>Returns <paramref name="dock"/> if it is currently attached to the live tree, else null.</summary>
    private IDock? LiveOrNull(IDock? dock) =>
        dock is not null && _root is not null && (ReferenceEquals(dock, _root) || ContainsDockable(_root, dock))
            ? dock
            : null;

    // A pinned tool is owned by the root. Adding it directly back to that root produces a
    // formally reachable child that Dock's tool presenter cannot display; restored panels
    // must always land in an actual ToolDock.
    private ToolDock? LiveToolDockOrNull(IDock? dock) => LiveOrNull(dock) as ToolDock;

    /// <summary>Brings a recorded owner back into the tree via its parent chain; returns it if it
    /// ends up attached, otherwise null so <see cref="Restore"/> can fall back to a live dock.</summary>
    private IDock? Reattach(IDock? dock) =>
        dock is not null && EnsureAttached(dock) ? dock : null;

    /// <summary>Rebuilds a brand-new default layout (fresh tools, fresh tree) and initializes it.</summary>
    public IRootDock ResetToDefault()
    {
        AllTools.Clear();
        HiddenTools.Clear();
        _lastOwners.Clear();
        _lastDockOwners.Clear();

        var root = CreateLayout();
        InitLayout(root);
        return root;
    }

    private bool EnsureAttached(IDock dock)
    {
        if (_root is null)
        {
            return false;
        }

        if (ReferenceEquals(dock, _root) || ContainsDockable(_root, dock))
        {
            return true;
        }

        var parent = dock.Owner as IDock
            ?? (_lastDockOwners.TryGetValue(dock, out var lastParent) ? lastParent : null);
        if (parent is not null && EnsureAttached(parent))
        {
            AddDockable(parent, dock);
            return true;
        }

        // Dock can remove an empty ToolDock without a separate close event, leaving no
        // reachable parent. Restore() handles that by falling back to a live tool dock.
        return false;
    }

    private static bool ContainsDockable(IDock dock, IDockable sought) =>
        dock.VisibleDockables?.Any(child =>
            ReferenceEquals(child, sought) || child is IDock nested && ContainsDockable(nested, sought)) == true;

    private static IDock? FindFirstToolDock(IDock dock)
    {
        foreach (var child in dock.VisibleDockables ?? Enumerable.Empty<IDockable>())
        {
            if (child is ToolDock toolDock)
            {
                return toolDock;
            }

            if (child is IDock nested && FindFirstToolDock(nested) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static IEnumerable<PanelTool> DescendantTools(IDockable? dockable) => dockable switch
    {
        PanelTool tool => new[] { tool },
        IDock dock => (dock.VisibleDockables ?? Enumerable.Empty<IDockable>()).SelectMany(DescendantTools),
        _ => Enumerable.Empty<PanelTool>(),
    };

    // ========================================================================
    // Layout persistence (save/restore panel arrangement across app restarts)
    // ========================================================================

    public DockLayoutSnapshot Snapshot(IRootDock root)
    {
        var rootChild = root.VisibleDockables?.FirstOrDefault();
        return new DockLayoutSnapshot
        {
            Root = rootChild is null ? null : BuildNode(rootChild),
            HiddenToolIds = HiddenTools.Select(t => t.Id!).ToList(),
            PinnedTools = PinnedTools(root),
        };
    }

    private static List<PinnedToolSnapshot> PinnedTools(IRootDock root)
    {
        var edges = new[]
        {
            (Alignment.Left, root.LeftPinnedDockables),
            (Alignment.Right, root.RightPinnedDockables),
            (Alignment.Top, root.TopPinnedDockables),
            (Alignment.Bottom, root.BottomPinnedDockables),
        };

        return edges
            .Where(e => e.Item2 is not null)
            .SelectMany(e => e.Item2!.OfType<PanelTool>().Select(tool => new PinnedToolSnapshot
            {
                Id = tool.Id!,
                OwnerId = (tool.Owner as IDock)?.Id,
                Edge = e.Item1.ToString(),
            }))
            .ToList();
    }

    /// <summary>
    /// Rebuilds <paramref name="root"/>'s tree from <paramref name="snapshot"/>, reusing the
    /// live <see cref="PanelTool"/> instances in <see cref="AllTools"/>. Returns false (leaving
    /// <paramref name="root"/> untouched) if the snapshot doesn't reference exactly the current
    /// set of known tools — e.g. after an app update added/removed a panel.
    /// </summary>
    public bool TryApplySnapshot(IRootDock root, DockLayoutSnapshot snapshot)
    {
        if (snapshot.Root is null)
        {
            return false;
        }

        var referenced = new HashSet<string>();
        CollectPanelIds(snapshot.Root, referenced);
        var hidden = new HashSet<string>(snapshot.HiddenToolIds);
        var pinned = new HashSet<string>(snapshot.PinnedTools.Select(p => p.Id));
        var known = AllTools.Select(t => t.Id!).ToHashSet();

        // Every known tool must appear exactly once across the visible tree, the hidden
        // list, and the pinned list — otherwise the snapshot predates a panel change.
        if (referenced.Overlaps(hidden) || referenced.Overlaps(pinned) || hidden.Overlaps(pinned)
            || !new HashSet<string>(referenced.Union(hidden).Union(pinned)).SetEquals(known))
        {
            return false;
        }

        var toolsById = AllTools.ToDictionary(t => t.Id!);
        if (BuildFromSnapshot(snapshot.Root, toolsById) is not { } built)
        {
            return false;
        }

        root.VisibleDockables = CreateList<IDockable>(built);
        root.ActiveDockable = built;
        root.DefaultDockable = built;
        InitLayout(root);

        HiddenTools.Clear();
        foreach (var id in hidden)
        {
            if (toolsById.TryGetValue(id, out var tool))
            {
                HiddenTools.Add(tool);
            }
        }

        RestorePinnedTools(root, snapshot.PinnedTools, toolsById);

        return true;
    }

    /// <summary>
    /// Re-hides tools the user had auto-hidden: docks each into its recorded owner (so it picks
    /// up that dock's edge alignment), then pins it via the factory so Dock rebuilds the pinned bar.
    /// </summary>
    private void RestorePinnedTools(
        IRootDock root, List<PinnedToolSnapshot> pinnedTools, Dictionary<string, PanelTool> toolsById)
    {
        foreach (var pin in pinnedTools)
        {
            if (!toolsById.TryGetValue(pin.Id, out var tool))
            {
                continue;
            }

            var owner = (pin.OwnerId is not null ? FindDockById(root, pin.OwnerId) : null)
                ?? FindFirstToolDock(root);
            if (owner is null)
            {
                continue;
            }

            AddDockable(owner, tool);

            // Honor the edge the tab was on. Older snapshots have no Edge; fall back to
            // the owner dock's alignment, matching the pre-per-edge behavior.
            if (Enum.TryParse<Alignment>(pin.Edge, out var edge))
            {
                PinToolToEdge(tool, edge);
            }
            else
            {
                SetActiveDockable(tool);
                PinDockable(tool);
            }
        }
    }

    private static IDock? FindDockById(IDock dock, string id)
    {
        if (string.Equals(dock.Id, id, StringComparison.Ordinal))
        {
            return dock;
        }

        foreach (var child in dock.VisibleDockables ?? Enumerable.Empty<IDockable>())
        {
            if (child is IDock nested && FindDockById(nested, id) is { } found)
            {
                return found;
            }
        }

        return null;
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

    private static DockNodeSnapshot BuildNode(IDockable dockable) => dockable switch
    {
        PanelTool tool => new DockNodeSnapshot { Kind = "Panel", Id = tool.Id },
        ProportionalDockSplitter splitter => new DockNodeSnapshot { Kind = "Splitter", Id = splitter.Id },
        ProportionalDock pd => new DockNodeSnapshot
        {
            Kind = "Proportional",
            Id = pd.Id,
            Proportion = pd.Proportion,
            Orientation = pd.Orientation.ToString(),
            Children = pd.VisibleDockables?.Select(BuildNode).ToList() ?? new(),
        },
        ToolDock td => new DockNodeSnapshot
        {
            Kind = "ToolDock",
            Id = td.Id,
            Proportion = td.Proportion,
            ActiveDockableId = (td.ActiveDockable as IDockable)?.Id,
            Children = td.VisibleDockables?.Select(BuildNode).ToList() ?? new(),
        },
        _ => new DockNodeSnapshot { Kind = "Unknown", Id = dockable.Id },
    };

    private IDockable? BuildFromSnapshot(DockNodeSnapshot node, Dictionary<string, PanelTool> toolsById)
    {
        switch (node.Kind)
        {
            case "Panel":
                return node.Id is not null && toolsById.TryGetValue(node.Id, out var tool) ? tool : null;

            case "Splitter":
                return new ProportionalDockSplitter { Id = node.Id ?? Guid.NewGuid().ToString() };

            case "Proportional":
            {
                var children = node.Children
                    .Select(c => BuildFromSnapshot(c, toolsById))
                    .Where(d => d is not null)
                    .Cast<IDockable>()
                    .ToList();
                if (children.Count == 0)
                {
                    return null;
                }

                return new ProportionalDock
                {
                    Id = node.Id ?? "Proportional",
                    Proportion = node.Proportion,
                    Orientation = Enum.TryParse<Orientation>(node.Orientation, out var orientation)
                        ? orientation
                        : Orientation.Horizontal,
                    VisibleDockables = CreateList<IDockable>(children.ToArray()),
                };
            }

            case "ToolDock":
            {
                var children = node.Children
                    .Select(c => BuildFromSnapshot(c, toolsById))
                    .Where(d => d is not null)
                    .Cast<IDockable>()
                    .ToList();
                if (children.Count == 0)
                {
                    return null;
                }

                var active = node.ActiveDockableId is not null
                    ? children.FirstOrDefault(c => c.Id == node.ActiveDockableId) ?? children[0]
                    : children[0];

                return new ToolDock
                {
                    Id = node.Id ?? "ToolDock",
                    Proportion = node.Proportion,
                    ActiveDockable = active,
                    VisibleDockables = CreateList<IDockable>(children.ToArray()),
                };
            }

            default:
                return null;
        }
    }
}
