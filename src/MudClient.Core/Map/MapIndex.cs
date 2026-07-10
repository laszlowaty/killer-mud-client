namespace MudClient.Core.Map;

public sealed class MapIndex
{
    private readonly Dictionary<MapSpatialBucketKey, List<MapRoom>> _spatialBuckets;

    public MapIndex(MapDocument document, int spatialBucketSize = 32)
    {
        Document = document;
        SpatialBucketSize = spatialBucketSize <= 0 ? 32 : spatialBucketSize;

        var roomsById = new Dictionary<int, MapRoom>();
        var roomsByVnum = new Dictionary<string, List<MapRoom>>();
        var areasById = new Dictionary<int, MapArea>();
        var roomsByAreaAndZ = new Dictionary<(int AreaId, double Z), List<MapRoom>>();
        var collisionGroups = new Dictionary<MapCellKey, List<MapRoom>>();
        _spatialBuckets = new Dictionary<MapSpatialBucketKey, List<MapRoom>>();

        foreach (var area in document.Areas)
        {
            areasById[area.Id] = area;

            foreach (var room in area.Rooms)
            {
                roomsById[room.Id] = room;

                var vnum = room.Vnum;
                if (vnum is not null)
                {
                    if (!roomsByVnum.TryGetValue(vnum, out var list))
                    {
                        list = [];
                        roomsByVnum[vnum] = list;
                    }

                    list.Add(room);
                }

                var areaZKey = (room.AreaId, room.Coordinates.Z);
                if (!roomsByAreaAndZ.TryGetValue(areaZKey, out var areaZList))
                {
                    areaZList = [];
                    roomsByAreaAndZ[areaZKey] = areaZList;
                }

                areaZList.Add(room);

                var cellKey = new MapCellKey(room.AreaId, room.Coordinates.X, room.Coordinates.Y, room.Coordinates.Z);
                if (!collisionGroups.TryGetValue(cellKey, out var cellList))
                {
                    cellList = [];
                    collisionGroups[cellKey] = cellList;
                }

                cellList.Add(room);

                var bucketKey = new MapSpatialBucketKey(
                    room.AreaId,
                    room.Coordinates.Z,
                    (long)Math.Floor(room.Coordinates.X / SpatialBucketSize),
                    (long)Math.Floor(room.Coordinates.Y / SpatialBucketSize));

                if (!_spatialBuckets.TryGetValue(bucketKey, out var bucketList))
                {
                    bucketList = [];
                    _spatialBuckets[bucketKey] = bucketList;
                }

                bucketList.Add(room);
            }
        }

        RoomsById = roomsById;
        RoomsByVnum = roomsByVnum.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<MapRoom>)pair.Value);
        AreasById = areasById;
        RoomsByAreaAndZ = roomsByAreaAndZ.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<MapRoom>)pair.Value);
        CollisionGroups = collisionGroups.ToDictionary(
            pair => pair.Key,
            pair => new MapCollisionGroup { Cell = pair.Key, Rooms = pair.Value });
    }

    public MapDocument Document { get; }

    public int SpatialBucketSize { get; }

    public IReadOnlyDictionary<int, MapRoom> RoomsById { get; }

    public IReadOnlyDictionary<string, IReadOnlyList<MapRoom>> RoomsByVnum { get; }

    public IReadOnlyDictionary<int, MapArea> AreasById { get; }

    public IReadOnlyDictionary<(int AreaId, double Z), IReadOnlyList<MapRoom>> RoomsByAreaAndZ { get; }

    public IReadOnlyDictionary<MapCellKey, MapCollisionGroup> CollisionGroups { get; }

    public MapRoom? FindFirstRoomByVnum(string vnum) =>
        RoomsByVnum.TryGetValue(vnum, out var rooms) && rooms.Count > 0 ? rooms[0] : null;

    public MapCollisionGroup? GetCollisionGroup(MapRoom room)
    {
        var key = new MapCellKey(room.AreaId, room.Coordinates.X, room.Coordinates.Y, room.Coordinates.Z);
        return CollisionGroups.GetValueOrDefault(key);
    }

    public IEnumerable<MapRoom> GetRoomsInBounds(int areaId, double z, double minX, double minY, double maxX, double maxY)
    {
        var minBucketX = (long)Math.Floor(minX / SpatialBucketSize);
        var maxBucketX = (long)Math.Floor(maxX / SpatialBucketSize);
        var minBucketY = (long)Math.Floor(minY / SpatialBucketSize);
        var maxBucketY = (long)Math.Floor(maxY / SpatialBucketSize);

        for (var bx = minBucketX; bx <= maxBucketX; bx++)
        {
            for (var by = minBucketY; by <= maxBucketY; by++)
            {
                var key = new MapSpatialBucketKey(areaId, z, bx, by);
                if (!_spatialBuckets.TryGetValue(key, out var rooms))
                {
                    continue;
                }

                foreach (var room in rooms)
                {
                    if (room.Coordinates.X >= minX && room.Coordinates.X <= maxX &&
                        room.Coordinates.Y >= minY && room.Coordinates.Y <= maxY)
                    {
                        yield return room;
                    }
                }
            }
        }
    }

    public IEnumerable<double> GetZLevels(int areaId) =>
        RoomsByAreaAndZ.Keys.Where(key => key.AreaId == areaId).Select(key => key.Z).Distinct().OrderBy(z => z);
}
