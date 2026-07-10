using MudClient.Core.Gmcp;

namespace MudClient.Core.Tests;

public sealed class GmcpMessageTests
{
    [Fact]
    public void SplitsPackageAndJson()
    {
        var message = GmcpMessage.Parse("Char.Vitals {\"hp\":123}"u8);

        Assert.Equal("Char.Vitals", message.Package);
        Assert.Equal("{\"hp\":123}", message.Json);
    }

    [Fact]
    public void AcceptsPackageWithoutPayload()
    {
        var message = GmcpMessage.Parse("Core.Ping"u8);

        Assert.Equal("Core.Ping", message.Package);
        Assert.Empty(message.Json);
    }
}
