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

    [Fact]
    public void StopsConsumingRightAfterMccp2CompressionStart()
    {
        var parser = new TelnetParser();

        // "ab" IAC SB 86 IAC SE, then bytes that already belong to the zlib stream
        // and must NOT be parsed as Telnet (0x78 0x9C is a typical zlib header).
        byte[] input = [
            (byte)'a',
            (byte)'b',
            TelnetConstants.Iac,
            TelnetConstants.Sb,
            TelnetConstants.Mccp2,
            TelnetConstants.Iac,
            TelnetConstants.Se,
            0x78,
            0x9C,
            TelnetConstants.Iac,
        ];

        var tokens = parser.Feed(input, out var consumed);

        Assert.Equal(7, consumed);
        Assert.Equal(2, tokens.Count);
        Assert.Equal("ab"u8.ToArray(), Assert.IsType<TelnetDataToken>(tokens[0]).Data);
        var subnegotiation = Assert.IsType<TelnetSubnegotiationToken>(tokens[1]);
        Assert.Equal(TelnetConstants.Mccp2, subnegotiation.Option);
        Assert.Empty(subnegotiation.Data);
    }

    [Fact]
    public void ConsumesWholeInputWhenNoCompressionStart()
    {
        var parser = new TelnetParser();

        var tokens = parser.Feed("hello"u8, out var consumed);

        Assert.Equal(5, consumed);
        Assert.Single(tokens);
    }
}
