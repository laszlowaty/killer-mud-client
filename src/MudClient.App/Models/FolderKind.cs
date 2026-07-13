namespace MudClient.App.Models;

/// <summary>
/// The domain a folder (and its items) belongs to. A folder only ever groups
/// items of a single kind, and items may only be moved between folders of a
/// matching kind. Aliases and triggers are separate kinds even though they
/// share the <see cref="AutomationRuleEntry"/> model.
/// </summary>
public enum FolderKind
{
    Timers,
    Aliases,
    Triggers,
    Notes,
    Autowalk,
}
