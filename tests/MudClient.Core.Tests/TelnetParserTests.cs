using MudClient.Core.Telnet;

namespace MudClient.Core.Tests;

public sealed class TelnetParserTests
{
    [Fact]
    public void ParsesPlainData()
    {
        var parser = new TelnetParser();

        var tokens = parser.Feed("hello"u8);

        var data = Assert.Single(tokens);
        Assert.Equal("hello"u8.ToArray(), Assert.IsType<TelnetDataToken>(data).Data);
    }

    [Fact]
    public void ParsesEscapedIacAsData()
    {
        var parser = new TelnetParser();
        byte[] input = [
            (byte)'a',
            TelnetConstants.Iac,
            TelnetConstants.Iac,
            (byte)'b',
        ];

        var tokens = parser.Feed(input);

        var data = Assert.Single(tokens);
        Assert.Equal(
            new byte[] { (byte)'a', TelnetConstants.Iac, (byte)'b' },
            Assert.IsType<TelnetDataToken>(data).Data);
    }

    [Fact]
    public void KeepsNegotiationStateAcrossTcpReads()
    {
        var parser = new TelnetParser();

        Assert.Empty(parser.Feed([TelnetConstants.Iac, TelnetConstants.Will]));
        var tokens = parser.Feed([TelnetConstants.Gmcp]);

        var negotiation = Assert.IsType<TelnetNegotiationToken>(Assert.Single(tokens));
        Assert.Equal(TelnetConstants.Will, negotiation.Command);
        Assert.Equal(TelnetConstants.Gmcp, negotiation.Option);
    }

    [Fact]
    public void ParsesGmcpSubnegotiationSplitAcrossReads()
    {
        var parser = new TelnetParser();

        Assert.Empty(parser.Feed([
            TelnetConstants.Iac,
            TelnetConstants.Sb,
            TelnetConstants.Gmcp,
            (byte)'C',
            (byte)'h',
        ]));

        var tokens = parser.Feed([
            (byte)'a',
            (byte)'r',
            TelnetConstants.Iac,
            TelnetConstants.Se,
        ]);

        var subnegotiation = Assert.IsType<TelnetSubnegotiationToken>(Assert.Single(tokens));
        Assert.Equal(TelnetConstants.Gmcp, subnegotiation.Option);
        Assert.Equal("Char"u8.ToArray(), subnegotiation.Data);
    }
}
