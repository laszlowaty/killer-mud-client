using MudClient.Core.Text;

namespace MudClient.Core.Tests;

public sealed class MudCommandTextTests
{
    [Theory]
    [InlineData("Wyjście", "wyjscie")]
    [InlineData("Żółta BRAMA", "zolta brama")]
    [InlineData("ĄĆĘŁŃÓŚŹŻ", "acelnoszz")]
    public void ToAsciiLowerInvariant_RemovesPolishDiacriticsAndLowercases(
        string input,
        string expected)
    {
        Assert.Equal(expected, MudCommandText.ToAsciiLowerInvariant(input));
    }
}
