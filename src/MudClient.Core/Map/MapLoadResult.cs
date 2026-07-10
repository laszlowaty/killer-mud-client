namespace MudClient.Core.Map;

public sealed class MapLoadResult
{
    public required MapDocument Document { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = [];
}
