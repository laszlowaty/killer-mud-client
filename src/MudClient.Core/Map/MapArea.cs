namespace MudClient.Core.Map;

public sealed class MapArea
{
    public required int Id { get; init; }

    public string? Name { get; init; }

    public IReadOnlyList<MapRoom> Rooms { get; init; } = [];
}
