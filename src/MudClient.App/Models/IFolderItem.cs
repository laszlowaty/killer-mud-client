using System.ComponentModel;

namespace MudClient.App.Models;

/// <summary>
/// An entry that can live inside a <see cref="FolderNode"/>. Membership is
/// stored on the item itself (<see cref="FolderId"/>); a null value means the
/// item is loose (rendered at the root of its section).
/// </summary>
/// <remarks>
/// <see cref="IsGlobal"/> is kept in sync with the item's containing folder by
/// the cascade logic in the view model: an item inside a global folder is
/// itself global (stored in the shared _global file). Loose items keep their
/// own flag. Implementers are also <see cref="INotifyPropertyChanged"/> so the
/// UI reflects membership/global changes made by drag &amp; drop.
/// </remarks>
public interface IFolderItem : INotifyPropertyChanged
{
    /// <summary>Id of the containing folder, or null when loose.</summary>
    string? FolderId { get; set; }

    /// <summary>True = shared by all profiles (stored in the global file).</summary>
    bool IsGlobal { get; set; }
}
