namespace MudClient.App.Docking;

/// <summary>Serializable snapshot of the dock tree, keyed by the stable <see cref="PanelTool"/> ids.</summary>
public sealed class DockLayoutSnapshot
{
    public DockNodeSnapshot? Root { get; set; }

    public List<string> HiddenToolIds { get; set; } = new();

    /// <summary>Tools the user has auto-hidden (pinned to an edge). These live outside the
    /// visible tree in the root's pinned collections, so they are tracked separately.</summary>
    public List<PinnedToolSnapshot> PinnedTools { get; set; } = new();
}

/// <summary>An auto-hidden (pinned) tool and the tool dock it should snap back to.</summary>
public sealed class PinnedToolSnapshot
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Id of the ToolDock the tool was pinned from; its alignment picks the pin edge.</summary>
    public string? OwnerId { get; set; }
}

public sealed class DockNodeSnapshot
{
    /// <summary>"Root" | "Proportional" | "ToolDock" | "Splitter" | "Panel".</summary>
    public string Kind { get; set; } = string.Empty;

    public string? Id { get; set; }

    public double Proportion { get; set; } = double.NaN;

    /// <summary>Orientation.ToString(), only set for "Proportional" nodes.</summary>
    public string? Orientation { get; set; }

    /// <summary>Id of the active child, only set for "ToolDock"/"Root" nodes.</summary>
    public string? ActiveDockableId { get; set; }

    public List<DockNodeSnapshot> Children { get; set; } = new();
}
