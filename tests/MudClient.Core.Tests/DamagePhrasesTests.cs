using MudClient.Core.Combat;

namespace MudClient.Core.Tests;

public sealed class DamagePhrasesTests
{
    [Theory]
    [InlineData("Chybiasz golema swoim mieczem.", 0)]
    [InlineData("Siniaczysz golema swoim mieczem.", 2)]
    [InlineData("Muskasz golema swoim mieczem.", 6)]
    [InlineData("Ledwie ranisz golema swoim mieczem.", 10)]
    [InlineData("Lekko ranisz golema swoim mieczem.", 14)]
    [InlineData("Ranisz golema swoim mieczem.", 18)]
    [InlineData("Mocno ranisz golema swoim mieczem.", 22)]
    [InlineData("Dotkliwie ranisz golema swoim mieczem.", 26)]
    [InlineData("Powaznie ranisz golema swoim mieczem.", 30)]
    [InlineData("Masakrujesz golema swoim mieczem.", 34)]
    [InlineData("Rozpruwasz golema swoim mieczem.", 38)]
    [InlineData("Dewastujesz golema swoim mieczem.", 44)]
    [InlineData("Grzmocisz golema swoim mieczem.", 50)]
    [InlineData("Niszczysz golema swoim mieczem.", 55)]
    [InlineData("NISZCZYSZ golema swoim mieczem.", 60)]
    [InlineData("DRUZGOCZESZ golema swoim mieczem.", 67)]
    [InlineData("ROZPRUWASZ golema swoim mieczem.", 75)]
    [InlineData("ROZRYWASZ golema swoim mieczem.", 84)]
    [InlineData("ROZBEBESZASZ golema swoim mieczem.", 100)]
    [InlineData("DEKAPITUJESZ golema swoim mieczem.", 115)]
    [InlineData("EKSTYRPUJESZ golema swoim mieczem.", 130)]
    [InlineData("ANIHILUJESZ golema swoim mieczem.", 145)]
    [InlineData("USMIERCASZ golema swoim mieczem.", 200)]
    [InlineData("UNICESTWIASZ golema swoim mieczem.", 201)]
    public void TryGetDamage_RecognizesEveryTier(string line, int expected)
    {
        Assert.True(DamagePhrases.TryGetDamage(line, out var damage));
        Assert.Equal(expected, damage);
    }

    [Theory]
    // 3rd-person forms mean someone/something else is the subject — a mob hitting you, or
    // bystander-visible combat between others — not damage the local character dealt.
    [InlineData("Golem cię rani swoją pięścią.")]
    [InlineData("Golem chybia.")]
    [InlineData("Golem rani Aragorna swoją pięścią.")]
    [InlineData("Golem cię niszczy swoją pięścią.")]
    public void TryGetDamage_IgnoresThirdPersonForms(string line)
    {
        Assert.False(DamagePhrases.TryGetDamage(line, out _));
    }

    [Fact]
    public void TryGetDamage_NoRecognizedPhrase_ReturnsFalse()
    {
        Assert.False(DamagePhrases.TryGetDamage("Rozglądasz się dookoła.", out _));
    }

    [Fact]
    public void TryGetDamage_MultiWordTier_DoesNotMatchTheShorterTierInsideIt()
    {
        // "Ledwie ranisz" must win over the bare "ranisz" (18) it contains.
        Assert.True(DamagePhrases.TryGetDamage("Ledwie ranisz golema.", out var damage));
        Assert.Equal(10, damage);
    }

    [Fact]
    public void TryGetDamage_StripsAnsiBeforeMatching()
    {
        var line = "[31mRanisz golema mieczem.[0m";

        Assert.True(DamagePhrases.TryGetDamage(line, out var damage));
        Assert.Equal(18, damage);
    }

    [Fact]
    public void TryGetDamage_RequiresWholeWordMatch()
    {
        // A made-up word containing "ranisz" as a substring must not match.
        Assert.False(DamagePhrases.TryGetDamage("Zaraniszowujesz coś dziwnego.", out _));
    }
}
