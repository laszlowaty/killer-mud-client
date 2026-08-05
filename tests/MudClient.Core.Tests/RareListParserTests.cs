using MudClient.Core.Killeropedia;

namespace MudClient.Core.Tests;

public sealed class RareListParserTests
{
    [Fact]
    public void ParseList_ExtractsItemsAndTracksCategoryAcrossPagerNoise()
    {
        string[] lines =
        [
            "<<============= lista przedmiotow unikalnych - artefact =============>>",
            "",
            "+[-1 d] [N] ( kilof             - one hand      ) [29099] krasnoludzki kilof 'Potega Ziemi'",
            "+[-1 d] [R] ( wlocznia          - two hand      ) [  215] trojzab Turlitha",
            "[Nacisnij Enter aby kontynuowac]",
            ">",
            "+[-1 d] [N] ( miecz             - one hand      ) [29052] miecz Ares Dragon",
            "",
            "<<============= lista przedmiotow wyjatkowych - rare =============>>",
            "+[-1 d] [N] ( maczuga           - one hand      ) [  874] szkarlatny mlot bojowy z glowa smoka",
        ];

        var entries = RareListParser.ParseList(lines);

        Assert.Equal(4, entries.Count);

        var kilof = Assert.Single(entries, entry => entry.Vnum == 29099);
        Assert.Equal("krasnoludzki kilof 'Potega Ziemi'", kilof.Name);
        Assert.Equal("kilof", kilof.ItemType);
        Assert.Equal("one hand", kilof.Slot);
        Assert.Equal("N", kilof.Flag);
        Assert.Equal("artefakt", kilof.Category);

        var trojzab = Assert.Single(entries, entry => entry.Vnum == 215);
        Assert.Equal("trojzab Turlitha", trojzab.Name);
        Assert.Equal("R", trojzab.Flag);
        Assert.Equal("artefakt", trojzab.Category);

        var miecz = Assert.Single(entries, entry => entry.Vnum == 29052);
        Assert.Equal("artefakt", miecz.Category);

        var mlot = Assert.Single(entries, entry => entry.Vnum == 874);
        Assert.Equal("rzadki", mlot.Category);
    }

    [Fact]
    public void ParseList_InstanceOnlyHeader_TaggedAsInstancyjny()
    {
        string[] lines =
        [
            "<<============= lista przedmiotow dostepnych tylko w instancji - artefact, rare =============>>",
            "+[-1 d] [R] ( maczuga           - two hand      ) [29060] mityczny mlot 'Niszczyciel Magii'",
        ];

        var entries = RareListParser.ParseList(lines);

        var entry = Assert.Single(entries);
        Assert.Equal("instancyjny", entry.Category);
        Assert.Equal(29060, entry.Vnum);
    }

    [Fact]
    public void ParseList_IgnoresNonItemLines()
    {
        string[] lines = ["", ">", "Cos innego.", "<412/488hp 60/100mv>"];

        Assert.Empty(RareListParser.ParseList(lines));
    }

    [Fact]
    public void ExtractDetailText_StripsPromptAndPagerNoiseButKeepsContent()
    {
        string[] lines =
        [
            "",
            "Jakis opis przedmiotu.",
            "Kolejna linia opisu.",
            "[Nacisnij Enter aby kontynuowac]",
            ">",
            "<412/488hp 60/100mv>",
            "",
        ];

        var text = RareListParser.ExtractDetailText(lines);

        Assert.Equal("Jakis opis przedmiotu.\nKolejna linia opisu.", text);
    }

    [Fact]
    public void ExtractDetailText_EmptyResponse_ReturnsEmptyString()
    {
        string[] lines = ["<412/488hp 60/100mv>"];

        Assert.Equal(string.Empty, RareListParser.ExtractDetailText(lines));
    }

    [Fact]
    public void ContainsPagerPrompt_DetectsMarkerRegardlessOfCase()
    {
        Assert.True(RareListParser.ContainsPagerPrompt(["[nacisnij enter aby kontynuowac]"]));
        Assert.False(RareListParser.ContainsPagerPrompt(["zwykla linia"]));
    }
}
