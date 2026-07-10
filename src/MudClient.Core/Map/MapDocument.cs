namespace MudClient.Core.Map;

public sealed class MapDocument
{
    public string? AnonymousAreaName { get; init; }

    public IReadOnlyList<MapArea> Areas { get; init; } = [];
}
