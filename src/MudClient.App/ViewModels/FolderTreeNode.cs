using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MudClient.App.Models;

namespace MudClient.App.ViewModels;

/// <summary>
/// One row in a folder tree: either a folder (with <see cref="Children"/>) or a
/// leaf item (<see cref="Content"/> holds the underlying model, e.g. a
/// <see cref="TimerEntry"/>). Built by the view model and consumed by the
/// reusable <c>FolderTreeView</c> control; the control renders folder chrome
/// itself and each leaf via the panel-supplied item template.
/// </summary>
public sealed partial class FolderTreeNode : ObservableObject
{
    public bool IsFolder { get; init; }

    /// <summary>The folder this row represents, or null for a leaf.</summary>
    public FolderNode? Folder { get; init; }

    /// <summary>The item model this row represents, or null for a folder.</summary>
    public object? Content { get; init; }

    public ObservableCollection<FolderTreeNode> Children { get; } = [];

    /// <summary>Recursive count of item (leaf) descendants — shown on the folder badge.</summary>
    [ObservableProperty]
    private int _itemCount;

    /// <summary>True when this is a folder that currently holds no items or subfolders.</summary>
    public bool IsEmptyFolder => IsFolder && Children.Count == 0;
}
