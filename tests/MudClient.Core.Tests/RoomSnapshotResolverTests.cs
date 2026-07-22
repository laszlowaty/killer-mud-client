using MudClient.Core.Gmcp;
using MudClient.Core.Map;

namespace MudClient.Core.Tests;

public sealed class RoomSnapshotResolverTests
{
    [Fact]
    public void ParsesCompleteObjectRoomInfo()
    {
        var resolver = new RoomSnapshotResolver();
        RoomSnapshot? received = null;
        resolver.SnapshotReceived += snapshot => received = snapshot;

        resolver.Process(new GmcpMessage(
            "Room.Info",
            """
            {
              "num": 6018,
              "name": "Pokój do wynajęcia",
              "sector": "wewnątrz",
              "exits": [
                { "dir": "W", "name": "", "door": true, "closed": true },
                { "dir": "N", "name": "brama", "door": false }
              ]
            }
            """));

        Assert.NotNull(received);
        Assert.Equal("6018", received.Vnum);
        Assert.Equal("Pokój do wynajęcia", received.Name);
        Assert.Equal("wewnątrz", received.Sector);
        Assert.Equal("W", received.Exits[0].Command);
        Assert.True(received.Exits[0].HasDoor);
        Assert.True(received.Exits[0].IsClosed);
        Assert.Equal("brama", received.Exits[1].Command);
    }

    [Fact]
    public void RepeatedVnumStillPublishesSnapshot()
    {
        var resolver = new RoomSnapshotResolver();
        var count = 0;
        resolver.SnapshotReceived += _ => count++;
        var message = new GmcpMessage("Room.Info", """{"num":"1","exits":[]}""");

        resolver.Process(message);
        resolver.Process(message);

        Assert.Equal(2, count);
    }

    [Fact]
    public void IgnoresPeopleArrayAndMalformedJson()
    {
        var resolver = new RoomSnapshotResolver();
        var count = 0;
        resolver.SnapshotReceived += _ => count++;

        resolver.Process(new GmcpMessage("Room.Info", """[{"name":"strażnik"}]"""));
        resolver.Process(new GmcpMessage("Room.Info", "not-json"));

        Assert.Equal(0, count);
        Assert.Null(resolver.Current);
    }
}
