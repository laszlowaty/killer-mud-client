using CommunityToolkit.Mvvm.ComponentModel;

namespace MudClient.App.Models;

/// <summary>
/// A folder grouping items (and other folders) of a single <see cref="Kind"/>.
/// Folders form a tree via <see cref="ParentId"/> (null = root). Marking a
/// folder global cascades to every descendant folder and item (see the view
/// model's cascade logic), so all nodes in a global subtree are stored in the
/// shared _global file.
/// </summary>
public sealed partial class FolderNode : ObservableObject
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>Id of the parent folder, or null for a root folder.</summary>
    [ObservableProperty]
    private string? _parentId;

    [ObservableProperty]
    private string _name = string.Empty;

    /// <summary>Domain this folder belongs to; items must match it.</summary>
    public FolderKind Kind { get; init; }

    /// <summary>True = stored in the shared global file, not a profile.</summary>
    [ObservableProperty]
    private bool _isGlobal;

    /// <summary>UI-only: whether the folder is expanded in the tree.</summary>
    [ObservableProperty]
    private bool _isExpanded = true;
}
