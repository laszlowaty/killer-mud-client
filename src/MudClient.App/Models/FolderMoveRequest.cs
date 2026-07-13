namespace MudClient.App.Models;

/// <summary>A drag-and-drop request to place an item or folder in a folder.</summary>
public sealed record FolderMoveRequest(object Source, FolderNode Target);
