using MudClient.Core.Text;

namespace MudClient.Core.Tests;

public sealed class CombatDamageRangeTextTransformerTests
{
    [Theory]
    [InlineData("Siniaczysz orka.", "Siniaczysz (2) orka.")]
    [InlineData("Muskasz orka.", "Muskasz (6) orka.")]
    [InlineData("Ledwie ranisz orka.", "Ledwie ranisz (10) orka.")]
    [InlineData("Lekko ranisz orka.", "Lekko ranisz (14) orka.")]
    [InlineData("Ranisz orka.", "Ranisz (18) orka.")]
    [InlineData("Mocno ranisz orka.", "Mocno ranisz (22) orka.")]
    [InlineData("Dotkliwie ranisz orka.", "Dotkliwie ranisz (26) orka.")]
    [InlineData("Poważnie ranisz orka.", "Poważnie ranisz (30) orka.")]
    [InlineData("Masakrujesz orka.", "Masakrujesz (34) orka.")]
    [InlineData("Rozpruwasz orka.", "Rozpruwasz (38) orka.")]
    [InlineData("Dewastujesz orka.", "Dewastujesz (44) orka.")]
    [InlineData("Grzmocisz orka.", "Grzmocisz (50) orka.")]
    [InlineData("Niszczysz orka.", "Niszczysz (55) orka.")]
    [InlineData("NISZCZYSZ orka.", "NISZCZYSZ (60) orka.")]
    [InlineData("DRUZGOCZESZ orka.", "DRUZGOCZESZ (67) orka.")]
    [InlineData("ROZPRUWASZ orka.", "ROZPRUWASZ (75) orka.")]
    [InlineData("ROZRYWASZ orka.", "ROZRYWASZ (84) orka.")]
    [InlineData("ROZBEBESZASZ orka.", "ROZBEBESZASZ (100) orka.")]
    [InlineData("DEKAPITUJESZ orka.", "DEKAPITUJESZ (115) orka.")]
    [InlineData("EKSTYRPUJESZ orka.", "EKSTYRPUJESZ (130) orka.")]
    [InlineData("ANIHILUJESZ orka.", "ANIHILUJESZ (145) orka.")]
    [InlineData("UŚMIERCASZ orka.", "UŚMIERCASZ (200) orka.")]
    [InlineData("UNICESTWIASZ orka.", "UNICESTWIASZ (200++) orka.")]
    public void Transform_AnnotatesEveryPlayerDamageTier(string input, string expected)
    {
        var transformer = new CombatDamageRangeTextTransformer();

        Assert.Equal(expected, transformer.Transform(input, enabled: true));
    }

    [Theory]
    [InlineData("Chybiasz orka.")]
    [InlineData("chybiając paskudnie.")]
    [InlineData("Ork chybia ciebie.")]
    public void Transform_DoesNotAnnotateMisses(string input)
    {
        var transformer = new CombatDamageRangeTextTransformer();

        Assert.Equal(input, transformer.Transform(input, enabled: true));
    }

    [Fact]
    public void Transform_AnnotatesThirdPersonPhraseInsideCombatLine()
    {
        var transformer = new CombatDamageRangeTextTransformer();

        var output = transformer.Transform("Czarny ork mocno rani bohatera.\n", enabled: true);

        Assert.Equal("Czarny ork mocno rani (22) bohatera.\n", output);
    }

    [Fact]
    public void Transform_AcceptsPolishCharactersAndAscii()
    {
        var transformer = new CombatDamageRangeTextTransformer();

        var output = transformer.Transform(
            "Powaznie ranisz orka.\nUŚMIERCA wojownika.\n",
            enabled: true);

        Assert.Equal(
            "Powaznie ranisz (30) orka.\nUŚMIERCA (200) wojownika.\n",
            output);
    }

    [Fact]
    public void Transform_PreservesCaseSensitiveDamageTiers()
    {
        var transformer = new CombatDamageRangeTextTransformer();

        var output = transformer.Transform(
            "Niszczysz orka.\nNISZCZYSZ orka.\nRozpruwasz orka.\nROZPRUWASZ orka.\n",
            enabled: true);

        Assert.Equal(
            "Niszczysz (55) orka.\nNISZCZYSZ (60) orka.\n"
            + "Rozpruwasz (38) orka.\nROZPRUWASZ (75) orka.\n",
            output);
    }

    [Fact]
    public void Transform_HandlesPhraseSplitAcrossChunksAndAnsiReset()
    {
        var transformer = new CombatDamageRangeTextTransformer();

        var first = transformer.Transform("\u001b[31mDotkliwie ra", enabled: true);
        var second = transformer.Transform("nisz\u001b[0m trolla.\n", enabled: true);

        Assert.Equal("\u001b[31m", first);
        Assert.Equal("Dotkliwie ranisz (26)\u001b[0m trolla.\n", second);
    }

    [Fact]
    public void Transform_DoesNotMatchPhraseInsideLongerWord()
    {
        var transformer = new CombatDamageRangeTextTransformer();

        var output = transformer.Transform("To jest rozpruwaszek z legendy.\n", enabled: true);

        Assert.Equal("To jest rozpruwaszek z legendy.\n", output);
    }

    [Fact]
    public void Transform_DisabledReturnsPendingTextUnmodified()
    {
        var transformer = new CombatDamageRangeTextTransformer();

        Assert.Empty(transformer.Transform("Mocno ra", enabled: true));

        Assert.Equal(
            "Mocno ranisz orka.",
            transformer.Transform("nisz orka.", enabled: false));
    }
}
