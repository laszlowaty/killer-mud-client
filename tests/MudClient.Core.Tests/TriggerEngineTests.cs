using MudClient.Core.Automation;

namespace MudClient.Core.Tests;

public sealed class TriggerEngineTests
{
    // ====================================================================
    // No match
    // ====================================================================

    [Fact]
    public void NoMatch_ReturnsEmptyList()
    {
        var engine = new TriggerEngine();

        var result = engine.Evaluate("anything");

        Assert.Empty(result);
    }

    // ====================================================================
    // Single rule, single-line action
    // ====================================================================

    [Fact]
    public void SingleMatch_SingleLineAction_ReturnsThatCommand()
    {
        var engine = new TriggerEngine();
        engine.Add(new TriggerRule("greet", "^witaj$", "say Hello"));

        var result = engine.Evaluate("witaj");

        var command = Assert.Single(result);
        Assert.Equal("say Hello", command);
    }

    // ====================================================================
    // Single rule, multi-line action
    // ====================================================================

    [Fact]
    public void SingleMatch_MultiLineAction_ReturnsMultipleCommandsInOrder()
    {
        var engine = new TriggerEngine();
        engine.Add(new TriggerRule("look", "widzisz skrzyni", "look chest\nopen chest\nloot chest"));

        var result = engine.Evaluate("widzisz skrzyni");

        Assert.Equal(3, result.Count);
        Assert.Equal("look chest", result[0]);
        Assert.Equal("open chest", result[1]);
        Assert.Equal("loot chest", result[2]);
    }

    // ====================================================================
    // Blank and whitespace-only lines skipped
    // ====================================================================

    [Fact]
    public void BlankLines_AreSkipped()
    {
        var engine = new TriggerEngine();
        engine.Add(new TriggerRule("mult", "trigger", "first\n\nsecond\n \nthird"));

        var result = engine.Evaluate("trigger");

        Assert.Equal(3, result.Count);
        Assert.Equal("first", result[0]);
        Assert.Equal("second", result[1]);
        Assert.Equal("third", result[2]);
    }

    // ====================================================================
    // Capture groups substituted in multi-line actions
    // ====================================================================

    [Fact]
    public void CaptureGroups_SubstitutedInMultiLine()
    {
        var engine = new TriggerEngine();
        engine.Add(new TriggerRule("tell", "(.+) mowi: (.+)", "reply $1\nsay do $1: odebrano"));

        var result = engine.Evaluate("Gandalf mowi: Uwazaj na pierscien!");

        Assert.Equal(2, result.Count);
        Assert.Equal("reply Gandalf", result[0]);
        Assert.Equal("say do Gandalf: odebrano", result[1]);
    }

    // ====================================================================
    // Whitespace and CR trimming per line
    // ====================================================================

    [Fact]
    public void TrailingWhitespace_Trimmed()
    {
        var engine = new TriggerEngine();
        engine.Add(new TriggerRule("spaces", "x", "  cmd1  \n  cmd2  "));

        var result = engine.Evaluate("x");

        Assert.Equal(2, result.Count);
        Assert.Equal("cmd1", result[0]);
        Assert.Equal("cmd2", result[1]);
    }

    [Fact]
    public void CarriageReturn_Trimmed()
    {
        var engine = new TriggerEngine();
        engine.Add(new TriggerRule("crlf", "y", "cmd1\r\ncmd2\r\ncmd3"));

        var result = engine.Evaluate("y");

        Assert.Equal(3, result.Count);
        Assert.Equal("cmd1", result[0]);
        Assert.Equal("cmd2", result[1]);
        Assert.Equal("cmd3", result[2]);
    }

    // ====================================================================
    // Multiple rules – each matched rule contributes commands
    // ====================================================================

    [Fact]
    public void MultipleRules_EachMatchedRuleProducesCommands()
    {
        var engine = new TriggerEngine();
        engine.Add(new TriggerRule("r1", "foo", "cmd1a\ncmd1b"));
        engine.Add(new TriggerRule("r2", "foo", "cmd2"));
        engine.Add(new TriggerRule("r3", "bar", "should-not-appear"));

        var result = engine.Evaluate("foo");

        Assert.Equal(3, result.Count);
        Assert.Equal("cmd1a", result[0]);
        Assert.Equal("cmd1b", result[1]);
        Assert.Equal("cmd2", result[2]);
    }

    // ====================================================================
    // Disabled rules are skipped
    // ====================================================================

    [Fact]
    public void DisabledRule_Skipped()
    {
        var engine = new TriggerEngine();
        engine.Add(new TriggerRule("enabled", "hit", "attack", enabled: true));
        engine.Add(new TriggerRule("disabled", "hit", "should-not-fire", enabled: false));

        var result = engine.Evaluate("hit");

        var command = Assert.Single(result);
        Assert.Equal("attack", command);
    }

    // ====================================================================
    // Match with empty action returns empty list
    // ====================================================================

    [Fact]
    public void MatchWithEmptyAction_ReturnsEmpty()
    {
        var engine = new TriggerEngine();
        engine.Add(new TriggerRule("empty", ".*", ""));

        var result = engine.Evaluate("anything");

        Assert.Empty(result);
    }

    // ====================================================================
    // Match with whitespace-only action returns empty list
    // ====================================================================

    [Fact]
    public void MatchWithWhitespaceOnlyAction_ReturnsEmpty()
    {
        var engine = new TriggerEngine();
        engine.Add(new TriggerRule("ws", ".*", "  \n  \n  "));

        var result = engine.Evaluate("anything");

        Assert.Empty(result);
    }
}
