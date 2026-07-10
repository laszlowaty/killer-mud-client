using MudClient.Core.Gmcp;
using MudClient.Core.Map;

namespace MudClient.Core.Tests;

public sealed class GmcpLocationResolverTests
{
    [Fact]
    public void ResolvesVnumFromStringProperty()
    {
        var resolver = new GmcpLocationResolver();
        string? changed = null;
        resolver.LocationChanged += v => changed = v;

        resolver.Process(new GmcpMessage("Room.Info", "{\"vnum\":\"1234\"}"));

        Assert.Equal("1234", changed);
        Assert.Equal("1234", resolver.CurrentVnum);
    }

    [Fact]
    public void ResolvesVnumFromNumberProperty()
    {
        var resolver = new GmcpLocationResolver();

        resolver.Process(new GmcpMessage("Room.Info", "{\"num\":5678}"));

        Assert.Equal("5678", resolver.CurrentVnum);
    }

    [Fact]
    public void ResolvesNestedRoomVnumPath()
    {
        var resolver = new GmcpLocationResolver();

        resolver.Process(new GmcpMessage("Room.Info", "{\"room\":{\"vnum\":\"999\"}}"));

        Assert.Equal("999", resolver.CurrentVnum);
    }

    [Fact]
    public void InvalidJsonDoesNotThrow()
    {
        var resolver = new GmcpLocationResolver();

        var exception = Record.Exception(() => resolver.Process(new GmcpMessage("Room.Info", "{ not json")));

        Assert.Null(exception);
        Assert.Null(resolver.CurrentVnum);
    }

    [Fact]
    public void IgnoresMessagesFromOtherPackages()
    {
        var resolver = new GmcpLocationResolver();

        resolver.Process(new GmcpMessage("Char.Vitals", "{\"vnum\":\"1\"}"));

        Assert.Null(resolver.CurrentVnum);
    }

    [Fact]
    public void DoesNotEmitWhenVnumUnchanged()
    {
        var resolver = new GmcpLocationResolver();
        var callCount = 0;
        resolver.LocationChanged += _ => callCount++;

        resolver.Process(new GmcpMessage("Room.Info", "{\"vnum\":\"1\"}"));
        resolver.Process(new GmcpMessage("Room.Info", "{\"vnum\":\"1\"}"));

        Assert.Equal(1, callCount);
    }

    [Fact]
    public void CustomPackageAndPathAreConfigurable()
    {
        var settings = new MapGmcpLocationSettings
        {
            Packages = ["Custom.Location"],
            VnumPaths = ["location.num"],
        };
        var resolver = new GmcpLocationResolver(settings);

        resolver.Process(new GmcpMessage("Custom.Location", "{\"location\":{\"num\":42}}"));

        Assert.Equal("42", resolver.CurrentVnum);
    }
}
