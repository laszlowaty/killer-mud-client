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

    // ====================================================================
    // ProcessCommands – backward compatible single-line alias
    // ====================================================================

    [Fact]
    public void ProcessCommands_MatchedAlias_SingleLine_ReturnsSingleCommand()
    {
        var engine = new AliasEngine();
        engine.Add(new AliasRule("look", "^l$", "look"));

        var result = engine.ProcessCommands("l");

        var command = Assert.Single(result);
        Assert.Equal("look", command);
    }

    // ====================================================================
    // ProcessCommands – multi-line alias
    // ====================================================================

    [Fact]
    public void ProcessCommands_MatchedAlias_MultiLine_ReturnsMultipleCommandsInOrder()
    {
        var engine = new AliasEngine();
        engine.Add(new AliasRule("multikill", "^mk$", "kill orc\nkill goblin\nkill troll"));

        var result = engine.ProcessCommands("mk");

        Assert.Equal(3, result.Count);
        Assert.Equal("kill orc", result[0]);
        Assert.Equal("kill goblin", result[1]);
        Assert.Equal("kill troll", result[2]);
    }

    // ====================================================================
    // ProcessCommands – blank lines skipped
    // ====================================================================

    [Fact]
    public void ProcessCommands_BlankLines_Skipped()
    {
        var engine = new AliasEngine();
        engine.Add(new AliasRule("buff", "^buff$", "cast shield\n\ncast armor\n \ncast bless"));

        var result = engine.ProcessCommands("buff");

        Assert.Equal(3, result.Count);
        Assert.Equal("cast shield", result[0]);
        Assert.Equal("cast armor", result[1]);
        Assert.Equal("cast bless", result[2]);
    }

    // ====================================================================
    // ProcessCommands – capture groups substituted in multi-line
    // ====================================================================

    [Fact]
    public void ProcessCommands_CaptureGroups_SubstitutedInMultiLine()
    {
        var engine = new AliasEngine();
        engine.Add(new AliasRule("tell", "^t (.+) (.+)$", "say $1 $2\nemote whispers to $1"));

        var result = engine.ProcessCommands("t Gandalf hello");

        Assert.Equal(2, result.Count);
        Assert.Equal("say Gandalf hello", result[0]);
        Assert.Equal("emote whispers to Gandalf", result[1]);
    }

    // ====================================================================
    // ProcessCommands – unmatched input returns original command
    // ====================================================================

    [Fact]
    public void ProcessCommands_UnmatchedInput_ReturnsOriginalCommand()
    {
        var engine = new AliasEngine();

        var result = engine.ProcessCommands("north");

        var command = Assert.Single(result);
        Assert.Equal("north", command);
    }

    // ====================================================================
    // ProcessCommands – first matching rule wins (same as Process)
    // ====================================================================

    [Fact]
    public void ProcessCommands_MultipleRules_OnlyFirstMatchProcessed()
    {
        var engine = new AliasEngine();
        engine.Add(new AliasRule("shortcut", "^l$", "look"));
        engine.Add(new AliasRule("second", "^l$", "listen"));

        var result = engine.ProcessCommands("l");

        var command = Assert.Single(result);
        Assert.Equal("look", command);
    }

    // ====================================================================
    // ProcessCommands – disabled rules skipped
    // ====================================================================

    [Fact]
    public void ProcessCommands_DisabledRule_Skipped()
    {
        var engine = new AliasEngine();
        engine.Add(new AliasRule("enabled", "^n$", "north", enabled: true));
        engine.Add(new AliasRule("disabled", "^n$", "nowhere", enabled: false));

        var result = engine.ProcessCommands("n");

        var command = Assert.Single(result);
        Assert.Equal("north", command);
    }

    // ====================================================================
    // ProcessCommands – trailing whitespace and CR trimmed
    // ====================================================================

    [Fact]
    public void ProcessCommands_WhitespaceTrimmed_PerLine()
    {
        var engine = new AliasEngine();
        engine.Add(new AliasRule("craft", "^craft$", "  craft sword  \n  craft shield  "));

        var result = engine.ProcessCommands("craft");

        Assert.Equal(2, result.Count);
        Assert.Equal("craft sword", result[0]);
        Assert.Equal("craft shield", result[1]);
    }

    [Fact]
    public void ProcessCommands_CarriageReturn_Trimmed()
    {
        var engine = new AliasEngine();
        engine.Add(new AliasRule("test", "^t$", "cmd1\r\ncmd2\r\ncmd3"));

        var result = engine.ProcessCommands("t");

        Assert.Equal(3, result.Count);
        Assert.Equal("cmd1", result[0]);
        Assert.Equal("cmd2", result[1]);
        Assert.Equal("cmd3", result[2]);
    }

    // ====================================================================
    // ProcessCommands – backward compatibility with original Process
    // ====================================================================

    [Fact]
    public void ProcessCommands_SingleLineResult_SameAsProcess()
    {
        var engine = new AliasEngine();
        engine.Add(new AliasRule("look", "^l$", "look"));

        Assert.Equal(engine.Process("l"), engine.ProcessCommands("l").Single());
    }

    [Fact]
    public void ProcessCommands_NoMatch_SameAsProcess()
    {
        var engine = new AliasEngine();

        Assert.Equal(engine.Process("north"), engine.ProcessCommands("north").Single());
    }
}
