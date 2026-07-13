namespace MudClient.App.Models;

/// <summary>An item whose active state contributes to its containing folder.</summary>
public interface IActivatableFolderItem : IFolderItem
{
    bool IsEnabled { get; set; }
}
