namespace MudClient.Core.Map;

/// <summary>
/// One step of a computed path: the command to send, the room it leads to
/// and — when the map marks this exit with a door — the raw value of the
/// map's "door" field (a state like "closed", not a door name).
/// </summary>
public sealed record MapPathStep(string Command, MapRoom ToRoom, string? Door = null);

/// <summary>A computed path between two rooms.</summary>
public sealed class MapPath
{
    public required MapRoom From { get; init; }

    public required MapRoom To { get; init; }

    public required IReadOnlyList<MapPathStep> Steps { get; init; }

    public required double TotalCost { get; init; }
}

/// <summary>
/// Weighted shortest-path search over the world map. The room graph is
/// compacted once (CSR layout: dense indices + flat edge arrays) so repeated
/// queries only pay for the Dijkstra itself — no dictionary lookups per edge.
/// </summary>
public sealed class MapPathfinder
{
    private readonly MapIndex _index;
    private readonly Dictionary<int, int> _denseIndexByRoomId;
    private readonly MapRoom[] _rooms;
    private readonly int[] _edgeOffsets;
    private readonly int[] _edgeTargets;
    private readonly string[] _edgeCommands;
    private readonly string?[] _edgeDoors;
    private readonly double[] _edgeCosts;

    public MapPathfinder(MapIndex index)
    {
        _index = index;

        var roomCount = index.RoomsById.Count;
        _rooms = new MapRoom[roomCount];
        _denseIndexByRoomId = new Dictionary<int, int>(roomCount);

        var dense = 0;
        foreach (var room in index.RoomsById.Values)
        {
            _rooms[dense] = room;
            _denseIndexByRoomId[room.Id] = dense;
            dense++;
        }

        // First pass: count valid edges per room to size the CSR arrays.
        _edgeOffsets = new int[roomCount + 1];
        var edgeCount = 0;
        for (var i = 0; i < roomCount; i++)
        {
            _edgeOffsets[i] = edgeCount;
            foreach (var exit in _rooms[i].Exits)
            {
                if (!string.IsNullOrWhiteSpace(exit.Name) && _denseIndexByRoomId.ContainsKey(exit.ExitId))
                {
                    edgeCount++;
                }
            }
        }

        _edgeOffsets[roomCount] = edgeCount;
        _edgeTargets = new int[edgeCount];
        _edgeCommands = new string[edgeCount];
        _edgeDoors = new string?[edgeCount];
        _edgeCosts = new double[edgeCount];

        var edge = 0;
        for (var i = 0; i < roomCount; i++)
        {
            foreach (var exit in _rooms[i].Exits)
            {
                if (string.IsNullOrWhiteSpace(exit.Name) ||
                    !_denseIndexByRoomId.TryGetValue(exit.ExitId, out var target))
                {
                    continue;
                }

                _edgeTargets[edge] = target;
                _edgeCommands[edge] = exit.Name;
                _edgeDoors[edge] = exit.HasDoor ? exit.Door : null;
                _edgeCosts[edge] = EnterCost(_rooms[target]);
                edge++;
            }
        }
    }

    /// <summary>Cost of entering a room; map weights below 1 are treated as 1.</summary>
    private static double EnterCost(MapRoom room) =>
        room.Weight is { } weight && weight >= 1 ? weight : 1;

    public MapPath? FindPath(int fromRoomId, int toRoomId)
    {
        if (!_denseIndexByRoomId.TryGetValue(fromRoomId, out var source) ||
            !_denseIndexByRoomId.TryGetValue(toRoomId, out var target))
        {
            return null;
        }

        var from = _rooms[source];
        var to = _rooms[target];

        if (source == target)
        {
            return new MapPath { From = from, To = to, Steps = [], TotalCost = 0 };
        }

        var distances = new double[_rooms.Length];
        Array.Fill(distances, double.PositiveInfinity);
        distances[source] = 0;

        // previousEdge[i] = index of the CSR edge used to reach room i.
        var previousEdge = new int[_rooms.Length];
        Array.Fill(previousEdge, -1);

        var previousRoom = new int[_rooms.Length];
        var visited = new bool[_rooms.Length];
        var queue = new PriorityQueue<int, double>();
        queue.Enqueue(source, 0);

        while (queue.TryDequeue(out var current, out var distance))
        {
            if (visited[current])
            {
                continue; // Stale queue entry — a shorter path was already settled.
            }

            visited[current] = true;

            if (current == target)
            {
                break;
            }

            var end = _edgeOffsets[current + 1];
            for (var edge = _edgeOffsets[current]; edge < end; edge++)
            {
                var next = _edgeTargets[edge];
                if (visited[next])
                {
                    continue;
                }

                var candidate = distance + _edgeCosts[edge];
                if (candidate < distances[next])
                {
                    distances[next] = candidate;
                    previousEdge[next] = edge;
                    previousRoom[next] = current;
                    queue.Enqueue(next, candidate);
                }
            }
        }

        if (previousEdge[target] < 0)
        {
            return null;
        }

        var steps = new List<MapPathStep>();
        for (var node = target; node != source; node = previousRoom[node])
        {
            var edge = previousEdge[node];
            steps.Add(new MapPathStep(_edgeCommands[edge], _rooms[node], _edgeDoors[edge]));
        }

        steps.Reverse();

        return new MapPath { From = from, To = to, Steps = steps, TotalCost = distances[target] };
    }

    /// <summary>Finds a path between rooms identified by vnum (first match wins).</summary>
    public MapPath? FindPathByVnum(string fromVnum, string toVnum)
    {
        var from = _index.FindFirstRoomByVnum(fromVnum);
        var to = _index.FindFirstRoomByVnum(toVnum);

        return from is null || to is null ? null : FindPath(from.Id, to.Id);
    }
}
