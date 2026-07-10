namespace MudClient.Core.Map;

public sealed class CollisionLayoutService
{
    private static readonly IReadOnlyDictionary<string, MapOffset> DirectionOffsets = new Dictionary<string, MapOffset>(StringComparer.OrdinalIgnoreCase)
    {
        ["north"] = new MapOffset(0, 1),
        ["northeast"] = new MapOffset(1, 1),
        ["east"] = new MapOffset(1, 0),
        ["southeast"] = new MapOffset(1, -1),
        ["south"] = new MapOffset(0, -1),
        ["southwest"] = new MapOffset(-1, -1),
        ["west"] = new MapOffset(-1, 0),
        ["northwest"] = new MapOffset(-1, 1),
    };

    public IReadOnlyDictionary<int, MapOffset> ComputeLayout(MapCollisionGroup group, int? currentRoomId = null)
    {
        ArgumentNullException.ThrowIfNull(group);

        var rooms = group.Rooms;
        var offsets = new Dictionary<int, MapOffset>();

        if (rooms.Count <= 1)
        {
            foreach (var room in rooms)
            {
                offsets[room.Id] = MapOffset.Zero;
            }

            return offsets;
        }

        var roomIds = new HashSet<int>(rooms.Select(r => r.Id));
        var byId = rooms.ToDictionary(r => r.Id);

        var startId = currentRoomId.HasValue && roomIds.Contains(currentRoomId.Value)
            ? currentRoomId.Value
            : rooms.Min(r => r.Id);

        offsets[startId] = MapOffset.Zero;

        var visited = new HashSet<int> { startId };
        var queue = new Queue<int>();
        queue.Enqueue(startId);
        var occupied = new HashSet<MapOffset> { MapOffset.Zero };

        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            var current = byId[currentId];
            var currentOffset = offsets[currentId];

            foreach (var exit in current.Exits.OrderBy(e => e.ExitId))
            {
                if (!roomIds.Contains(exit.ExitId) || visited.Contains(exit.ExitId))
                {
                    continue;
                }

                if (exit.Name is null || !DirectionOffsets.TryGetValue(exit.Name, out var direction))
                {
                    continue;
                }

                var candidate = new MapOffset(currentOffset.X + direction.X, currentOffset.Y + direction.Y);
                if (occupied.Contains(candidate))
                {
                    continue;
                }

                offsets[exit.ExitId] = candidate;
                occupied.Add(candidate);
                visited.Add(exit.ExitId);
                queue.Enqueue(exit.ExitId);
            }
        }

        var unplaced = rooms.Where(r => !visited.Contains(r.Id)).OrderBy(r => r.Id).ToList();
        const double radialStep = 1.4;

        for (var i = 0; i < unplaced.Count; i++)
        {
            var angle = 2 * Math.PI * i / Math.Max(unplaced.Count, 1);
            var radius = radialStep * (1 + i / 8);
            var offset = new MapOffset(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
            offsets[unplaced[i].Id] = offset;
        }

        return offsets;
    }
}
