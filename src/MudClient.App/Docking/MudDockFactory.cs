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
            CanPin = true,
        };
        AllTools.Add(tool);
        return tool;
    }

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
        var autoAssistTool = NewTool("AutoAssist", "⚔ Autoassist", typeof(Views.Panels.AutoAssistPanelView), _mainContext);
        var groupOrdersTool = NewTool("GroupOrders", "📣 Ordery", typeof(Views.Panels.GroupOrdersPanelView), _mainContext);
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
                automationTool, autoAssistTool, groupOrdersTool, autowalkTool, notesTool, gmcpTool, settingsTool),
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

    /// <summary>Re-adds a previously closed panel back to its original dock.</summary>
    public void Restore(PanelTool tool)
    {
        var owner = tool.Owner as IDock
            ?? (_lastOwners.TryGetValue(tool, out var lastOwner) ? lastOwner : null)
            ?? tool.OriginalOwner as IDock;
        if (owner is null)
        {
            return;
        }

        if (!EnsureAttached(owner))
        {
            owner = _lastOwners.TryGetValue(tool, out var fallbackOwner) ? fallbackOwner : null;
            if (owner is null || !EnsureAttached(owner))
            {
                return;
            }
        }

        AddDockable(owner, tool);
        SetActiveDockable(tool);
        SetFocusedDockable(owner, tool);
        HiddenTools.Remove(tool);
    }

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

        // Dock can remove an empty ToolDock without raising a separate close event.
        // In that case restore the panel as a tab in any still-visible tool group.
        var fallback = FindFirstToolDock(_root);
        if (fallback is null || ReferenceEquals(fallback, dock))
        {
            return false;
        }

        _lastOwners.Where(pair => ReferenceEquals(pair.Value, dock))
            .Select(pair => pair.Key)
            .ToList()
            .ForEach(tool => _lastOwners[tool] = fallback);
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
            root.LeftPinnedDockables,
            root.RightPinnedDockables,
            root.TopPinnedDockables,
            root.BottomPinnedDockables,
        };

        return edges
            .Where(list => list is not null)
            .SelectMany(list => list!)
            .OfType<PanelTool>()
            .Select(tool => new PinnedToolSnapshot { Id = tool.Id!, OwnerId = (tool.Owner as IDock)?.Id })
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
            SetActiveDockable(tool);
            PinDockable(tool);
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
