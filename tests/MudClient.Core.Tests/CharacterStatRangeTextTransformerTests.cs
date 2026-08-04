using MudClient.Core.Text;

namespace MudClient.Core.Tests;

public sealed class CharacterStatRangeTextTransformerTests
{
    [Theory]
    [InlineData("półboska.", "214+")]
    [InlineData("legendarna.", "200-213")]
    [InlineData("niespotykana.", "186-199")]
    [InlineData("niezmiernie wysoka.", "172-185")]
    [InlineData("wysoka.", "158-171")]
    [InlineData("niezła.", "144-157")]
    [InlineData("nieprzeciętna.", "130-143")]
    [InlineData("średnia.", "116-129")]
    [InlineData("poniżej przeciętnej.", "102-115")]
    [InlineData("bardzo niska.", "88-101")]
    [InlineData("godna pożałowania.", "74-87")]
    [InlineData("katastrofalna.", "<73")]
    public void AnnotateLine_MapsDescriptionsToNumericRanges(string description, string range)
    {
        var result = CharacterStatRangeTextTransformer.AnnotateLine(
            $"Twoja siła jest {description}");

        Assert.Equal($"Twoja siła jest {description} ({range})", result);
    }

    [Fact]
    public void Transform_HandlesAnsiAndStatLineSplitAcrossChunks()
    {
        var transformer = new CharacterStatRangeTextTransformer();

        var first = transformer.Transform("\u001b[32mTwoja zręcz", enabled: true);
        var second = transformer.Transform("ność jest niezmiernie wysoka.\u001b[0m\r\n", enabled: true);

        Assert.Empty(first);
        Assert.Equal(
            "\u001b[32mTwoja zręczność jest niezmiernie wysoka.\u001b[0m (172-185)\r\n",
            second);
    }

    [Theory]
    [InlineData("Twoja sila jest polboska.", "214+")]
    [InlineData("Twoja zrecznosc jest nieprzecietna.", "130-143")]
    [InlineData("Twoja kondycja jest ponizej przecietnej.", "102-115")]
    [InlineData("Twoja inteligencja jest srednia.", "116-129")]
    [InlineData("Twoja wiedza jest godna pozalowania.", "74-87")]
    [InlineData("Twoja charyzma jest niezla.", "144-157")]
    public void AnnotateLine_AcceptsTextWithoutPolishCharacters(string line, string range)
    {
        Assert.Equal($"{line} ({range})", CharacterStatRangeTextTransformer.AnnotateLine(line));
    }

    [Fact]
    public void Transform_AcceptsAsciiStatNameSplitAcrossChunks()
    {
        var transformer = new CharacterStatRangeTextTransformer();

        Assert.Empty(transformer.Transform("Twoja zrecz", enabled: true));

        Assert.Equal(
            "Twoja zrecznosc jest niezmiernie wysoka. (172-185)\n",
            transformer.Transform("nosc jest niezmiernie wysoka.\n", enabled: true));
    }

    [Fact]
    public void Transform_DoesNotBufferOrdinaryOutputOrPrompts()
    {
        var transformer = new CharacterStatRangeTextTransformer();

        var output = transformer.Transform("Witaj w KillerMUDzie.\r\n> ", enabled: true);

        Assert.Equal("Witaj w KillerMUDzie.\r\n> ", output);
    }

    [Fact]
    public void Transform_DisabledReturnsPendingCandidateUnmodified()
    {
        var transformer = new CharacterStatRangeTextTransformer();

        Assert.Empty(transformer.Transform("Twoja wiedza jest ", enabled: true));

        var output = transformer.Transform("legendarna.\n", enabled: false);

        Assert.Equal("Twoja wiedza jest legendarna.\n", output);
    }

    [Fact]
    public void AnnotateLine_DoesNotDuplicateExistingRange()
    {
        const string line = "Twoja charyzma jest średnia. (116-129)";

        Assert.Equal(line, CharacterStatRangeTextTransformer.AnnotateLine(line));
    }
}
