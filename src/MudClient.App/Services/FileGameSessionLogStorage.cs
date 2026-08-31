namespace MudClient.App.Services;

/// <summary>Creates session logs in an ordinary desktop file-system folder.</summary>
public sealed class FileGameSessionLogStorage : IGameSessionLogStorage
{
    public bool SupportsFolder(string folderIdentifier) =>
        !string.IsNullOrWhiteSpace(folderIdentifier)
        && Path.IsPathFullyQualified(folderIdentifier);

    public Task<Stream> CreateFileAsync(
        string folderIdentifier,
        string fileName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(folderIdentifier);

        var path = Path.Combine(folderIdentifier, fileName);
        Stream stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        return Task.FromResult(stream);
    }
}
