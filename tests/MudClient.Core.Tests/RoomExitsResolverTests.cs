using MudClient.Core.Gmcp;
using MudClient.Core.Map;

namespace MudClient.Core.Tests;

public sealed class RoomExitsResolverTests
{
    [Fact]
    public void Process_ParsesExitsWithDoorsAndNames()
    {
        var resolver = new RoomExitsResolver();

        resolver.Process(new GmcpMessage(
            "Room.Info",
            """
            {
              "name": "Pokój do wynajęcia w Arras",
              "num": 6018,
              "sector": "wewnątrz",
              "exits": [
                { "dir": "W", "name": "", "door": true, "closed": true },
                { "dir": "N", "name": "brama", "door": true, "closed": false },
                { "dir": "S" }
              ]
            }
            """));

        Assert.Collection(
            resolver.CurrentExits,
            exit =>
            {
                Assert.Equal("W", exit.Dir);
                Assert.Null(exit.Name);
                Assert.True(exit.HasDoor);
                Assert.True(exit.IsClosed);
            },
            exit =>
            {
                Assert.Equal("N", exit.Dir);
                Assert.Equal("brama", exit.Name);
                Assert.True(exit.HasDoor);
                Assert.False(exit.IsClosed);
            },
            exit =>
            {
                Assert.Equal("S", exit.Dir);
                Assert.False(exit.HasDoor);
                Assert.False(exit.IsClosed);
            });
    }

    [Fact]
    public void Process_IgnoresOtherPackagesAndArrayShapedRoomInfo()
    {
        var resolver = new RoomExitsResolver();

        resolver.Process(new GmcpMessage(
            "Room.Info",
            """{ "exits": [ { "dir": "N" } ] }"""));
        Assert.Single(resolver.CurrentExits);

        // Array-shaped Room.Info (people list) and other packages must not
        // clear the last known exits.
        resolver.Process(new GmcpMessage("Room.Info", """[ { "name": "krasnolud" } ]"""));
        resolver.Process(new GmcpMessage("Char.Vitals", """{ "hp": 10 }"""));
        resolver.Process(new GmcpMessage("Room.Info", "not json"));
        Assert.Single(resolver.CurrentExits);
    }

    [Fact]
    public void Process_RaisesExitsChanged()
    {
        var resolver = new RoomExitsResolver();
        IReadOnlyList<RoomExitInfo>? received = null;
        resolver.ExitsChanged += exits => received = exits;

        resolver.Process(new GmcpMessage("Room.Info", """{ "exits": [] }"""));

        Assert.NotNull(received);
        Assert.Empty(received);
    }
}
