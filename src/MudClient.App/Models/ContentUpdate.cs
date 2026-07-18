namespace MudClient.App.Models;

public sealed record ContentComponentUpdate(
    string Name,
    string Version,
    Uri DownloadUri,
    long Size,
    string Sha256);

public sealed record ContentUpdateAvailability(
    string Release,
    IReadOnlyList<ContentComponentUpdate> Components)
{
    public long DownloadSize => Components.Sum(component => component.Size);
}

public sealed record ContentUpdateProgress(
    string ComponentName,
    long BytesReceived,
    long TotalBytes);

public sealed record ContentInstallResult(
    string Release,
    IReadOnlyList<string> InstalledComponents);
