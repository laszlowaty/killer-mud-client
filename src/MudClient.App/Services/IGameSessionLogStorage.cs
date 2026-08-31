namespace MudClient.App.Services;

/// <summary>Opens a new session log in a user-selected, platform-specific folder.</summary>
public interface IGameSessionLogStorage
{
    bool SupportsFolder(string folderIdentifier);

    Task<Stream> CreateFileAsync(
        string folderIdentifier,
        string fileName,
        CancellationToken cancellationToken);
}
