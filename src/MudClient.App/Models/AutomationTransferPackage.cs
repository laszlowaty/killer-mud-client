namespace MudClient.App.Models;

/// <summary>
/// Versioned JSON interchange format for aliases, triggers and timers. A
/// package may contain one loose item or an entire folder subtree.
/// </summary>
public sealed class AutomationTransferPackage
{
    public int Version { get; set; } = 1;

    public FolderKind Kind { get; set; }

    public List<ProfileFolder> Folders { get; set; } = [];

    public List<ProfileRule> Aliases { get; set; } = [];

    public List<ProfileRule> Triggers { get; set; } = [];

    public List<ProfileTimer> Timers { get; set; } = [];
}
