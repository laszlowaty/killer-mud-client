using MudClient.Core.Automation;

namespace MudClient.Core.Tests;

public sealed class CommandStackerTests
{
    // ====================================================================
    // Null / empty / whitespace-only text
    // ====================================================================

    [Fact]
    public void Split_NullText_ReturnsEmpty()
    {
        var result = CommandStacker.Split(null);

        Assert.Empty(result);
    }

    [Fact]
    public void Split_EmptyText_ReturnsEmpty()
    {
        var result = CommandStacker.Split(string.Empty);

        Assert.Empty(result);
    }

    [Fact]
    public void Split_WhitespaceOnlyText_ReturnsEmpty()
    {
        var result = CommandStacker.Split("   \n  \n  ", ";");

        Assert.Empty(result);
    }

    [Fact]
    public void Split_WhitespaceOnlyText_NoSeparator_ReturnsEmpty()
    {
        var result = CommandStacker.Split("   \n  \n  ");

        Assert.Empty(result);
    }

    // ====================================================================
    // Newline-only behavior (default separator = null)
    // ====================================================================

    [Fact]
    public void Split_NullSeparator_SplitsOnNewlines()
    {
        var result = CommandStacker.Split("look\nnorth\nsay hi", separator: null);

        Assert.Equal(3, result.Count);
        Assert.Equal("look", result[0]);
        Assert.Equal("north", result[1]);
        Assert.Equal("say hi", result[2]);
    }

    [Fact]
    public void Split_EmptySeparator_SplitsOnNewlinesOnly()
    {
        var result = CommandStacker.Split("look\nnorth", separator: "");

        Assert.Equal(2, result.Count);
        Assert.Equal("look", result[0]);
        Assert.Equal("north", result[1]);
    }

    [Fact]
    public void Split_EmptySeparator_DoesNotSplitOnSemicolons()
    {
        var result = CommandStacker.Split("look;north", separator: "");

        var command = Assert.Single(result);
        Assert.Equal("look;north", command);
    }

    [Fact]
    public void Split_NullSeparator_DoesNotSplitOnSemicolons()
    {
        var result = CommandStacker.Split("look;north", separator: null);

        var command = Assert.Single(result);
        Assert.Equal("look;north", command);
    }

    // ====================================================================
    // Explicit ";" separator — splits on both newlines and semicolons
    // ====================================================================

    [Fact]
    public void Split_WithExplicitSemicolonSeparator_SplitsOnSemicolons()
    {
        var result = CommandStacker.Split("look;north;say hi", ";");

        Assert.Equal(3, result.Count);
        Assert.Equal("look", result[0]);
        Assert.Equal("north", result[1]);
        Assert.Equal("say hi", result[2]);
    }

    [Fact]
    public void Split_WithExplicitSemicolonSeparator_SplitsOnNewlinesAndSemicolons()
    {
        var result = CommandStacker.Split("look;north\nsay hi;emote test", ";");

        Assert.Equal(4, result.Count);
        Assert.Equal("look", result[0]);
        Assert.Equal("north", result[1]);
        Assert.Equal("say hi", result[2]);
        Assert.Equal("emote test", result[3]);
    }

    // ====================================================================
    // Whitespace trimming
    // ====================================================================

    [Fact]
    public void Split_TrimsWhitespace_WithSeparator()
    {
        var result = CommandStacker.Split("  look  ;  north  \n  say hi  ", ";");

        Assert.Equal(3, result.Count);
        Assert.Equal("look", result[0]);
        Assert.Equal("north", result[1]);
        Assert.Equal("say hi", result[2]);
    }

    [Fact]
    public void Split_TrimsCarriageReturn()
    {
        var result = CommandStacker.Split("look\r\nnorth\r\nsay hi");

        Assert.Equal(3, result.Count);
        Assert.Equal("look", result[0]);
        Assert.Equal("north", result[1]);
        Assert.Equal("say hi", result[2]);
    }

    [Fact]
    public void Split_WithSeparator_TrimsCarriageReturn()
    {
        var result = CommandStacker.Split("look\r\nnorth;say hi\r\n", ";");

        Assert.Equal(3, result.Count);
        Assert.Equal("look", result[0]);
        Assert.Equal("north", result[1]);
        Assert.Equal("say hi", result[2]);
    }

    // ====================================================================
    // Empty segments skipped
    // ====================================================================

    [Fact]
    public void Split_SkipsEmptySegments_Newlines()
    {
        var result = CommandStacker.Split("look\n\nnorth\n\n\nsay hi");

        Assert.Equal(3, result.Count);
        Assert.Equal("look", result[0]);
        Assert.Equal("north", result[1]);
        Assert.Equal("say hi", result[2]);
    }

    [Fact]
    public void Split_SkipsEmptySegments_Semicolons()
    {
        var result = CommandStacker.Split("look;;north;;;say hi", ";");

        Assert.Equal(3, result.Count);
        Assert.Equal("look", result[0]);
        Assert.Equal("north", result[1]);
        Assert.Equal("say hi", result[2]);
    }

    [Fact]
    public void Split_SkipsWhitespaceOnlySegments()
    {
        var result = CommandStacker.Split("look; \n  ;\nnorth", ";");

        Assert.Equal(2, result.Count);
        Assert.Equal("look", result[0]);
        Assert.Equal("north", result[1]);
    }

    [Fact]
    public void Split_OnlySeparators_ReturnsEmpty()
    {
        var result = CommandStacker.Split(";;;\n;;", ";");

        Assert.Empty(result);
    }

    // ====================================================================
    // Custom separator character
    // ====================================================================

    [Fact]
    public void Split_CustomSeparator_UsesIt()
    {
        var result = CommandStacker.Split("look|north|say hi", separator: "|");

        Assert.Equal(3, result.Count);
        Assert.Equal("look", result[0]);
        Assert.Equal("north", result[1]);
        Assert.Equal("say hi", result[2]);
    }

    [Fact]
    public void Split_CustomSeparator_StillSplitsOnNewlines()
    {
        var result = CommandStacker.Split("look|north\nsay hi", separator: "|");

        Assert.Equal(3, result.Count);
        Assert.Equal("look", result[0]);
        Assert.Equal("north", result[1]);
        Assert.Equal("say hi", result[2]);
    }

    // ====================================================================
    // Whitespace separator disables stacking (like null/empty)
    // ====================================================================

    [Fact]
    public void Split_WhitespaceSeparator_OnlySplitsOnNewlines()
    {
        var result = CommandStacker.Split("look;north\nsay hi", separator: "  ");

        Assert.Equal(2, result.Count);
        Assert.Equal("look;north", result[0]);
        Assert.Equal("say hi", result[1]);
    }

    // ====================================================================
    // Single line, no separator
    // ====================================================================

    [Fact]
    public void Split_SingleLine_NoSeparator_ReturnsSingleCommand()
    {
        var result = CommandStacker.Split("look");

        var command = Assert.Single(result);
        Assert.Equal("look", command);
    }

    // ====================================================================
    // Preservation of internal whitespace
    // ====================================================================

    [Fact]
    public void Split_PreservesInternalWhitespace()
    {
        var result = CommandStacker.Split("say hello world;emote  test", ";");

        Assert.Equal(2, result.Count);
        Assert.Equal("say hello world", result[0]);
        Assert.Equal("emote  test", result[1]);
    }
}
