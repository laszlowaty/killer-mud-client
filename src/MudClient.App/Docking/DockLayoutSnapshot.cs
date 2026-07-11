namespace MudClient.App.Docking;

/// <summary>Serializable snapshot of the dock tree, keyed by the stable <see cref="PanelTool"/> ids.</summary>
public sealed class DockLayoutSnapshot
{
    public DockNodeSnapshot? Root { get; set; }

    public List<string> HiddenToolIds { get; set; } = new();
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
