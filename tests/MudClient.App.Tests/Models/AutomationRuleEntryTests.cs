using MudClient.App.Models;

namespace MudClient.App.Tests.Models;

public sealed class AutomationRuleEntryTests
{
    // ====================================================================
    // Multi-line action text is stored unchanged
    // ====================================================================

    [Fact]
    public void Constructor_StoresMultiLineActionUnchanged()
    {
        var multiLineAction = "look\nnorth\nsouth";
        var entry = new AutomationRuleEntry("test", "alias", "^x$", multiLineAction, true);

        Assert.Equal(multiLineAction, entry.Action);
    }

    [Fact]
    public void ActionProperty_RoundTripsMultiLineText()
    {
        var multiLineAction = "kill orc\nkill goblin\nloot corpse";
        var entry = new AutomationRuleEntry("combat", "alias", "^k$", multiLineAction, true);

        Assert.Equal(multiLineAction, entry.Action);

        // Modify action and verify
        var updatedAction = "cast shield\ncast armor\ncast bless";
        entry.Action = updatedAction;
        Assert.Equal(updatedAction, entry.Action);
    }

    [Fact]
    public void Constructor_StoresSingleLineActionUnchanged()
    {
        var entry = new AutomationRuleEntry("look", "alias", "^l$", "look", true);

        Assert.Equal("look", entry.Action);
    }

    [Fact]
    public void ActionProperty_WithEmptyString_StoredCorrectly()
    {
        var entry = new AutomationRuleEntry("empty", "trigger", ".*", string.Empty, true);

        Assert.Equal(string.Empty, entry.Action);

        entry.Action = string.Empty;
        Assert.Equal(string.Empty, entry.Action);
    }

    [Fact]
    public void ActionProperty_WithNewlinesAndCarriageReturns_Preserved()
    {
        var actionWithCrLf = "cmd1\r\ncmd2\r\ncmd3";
        var entry = new AutomationRuleEntry("crlf", "alias", "^t$", actionWithCrLf, true);

        Assert.Equal(actionWithCrLf, entry.Action);
    }

    [Fact]
    public void ActionProperty_WithTrailingWhitespaceOnLines_StoredExactly()
    {
        var action = "  cmd1  \n  cmd2  \ncmd3  ";
        var entry = new AutomationRuleEntry("spaces", "alias", "^x$", action, true);

        // Action must be stored verbatim; trimming is the engine's responsibility
        Assert.Equal(action, entry.Action);
    }
}
