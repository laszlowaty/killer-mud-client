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
    public void TryGetDamage_RecognizesEverySelfVerbTier(string line, int expected)
    {
        Assert.True(DamagePhrases.TryGetDamage(line, out var damage));
        Assert.Equal(expected, damage);
    }

    [Theory]
    // "Twoje <technika> <3rd-person verb> <cel>." — the technique noun is the grammatical
    // subject, so the verb conjugates in 3rd person even though it's your own hit.
    [InlineData("Twoje niezdarne pchnięcie chybia.", 0)]
    [InlineData("Twoje szybkie cięcie siniaczy sędziwego krasnoluda.", 2)]
    [InlineData("Twoje delikatne draśnięcie muska sędziwego krasnoluda.", 6)]
    [InlineData("Twoje miażdżące walniecie dewastuje sedziwego krasnoluda.", 44)]
    [InlineData("Twoje potężne uderzenie niszczy sędziwego krasnoluda.", 55)]
    [InlineData("Twoje druzgoczące uderzenie NISZCZY sędziwego krasnoluda.", 60)]
    [InlineData("Twoje straszliwe cięcie UNICESTWIA sędziwego krasnoluda.", 201)]
    public void TryGetDamage_RecognizesTechniqueVerbTier_WhenLineNamesYourTechnique(
        string line, int expected)
    {
        Assert.True(DamagePhrases.TryGetDamage(line, out var damage));
        Assert.Equal(expected, damage);
    }

    [Theory]
    // Bare 3rd-person forms — no "Twoje"/"Twój" in the line — mean someone/something else is the
    // subject: a mob hitting you, or bystander-visible combat between others. Not your damage.
    [InlineData("Golem cię rani swoją pięścią.")]
    [InlineData("Golem chybia.")]
    [InlineData("Miażdżące uderzenie golema dewastuje cię.")]
    [InlineData("Miażdżące uderzenie golema dewastuje Aragorna.")]
    public void TryGetDamage_IgnoresThirdPersonForms_WithoutYourTechniqueNamed(string line)
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
        var esc = (char)0x1B;
        var line = $"{esc}[31mRanisz golema mieczem.{esc}[0m";

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
