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
public sealed class MudDockFactory : Factory, IFactory
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
    /// Supplies the fixed expanded size (in DIPs) for a pinned edge tab's preview: one third of
    /// the dock area's width for <see cref="Alignment.Left"/>/<see cref="Alignment.Right"/> and
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
            // CanPin must stay true — Dock's PinDockable is a no-op otherwise, while the
            // explicit edge commands in the Panels and tab menus pin programmatically.
            // The chrome's own pin button is hidden in Styles/Docking.axaml.
            CanPin = true,
            // "Dock as tabbed document" misbehaves in Dock 12 (panels get lost) — disabled,
            // which also drops its entry from the tab context menu.
            CanDockAsDocument = false,
        };
        tool.PinToEdge = edge => PinToolToEdge(tool, edge);
        tool.ReturnToLayout = () => ReturnToLayout(tool);
        tool.CanReturnToLayout = () => _root is not null && IsPinned(_root, tool);
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
        if (_root is null)
        {
            return;
        }

        // A pinned preview is moved between edges only through its menu. Re-enable drag
        // temporarily for Dock's internal unpin/re-pin transition, then disable it again.
        tool.CanDrag = true;

        // Preserve the last real ToolDock before Dock replaces Owner with the root while
        // auto-hidden. OriginalOwner is not reliable after repeated pin/unpin operations.
        var preferredOwner =
            LiveToolDockOrNull(tool.Owner as IDock)
            ?? LiveToolDockOrNull(tool.OriginalOwner as IDock)
            ?? (_lastOwners.TryGetValue(tool, out var rememberedOwner)
                ? Reattach(rememberedOwner) as ToolDock
                : null);
        if (preferredOwner is not null)
        {
            RememberOwner(tool, preferredOwner);
        }

        // PinDockable does not move an already pinned tool between edge collections reliably
        // in Dock 12. It can leave the same tab on both the old and new edge. Always normalize
        // the model back to one live ToolDock before pinning it exactly once.
        if (IsPinned(_root, tool))
        {
            UnpinDockable(tool);
        }

        RemovePinnedEntries(tool);

        if (!ContainsDockable(_root, tool))
        {
            preferredOwner ??=
                (_lastOwners.TryGetValue(tool, out var lastOwner) ? Reattach(lastOwner) as ToolDock : null)
                ?? FindFirstToolDock(_root) as ToolDock;
            if (preferredOwner is null)
            {
                return;
            }

            AddDockable(preferredOwner, tool);
        }

        if (LiveToolDockOrNull(tool.Owner as IDock) is not { } owner)
        {
            return;
        }

        var saved = owner.Alignment;
        owner.Alignment = edge;
        SetActiveDockable(tool);
        PinDockable(tool);
        owner.Alignment = saved;

        ApplyFixedPinnedSize(tool, edge);
        tool.CanDrag = false;
        HiddenTools.Remove(tool);
        tool.RefreshDockCommands();
    }

    private void RememberOwner(PanelTool tool, ToolDock owner)
    {
        _lastOwners[tool] = owner;
        if (owner.Owner is IDock dockOwner)
        {
            _lastDockOwners[owner] = dockOwner;
        }
    }

    /// <summary>
    /// Replaces Dock's remembered <c>PinnedBounds</c> with the configured fixed edge size. Both
    /// axes are stored (only the edge's axis is applied to the preview) so Dock treats the size as
    /// explicit rather than falling back to the panel content's desired size.
    /// </summary>
    private void ApplyFixedPinnedSize(PanelTool tool, Alignment edge)
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

    /// <summary>
    /// Dock's tab button invokes this method through <see cref="IFactory"/>. Reset the bounds on
    /// every toggle so a previous splitter drag or a window resize is never remembered by the next
    /// expansion; the preview is always recalculated from the live dock dimensions.
    /// </summary>
    void IFactory.TogglePreviewPinnedDockable(IDockable dockable)
    {
        if (dockable is PanelTool tool && FindPinnedEdge(tool) is { } edge)
        {
            ApplyFixedPinnedSize(tool, edge);
        }

        TogglePreviewPinnedDockable(dockable);
    }

    private Alignment? FindPinnedEdge(PanelTool tool)
    {
        if (_root?.LeftPinnedDockables?.Contains(tool) == true)
        {
            return Alignment.Left;
        }

        if (_root?.RightPinnedDockables?.Contains(tool) == true)
        {
            return Alignment.Right;
        }

        if (_root?.TopPinnedDockables?.Contains(tool) == true)
        {
            return Alignment.Top;
        }

        return _root?.BottomPinnedDockables?.Contains(tool) == true ? Alignment.Bottom : null;
    }

    private static bool IsValidSize(double size) => !double.IsNaN(size) && !double.IsInfinity(size) && size > 0;

    public override IRootDock CreateLayout()
    {
        var mapTool = NewTool("Map", "🗺 Mapa", typeof(Views.Panels.MapPanelView), _mapContext);
        var roomInfoTool = NewTool("RoomInfo", "📋 Pokój", typeof(Views.Panels.RoomInfoPanelView), _mainContext);
        var terminalTool = NewTool("Terminal", "Terminal", typeof(Views.Panels.TerminalPanelView), _mainContext);
        var infoTool = NewTool("CharInfo", "👤 Postać", typeof(Views.Panels.CharacterInfoPanelView), _mainContext);
        var conditionTool = NewTool("Condition", "♥ Kondycja", typeof(Views.Panels.ConditionPanelView), _mainContext);
        var effectsTool = NewTool("Effects", "✨ Efekty", typeof(Views.Panels.EffectsPanelView), _mainContext);
        var buffsTool = NewTool(BuffsToolId, "🛡 Buffy", typeof(Views.Panels.BuffsPanelView), _mainContext);
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
                infoTool, conditionTool, effectsTool, buffsTool, groupTool, memSpellsTool),
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
            // Never overwrite a remembered ToolDock with the root that temporarily owns an
            // auto-hidden tool. That is the parent-loss bug seen after closing a pinned preview.
            var owner = tool.Owner as ToolDock ?? tool.OriginalOwner as ToolDock;
            if (owner is not null)
            {
                RememberOwner(tool, owner);
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

        tool.CanDrag = true;

        // Closing the chrome of an expanded auto-hide preview does not fully clear Dock
        // 12's pinned/preview state. If we AddDockable immediately, the model changes but
        // the presenter keeps treating the tool as a closed side tab and renders nothing.
        // Unpin first; this also puts the tool back in its original ToolDock when Dock still
        // has enough ownership information to do so.
        if (IsPinned(_root, tool))
        {
            UnpinDockable(tool);
            RemovePinnedEntries(tool);
            if (ContainsDockable(_root, tool) && tool.Owner is ToolDock restoredOwner)
            {
                if (_lastOwners.TryGetValue(tool, out var remembered)
                    && Reattach(remembered) is ToolDock preferredOwner
                    && !ReferenceEquals(restoredOwner, preferredOwner))
                {
                    MoveDockable(restoredOwner, preferredOwner, tool, null);
                    restoredOwner = preferredOwner;
                }

                SetActiveDockable(tool);
                SetFocusedDockable(restoredOwner, tool);
                HiddenTools.Remove(tool);
                tool.RefreshDockCommands();
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
        tool.RefreshDockCommands();
    }

    /// <summary>Moves an auto-hidden widget back to its remembered ToolDock.</summary>
    public void ReturnToLayout(PanelTool tool)
    {
        if (_root is null || !IsPinned(_root, tool))
        {
            return;
        }

        Restore(tool);
    }

    /// <summary>Restores, selects and focuses a tool so an action can reveal its content.</summary>
    public bool ShowTool(string id)
    {
        if (_root is null || AllTools.FirstOrDefault(tool => tool.Id == id) is not { } tool)
        {
            return false;
        }

        if (IsPinned(_root, tool) || !ContainsDockable(_root, tool))
        {
            Restore(tool);
        }

        if (tool.Owner is not ToolDock owner || !ContainsDockable(_root, tool))
        {
            return false;
        }

        SetActiveDockable(tool);
        SetFocusedDockable(owner, tool);
        HiddenTools.Remove(tool);
        return true;
    }

    /// <summary>
    /// Restores a panel selected from the "Panele" menu as a top-edge auto-hide tab.
    /// This deliberately ignores the previous owner: Dock 12 can leave that owner detached
    /// after closing an expanded pinned preview, making parent-based restoration unreliable.
    /// </summary>
    public void RestoreToTopEdge(PanelTool tool)
    {
        PinToolToEdge(tool, Alignment.Top);
    }

    private void RemovePinnedEntries(PanelTool tool)
    {
        if (_root is null)
        {
            return;
        }

        // UnpinDockable does not always remove a closed preview wrapper in Dock 12.
        // Purge all stale wrappers/references before adding one canonical edge entry.
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
    }

    /// <summary>
    /// Re-pins model-level entries for which Dock failed to create visible edge tabs. Keeping
    /// the original edge is important during startup: a slow first render must never turn valid
    /// imported pins into closed panels and persist that destructive state on shutdown.
    /// </summary>
    public void RepairUnrenderedPinnedTools(
        IRootDock root, IReadOnlyCollection<PanelTool> renderedTools)
    {
        var rendered = renderedTools.ToHashSet();
        var pinnedByEdge = new[]
        {
            (Alignment.Left, root.LeftPinnedDockables),
            (Alignment.Right, root.RightPinnedDockables),
            (Alignment.Top, root.TopPinnedDockables),
            (Alignment.Bottom, root.BottomPinnedDockables),
        };
        var missing = pinnedByEdge
            .Where(entry => entry.Item2 is not null)
            .SelectMany(entry => entry.Item2!
                .SelectMany(DescendantTools)
                .Select(tool => (Tool: tool, Edge: entry.Item1)))
            .Where(entry => !rendered.Contains(entry.Tool))
            .GroupBy(entry => entry.Tool)
            .Select(group => group.First())
            .ToList();

        foreach (var (tool, edge) in missing)
        {
            PinToolToEdge(tool, edge);
        }
    }

    public IReadOnlyCollection<PanelTool> GetPinnedTools(IRootDock root) =>
        AllTools.Where(tool => IsPinned(root, tool)).ToArray();

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

    private static ToolDock? FindFirstToolDock(IDock dock)
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
            // Dock may remove the last empty container when every tool is hidden or pinned.
            // Keep the snapshot loadable; pinned restoration will create a staging ToolDock.
            Root = rootChild is null
                ? new DockNodeSnapshot { Kind = "Splitter", Id = "EmptyLayoutPlaceholder" }
                : BuildNode(rootChild),
            HiddenToolIds = HiddenTools.Select(t => t.Id!).ToList(),
            PinnedTools = PinnedTools(root),
        };
    }

    private List<PinnedToolSnapshot> PinnedTools(IRootDock root)
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
            // Dock can wrap an auto-hidden tool in a temporary preview dock. Persist the actual
            // panel rather than assuming every root pinned entry is directly a PanelTool.
            .SelectMany(e => e.Item2!.SelectMany(DescendantTools).Select(tool =>
            {
                var owner = tool.Owner as ToolDock
                    ?? tool.OriginalOwner as ToolDock
                    ?? (_lastOwners.TryGetValue(tool, out var rememberedOwner) ? rememberedOwner : null);
                return new PinnedToolSnapshot
                {
                    Id = tool.Id!,
                    // Anonymous docks are common after drag/drop. An empty id cannot identify
                    // their type on restore and used to resolve to an unrelated ProportionalDock.
                    OwnerId = string.IsNullOrWhiteSpace(owner?.Id) ? null : owner.Id,
                    Edge = e.Item1.ToString(),
                };
            }))
            .GroupBy(pin => pin.Id)
            .Select(group => group.First())
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
        ToolDock? fallbackOwner = null;
        foreach (var pin in pinnedTools)
        {
            if (!toolsById.TryGetValue(pin.Id, out var tool))
            {
                continue;
            }

            // Only a ToolDock is a valid staging owner for PinDockable. Older snapshots may
            // contain OwnerId="" from an anonymous dock; treating that as a generic dock id can
            // select an anonymous ProportionalDock and silently lose the side tab.
            var owner = (!string.IsNullOrWhiteSpace(pin.OwnerId)
                    ? FindToolDockById(root, pin.OwnerId)
                    : null)
                ?? FindFirstToolDock(root)
                ?? (fallbackOwner ??= CreatePinnedRestoreDock(root));

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

    /// <summary>
    /// A valid snapshot can contain only hidden and pinned tools, leaving no ToolDock in its
    /// visible tree. Dock still needs a real ToolDock as the staging owner for PinDockable; using
    /// the root directly makes the operation a silent no-op.
    /// </summary>
    private ToolDock CreatePinnedRestoreDock(IRootDock root)
    {
        var owner = new ToolDock
        {
            Id = "PinnedRestorePane",
            Alignment = Alignment.Left,
            Proportion = 0.2,
            VisibleDockables = CreateList<IDockable>(),
        };
        AddDockable(root, owner);
        return owner;
    }

    private static ToolDock? FindToolDockById(IDock dock, string id)
    {
        if (dock is ToolDock toolDock && string.Equals(toolDock.Id, id, StringComparison.Ordinal))
        {
            return toolDock;
        }

        foreach (var child in dock.VisibleDockables ?? Enumerable.Empty<IDockable>())
        {
            if (child is IDock nested && FindToolDockById(nested, id) is { } found)
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
