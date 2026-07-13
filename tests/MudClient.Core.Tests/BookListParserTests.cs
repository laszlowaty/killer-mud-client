using MudClient.Core.Killeropedia;

namespace MudClient.Core.Tests;

public sealed class BookListParserTests
{
    [Fact]
    public void ParseClassList_ExtractsBooksAcrossPagerNoise()
    {
        string[] lines =
        [
            "<<============= lista ksiag dla klasy: Czarodziej =============>>",
            "[28063] starozytna ksiega wywolywan: 'fireball' 'cone of cold' 'iceshield'",
            "[1210] kaplanska ksiega:",
            "[Nacisnij Enter aby kontynuowac]",
            ">",
            "[16818] ksiega zaklec: 'force bolt' 'decay' 'vampiric touch' 'fly' 'resist elements'",
        ];

        var books = BookListParser.ParseClassList(lines);

        Assert.Equal(3, books.Count);
        Assert.Equal(28063, books[0].Vnum);
        Assert.Equal("starozytna ksiega wywolywan", books[0].Name);
        Assert.Equal(["fireball", "cone of cold", "iceshield"], books[0].Spells);
        Assert.Empty(books[1].Spells);
        Assert.Equal(16818, books[2].Vnum);
    }

    [Fact]
    public void ParseDetails_ExtractsNameSpellsAndLoadLocations()
    {
        string[] lines =
        [
            "<<============= Informacje na temat ksiegi =============>>",
            "",
            "ksiega zaklec",
            "Zaklecia: 'force bolt' 'decay' 'vampiric touch' 'fly' 'resist elements'",
            "",
            "Laduje sie w(na):",
            " na mobie: czarnoksieznik Zeerith'din (Podmrok gobliny)",
            "",
            "Wszystkie twoje lustrzane odbicia zniknely.",
            "<412/488hp 60/100mv>",
        ];

        var details = BookListParser.ParseDetails(lines);

        Assert.Equal("ksiega zaklec", details.Name);
        Assert.Equal(5, details.Spells.Count);
        Assert.Equal(
            ["na mobie: czarnoksieznik Zeerith'din (Podmrok gobliny)"],
            details.LoadLocations);
    }

    [Fact]
    public void ParseDetails_WithoutHeader_ThrowsUsefulError()
    {
        var exception = Assert.Throws<FormatException>(() => BookListParser.ParseDetails(["brak ksiegi"]));

        Assert.Contains("nagłówka", exception.Message);
    }
}
