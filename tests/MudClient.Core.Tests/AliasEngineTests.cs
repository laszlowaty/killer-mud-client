using MudClient.Core.Automation;

namespace MudClient.Core.Tests;

public sealed class AliasEngineTests
{
    [Fact]
    public void ReplacesFirstMatchingAlias()
    {
        var engine = new AliasEngine();
        engine.Add(new AliasRule("look", "^l$", "look"));

        Assert.Equal("look", engine.Process("l"));
    }

    [Fact]
    public void SupportsRegexCaptureGroups()
    {
        var engine = new AliasEngine();
        engine.Add(new AliasRule("tell", "^t (.+) (.+)$", "tell $1 $2"));

        Assert.Equal("tell bob hello", engine.Process("t bob hello"));
    }

    [Fact]
    public void ReturnsOriginalCommandWhenNothingMatches()
    {
        var engine = new AliasEngine();

        Assert.Equal("north", engine.Process("north"));
    }
}
