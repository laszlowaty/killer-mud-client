using MudClient.Core.Map;

namespace MudClient.Core.Tests;

public sealed class MapIndexTests
{
    private static async Task<MapIndex> BuildIndexAsync()
    {
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, MapTestFixture.SampleJson);

        try
        {
            var loader = new MapLoader();
            var result = await loader.LoadAsync(path);
            return new MapIndex(result.Document);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task IndexesRoomsById()
    {
        var index = await BuildIndexAsync();

        Assert.True(index.RoomsById.ContainsKey(1));
        Assert.Equal("Las", index.RoomsById[1].Name);
    }

    [Fact]
    public async Task IndexesRoomsByVnum()
    {
        var index = await BuildIndexAsync();

        Assert.True(index.RoomsByVnum.ContainsKey("1001"));
        Assert.Equal(1, index.RoomsByVnum["1001"][0].Id);
    }

    [Fact]
    public async Task RoomIdAndVnumAreDistinctLookups()
    {
        var index = await BuildIndexAsync();

        // exitId of room 1 is 2, which is a room id, not a vnum.
        var room1 = index.RoomsById[1];
        var exit = room1.Exits.Single();
        Assert.True(index.RoomsById.ContainsKey(exit.ExitId));
        Assert.False(index.RoomsByVnum.ContainsKey(exit.ExitId.ToString()));
    }

    [Fact]
    public async Task GroupsRoomsByAreaAndZ()
    {
        var index = await BuildIndexAsync();

        var group = index.RoomsByAreaAndZ[(1, 0)];
        Assert.Contains(group, r => r.Id == 1);
        Assert.Contains(group, r => r.Id == 2);
    }

    [Fact]
    public async Task DetectsCollisionGroups()
    {
        var index = await BuildIndexAsync();

        var room4 = index.RoomsById[4];
        var collision = index.GetCollisionGroup(room4);

        Assert.NotNull(collision);
        Assert.True(collision!.HasCollision);
        Assert.Equal(2, collision.Rooms.Count);
    }

    [Fact]
    public async Task SpatialIndexReturnsOnlyRoomsInBounds()
    {
        var index = await BuildIndexAsync();

        var rooms = index.GetRoomsInBounds(1, 0, -1, -1, 1, 1).ToList();

        Assert.Contains(rooms, r => r.Id == 1);
        Assert.Contains(rooms, r => r.Id == 2);
        Assert.DoesNotContain(rooms, r => r.Id == 4);
    }
}
