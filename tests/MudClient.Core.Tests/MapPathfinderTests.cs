using System.Text.Json;
using MudClient.Core.Map;

namespace MudClient.Core.Tests;

public sealed class MapPathfinderTests
{
    private static MapRoom Room(int id, double? weight = null, string? vnum = null, params (string Name, int Target)[] exits) =>
        new()
        {
            Id = id,
            AreaId = 1,
            Coordinates = new MapCoordinates(0, 0, 0),
            Weight = weight,
            UserData = vnum is null
                ? null
                : new Dictionary<string, JsonElement>
                {
                    ["vnum"] = JsonSerializer.SerializeToElement(vnum),
                },
            Exits = exits
                .Select(e => new MapExit { ExitId = e.Target, Name = e.Name })
                .ToList(),
        };

    private static MapPathfinder Build(params MapRoom[] rooms)
    {
        var document = new MapDocument
        {
            Areas = [new MapArea { Id = 1, Rooms = rooms }],
        };

        return new MapPathfinder(new MapIndex(document));
    }

    [Fact]
    public void FindPath_ReturnsShortestSequenceOfCommands()
    {
        var pathfinder = Build(
            Room(1, exits: [("north", 2)]),
            Room(2, exits: [("east", 3), ("south", 1)]),
            Room(3, exits: [("west", 2)]));

        var path = pathfinder.FindPath(1, 3);

        Assert.NotNull(path);
        Assert.Equal(["north", "east"], path.Steps.Select(s => s.Command));
        Assert.Equal(3, path.To.Id);
    }

    [Fact]
    public void FindPath_PrefersLowerWeightRoute()
    {
        // Direct hop 1->3 through a heavy room (weight 10) vs a detour
        // through two light rooms — the detour must win.
        var pathfinder = Build(
            Room(1, exits: [("east", 4), ("north", 2)]),
            Room(4, weight: 10, exits: [("east", 3)]),
            Room(2, exits: [("east", 5)]),
            Room(5, exits: [("south", 3)]),
            Room(3));

        var path = pathfinder.FindPath(1, 3);

        Assert.NotNull(path);
        Assert.Equal(["north", "east", "south"], path.Steps.Select(s => s.Command));
        Assert.Equal(3, path.TotalCost);
    }

    [Fact]
    public void FindPath_RespectsDirectedExits()
    {
        // 2 -> 1 exists, but 1 -> 2 does not.
        var pathfinder = Build(
            Room(1),
            Room(2, exits: [("south", 1)]));

        Assert.Null(pathfinder.FindPath(1, 2));
        Assert.NotNull(pathfinder.FindPath(2, 1));
    }

    [Fact]
    public void FindPath_SameRoom_ReturnsEmptyPath()
    {
        var pathfinder = Build(Room(1));

        var path = pathfinder.FindPath(1, 1);

        Assert.NotNull(path);
        Assert.Empty(path.Steps);
        Assert.Equal(0, path.TotalCost);
    }

    [Fact]
    public void FindPath_UnknownRoomOrNoRoute_ReturnsNull()
    {
        var pathfinder = Build(Room(1), Room(2));

        Assert.Null(pathfinder.FindPath(1, 99));
        Assert.Null(pathfinder.FindPath(99, 1));
        Assert.Null(pathfinder.FindPath(1, 2));
    }

    [Fact]
    public void FindPath_IgnoresExitsToRoomsOutsideTheMap()
    {
        var pathfinder = Build(
            Room(1, exits: [("north", 777), ("east", 2)]),
            Room(2));

        var path = pathfinder.FindPath(1, 2);

        Assert.NotNull(path);
        Assert.Equal(["east"], path.Steps.Select(s => s.Command));
    }

    [Fact]
    public void FindPath_CarriesDoorNameFromMapExit()
    {
        var room1 = new MapRoom
        {
            Id = 1,
            AreaId = 1,
            Coordinates = new MapCoordinates(0, 0, 0),
            Exits = [new MapExit { ExitId = 2, Name = "west", Door = "drzwi" }],
        };

        var pathfinder = Build(room1, Room(2));

        var path = pathfinder.FindPath(1, 2);

        Assert.NotNull(path);
        var step = Assert.Single(path.Steps);
        Assert.Equal("west", step.Command);
        Assert.Equal("drzwi", step.Door);
    }

    [Fact]
    public void FindPathByVnum_ResolvesRoomsThroughUserData()
    {
        var pathfinder = Build(
            Room(1, vnum: "100", exits: [("north", 2)]),
            Room(2, vnum: "200"));

        var path = pathfinder.FindPathByVnum("100", "200");

        Assert.NotNull(path);
        Assert.Equal(["north"], path.Steps.Select(s => s.Command));
        Assert.Null(pathfinder.FindPathByVnum("100", "999"));
    }
}
