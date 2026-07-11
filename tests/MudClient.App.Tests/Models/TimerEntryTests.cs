using MudClient.App.Models;

namespace MudClient.App.Tests.Models;

public sealed class TimerEntryTests
{
    // ====================================================================
    // GetCommands() no-arg — newline-only split (backward compat)
    // ====================================================================

    [Fact]
    public void GetCommands_NoArg_SplitsOnNewlines()
    {
        var timer = new TimerEntry { CommandsText = "look\nnorth\nsay hi" };

        var result = timer.GetCommands();

        Assert.Equal(3, result.Count);
        Assert.Equal("look", result[0]);
        Assert.Equal("north", result[1]);
        Assert.Equal("say hi", result[2]);
    }

    [Fact]
    public void GetCommands_NoArg_DoesNotSplitOnSemicolons()
    {
        var timer = new TimerEntry { CommandsText = "look;north" };

        var result = timer.GetCommands();

        var command = Assert.Single(result);
        Assert.Equal("look;north", command);
    }

    // ====================================================================
    // GetCommands(null) — should behave like no-arg
    // ====================================================================

    [Fact]
    public void GetCommands_NullSeparator_NewlinesOnly()
    {
        var timer = new TimerEntry { CommandsText = "look;north\nsay hi" };

        var result = timer.GetCommands(separator: null);

        Assert.Equal(2, result.Count);
        Assert.Equal("look;north", result[0]);
        Assert.Equal("say hi", result[1]);
    }

    // ====================================================================
    // GetCommands("") — empty separator, newlines only
    // ====================================================================

    [Fact]
    public void GetCommands_EmptySeparator_NewlinesOnly()
    {
        var timer = new TimerEntry { CommandsText = "look;north\nsay hi" };

        var result = timer.GetCommands(separator: "");

        Assert.Equal(2, result.Count);
        Assert.Equal("look;north", result[0]);
        Assert.Equal("say hi", result[1]);
    }

    // ====================================================================
    // GetCommands(";") — splits on both newlines and semicolons
    // ====================================================================

    [Fact]
    public void GetCommands_WithSeparator_SplitsOnSemicolons()
    {
        var timer = new TimerEntry { CommandsText = "north;east;south" };

        var result = timer.GetCommands(separator: ";");

        Assert.Equal(3, result.Count);
        Assert.Equal("north", result[0]);
        Assert.Equal("east", result[1]);
        Assert.Equal("south", result[2]);
    }

    [Fact]
    public void GetCommands_WithSeparator_SplitsOnNewlinesAndSemicolons()
    {
        var timer = new TimerEntry { CommandsText = "north;east\nsouth;west" };

        var result = timer.GetCommands(separator: ";");

        Assert.Equal(4, result.Count);
        Assert.Equal("north", result[0]);
        Assert.Equal("east", result[1]);
        Assert.Equal("south", result[2]);
        Assert.Equal("west", result[3]);
    }

    // ====================================================================
    // Empty / whitespace CommandsText
    // ====================================================================

    [Fact]
    public void GetCommands_EmptyCommandsText_ReturnsEmpty()
    {
        var timer = new TimerEntry { CommandsText = string.Empty };

        var result = timer.GetCommands(separator: ";");

        Assert.Empty(result);
    }

    [Fact]
    public void GetCommands_NullCommandsText_DoesNotThrow()
    {
        var timer = new TimerEntry { CommandsText = null! }; // explicit null via property

        var result = timer.GetCommands(separator: ";");

        Assert.Empty(result);
    }

    // ====================================================================
    // Whitespace trimming and CR
    // ====================================================================

    [Fact]
    public void GetCommands_TrimsWhitespace()
    {
        var timer = new TimerEntry { CommandsText = "  look  \n  north  " };

        var result = timer.GetCommands(separator: ";");

        Assert.Equal(2, result.Count);
        Assert.Equal("look", result[0]);
        Assert.Equal("north", result[1]);
    }

    [Fact]
    public void GetCommands_TrimsCarriageReturn()
    {
        var timer = new TimerEntry { CommandsText = "look\r\nnorth\r\n" };

        var result = timer.GetCommands(separator: ";");

        Assert.Equal(2, result.Count);
        Assert.Equal("look", result[0]);
        Assert.Equal("north", result[1]);
    }

    // ====================================================================
    // Empty segments skipped
    // ====================================================================

    [Fact]
    public void GetCommands_SkipsEmptySegments()
    {
        var timer = new TimerEntry { CommandsText = "north;;east\n;south" };

        var result = timer.GetCommands(separator: ";");

        Assert.Equal(3, result.Count);
        Assert.Equal("north", result[0]);
        Assert.Equal("east", result[1]);
        Assert.Equal("south", result[2]);
    }

    // ====================================================================
    // Whitespace separator — newline-only like null/empty
    // ====================================================================

    [Fact]
    public void GetCommands_WhitespaceSeparator_NewlinesOnly()
    {
        var timer = new TimerEntry { CommandsText = "look;north\nsay hi" };

        var result = timer.GetCommands(separator: "  ");

        Assert.Equal(2, result.Count);
        Assert.Equal("look;north", result[0]);
        Assert.Equal("say hi", result[1]);
    }
}
