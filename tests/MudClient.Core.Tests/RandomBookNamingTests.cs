using MudClient.Core.Killeropedia;

namespace MudClient.Core.Tests;

public sealed class RandomBookNamingTests
{
    [Theory]
    [InlineData("duża księga triumfu", "duża księga triumfu (Paladyn)")]
    [InlineData("Widzisz tutaj: duża księga triumfu.", "Widzisz tutaj: duża księga triumfu (Paladyn).")]
    [InlineData("księga triumfu", "księga triumfu (Paladyn)")]
    [InlineData("kolosalny tom magii", "kolosalny tom magii (Mag)")]
    [InlineData("filigranowy wolumen Zapomnianego Boga", "filigranowy wolumen Zapomnianego Boga (Kleryk)")]
    [InlineData("masywne cymelium wiedzy", "masywne cymelium wiedzy (Mag)")]
    [InlineData("stary foliał walki", "stary foliał walki (Paladyn)")]
    [InlineData("mała książka lasu", "mała książka lasu (Druid)")]
    [InlineData("wolumin piasku", "wolumin piasku (Nomad)")]
    public void AnnotateClasses_MatchesKnownBookNames(string input, string expected)
    {
        Assert.Equal(expected, RandomBookNaming.AnnotateClasses(input));
    }

    [Theory]
    [InlineData("Brakuje ci sily do dalszej walki.")]
    [InlineData("Rzucasz zaklecie mocy.")]
    [InlineData("Twoja wiara w zwyciestwo rosnie.")]
    [InlineData("")]
    [InlineData("pojedyncze")]
    public void AnnotateClasses_LeavesUnrelatedTextUnchanged(string input)
    {
        Assert.Equal(input, RandomBookNaming.AnnotateClasses(input));
    }

    [Fact]
    public void AnnotateClasses_MultiWordClassPhrase_MatchesAsSingleUnit()
    {
        var result = RandomBookNaming.AnnotateClasses("nowa księga z rubinami");

        Assert.Equal("nowa księga z rubinami (Mag)", result);
    }

    [Fact]
    public void AnnotateClasses_AnnotatesEachMatchIndependently()
    {
        var result = RandomBookNaming.AnnotateClasses("duża księga triumfu obok kolosalny tom magii");

        Assert.Equal("duża księga triumfu (Paladyn) obok kolosalny tom magii (Mag)", result);
    }
}
