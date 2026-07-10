using MudClient.Core.Map;

namespace MudClient.Core.Tests;

public sealed class CollisionLayoutServiceTests
{
    private static MapRoom MakeRoom(int id, MapCoordinates coordinates, IReadOnlyList<MapExit>? exits = null) =>
        new()
        {
            Id = id,
            AreaId = 1,
            Coordinates = coordinates,
            Exits = exits ?? [],
        };

    [Fact]
    public void TwoRoomsWithoutConnectionsGetDistinctOffsets()
    {
        var coords = new MapCoordinates(0, 0, 0);
        var rooms = new List<MapRoom> { MakeRoom(1, coords), MakeRoom(2, coords) };
        var group = new MapCollisionGroup { Cell = new MapCellKey(1, 0, 0, 0), Rooms = rooms };

        var layout = new CollisionLayoutService().ComputeLayout(group);

        Assert.NotEqual(layout[1], layout[2]);
    }

    [Fact]
    public void ThreeRoomsConnectedEastWestLineUp()
    {
        var coords = new MapCoordinates(0, 0, 0);
        var rooms = new List<MapRoom>
        {
            MakeRoom(1, coords, [new MapExit { ExitId = 2, Name = "east" }]),
            MakeRoom(2, coords, [
                new MapExit { ExitId = 1, Name = "west" },
                new MapExit { ExitId = 3, Name = "east" },
            ]),
            MakeRoom(3, coords, [new MapExit { ExitId = 2, Name = "west" }]),
        };
        var group = new MapCollisionGroup { Cell = new MapCellKey(1, 0, 0, 0), Rooms = rooms };

        var layout = new CollisionLayoutService().ComputeLayout(group, currentRoomId: 1);

        Assert.Equal(new MapOffset(0, 0), layout[1]);
        Assert.Equal(new MapOffset(1, 0), layout[2]);
        Assert.Equal(new MapOffset(2, 0), layout[3]);
    }

    [Fact]
    public void NineRoomsWithoutExitsAllGetDistinctOffsets()
    {
        var coords = new MapCoordinates(5, 5, 0);
        var rooms = Enumerable.Range(1, 9).Select(id => MakeRoom(id, coords)).ToList();
        var group = new MapCollisionGroup { Cell = new MapCellKey(1, 5, 5, 0), Rooms = rooms };

        var layout = new CollisionLayoutService().ComputeLayout(group);

        Assert.Equal(9, layout.Count);
        Assert.Equal(9, layout.Values.Distinct().Count());
    }

    [Fact]
    public void CurrentRoomInsideCollisionBecomesLayoutOrigin()
    {
        var coords = new MapCoordinates(0, 0, 0);
        var rooms = new List<MapRoom>
        {
            MakeRoom(10, coords),
            MakeRoom(11, coords),
        };
        var group = new MapCollisionGroup { Cell = new MapCellKey(1, 0, 0, 0), Rooms = rooms };

        var layout = new CollisionLayoutService().ComputeLayout(group, currentRoomId: 11);

        Assert.Equal(MapOffset.Zero, layout[11]);
    }

    [Fact]
    public void LayoutDoesNotModifyOriginalCoordinates()
    {
        var coords = new MapCoordinates(3, 4, 0);
        var rooms = new List<MapRoom> { MakeRoom(1, coords), MakeRoom(2, coords) };
        var group = new MapCollisionGroup { Cell = new MapCellKey(1, 3, 4, 0), Rooms = rooms };

        _ = new CollisionLayoutService().ComputeLayout(group);

        Assert.Equal(coords, rooms[0].Coordinates);
        Assert.Equal(coords, rooms[1].Coordinates);
    }
}
