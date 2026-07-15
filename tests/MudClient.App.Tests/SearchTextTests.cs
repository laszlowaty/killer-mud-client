using MudClient.App.Services;

namespace MudClient.App.Tests;

public sealed class SearchTextTests
{
    [Fact]
    public void FindMatchRanges_ReturnsEmpty_ForEmptySearch()
    {
        Assert.Empty(SearchText.FindMatchRanges("Miecze dwuręczne", ""));
        Assert.Empty(SearchText.FindMatchRanges("Miecze dwuręczne", "   "));
        Assert.Empty(SearchText.FindMatchRanges("", "miecz"));
        Assert.Empty(SearchText.FindMatchRanges(null, "miecz"));
    }

    [Fact]
    public void FindMatchRanges_MatchesCaseInsensitive()
    {
        var ranges = SearchText.FindMatchRanges("Miecze dwuręczne", "MIECZ");

        Assert.Equal([(0, 5)], ranges);
    }

    [Fact]
    public void FindMatchRanges_IgnoresDiacritics_InBothDirections()
    {
        Assert.Equal([(7, 9)], SearchText.FindMatchRanges("Miecze dwuręczne", "dwureczne"));
        Assert.Equal([(7, 9)], SearchText.FindMatchRanges("Miecze dwureczne", "dwuręczne"));
        Assert.Equal([(0, 3)], SearchText.FindMatchRanges("Łuk długi", "luk"));
    }

    [Fact]
    public void FindMatchRanges_FindsAllOccurrencesOfAllTokens()
    {
        var ranges = SearchText.FindMatchRanges("magia ognia i magia wody", "magia wody");

        Assert.Equal([(0, 5), (14, 5), (20, 4)], ranges);
    }

    [Fact]
    public void FindMatchRanges_MergesOverlappingRanges()
    {
        var ranges = SearchText.FindMatchRanges("kowal", "kowa owal");

        Assert.Equal([(0, 5)], ranges);
    }

    [Fact]
    public void FindMatchRanges_ReturnsEmpty_WhenNoTokenMatches()
    {
        Assert.Empty(SearchText.FindMatchRanges("Miecze dwuręczne", "topór"));
    }
}
