using MudClient.Core.Automation;

namespace MudClient.Core.Tests;

public sealed class EchoCommandParserTests
{
    [Fact]
    public void TryCreate_KnownColor_CreatesEchoWithoutParsingText()
    {
        var created = EchoCommandParser.TryCreate(
            "red",
            "Utracono efekt: Ochrona \"smoka\".",
            out var command);

        Assert.True(created);
        Assert.NotNull(command);
        Assert.Equal(31, command.AnsiColorCode);
        Assert.Equal("Utracono efekt: Ochrona \"smoka\".", command.Text);
    }

    [Theory]
    [InlineData("echo(\"red\", \"Straciłeś ochronę!\")", 31, "Straciłeś ochronę!")]
    [InlineData(" ECHO ( 'gray' , 'Cel: \\'ork\\'' ) ", 90, "Cel: 'ork'")]
    [InlineData("echo(\"green\", \"linia 1\\nlinia 2\")", 32, "linia 1\nlinia 2")]
    public void Parse_ValidInvocation_ReturnsColorAndText(
        string input,
        int expectedColor,
        string expectedText)
    {
        var status = EchoCommandParser.Parse(input, out var command);

        Assert.Equal(EchoCommandParseStatus.Success, status);
        Assert.NotNull(command);
        Assert.Equal(expectedColor, command.AnsiColorCode);
        Assert.Equal(expectedText, command.Text);
    }

    [Theory]
    [InlineData("echo(\"orange\", \"tekst\")")]
    [InlineData("echo(red, \"tekst\")")]
    [InlineData("echo(\"red\")")]
    [InlineData("echo(\"red\", \"tekst\"")]
    public void Parse_InvalidInvocation_IsRecognizedAndRejected(string input)
    {
        var status = EchoCommandParser.Parse(input, out var command);

        Assert.Equal(EchoCommandParseStatus.Invalid, status);
        Assert.Null(command);
    }

    [Theory]
    [InlineData("echo test")]
    [InlineData("echoing(\"red\", \"tekst\")")]
    [InlineData("look")]
    public void Parse_RegularMudCommand_IsNotConsumed(string input)
    {
        var status = EchoCommandParser.Parse(input, out var command);

        Assert.Equal(EchoCommandParseStatus.NotEcho, status);
        Assert.Null(command);
    }
}
