namespace MudClient.Core.Map;

public sealed class MapCollisionGroup
{
    public required MapCellKey Cell { get; init; }

    public required IReadOnlyList<MapRoom> Rooms { get; init; }

    public bool HasCollision => Rooms.Count > 1;
}
