namespace MudClient.App.Models;

public sealed record AvailableUpdate(
    string Version,
    bool IsPrerelease,
    Uri ReleasePageUri,
    Uri ChangelogUri);
