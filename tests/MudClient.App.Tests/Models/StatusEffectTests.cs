using MudClient.App.Models;
using MudClient.Core.Gmcp;

namespace MudClient.App.Tests.Models;

/// <summary>
/// Unit tests for <see cref="StatusEffect.FromCore"/> mapping and
/// derived properties.  Each test exercises the icon-selection rule:
///
///   Ending ? "[!]" : (Negative ? "[-]" : "[+]")
///
/// and verifies that <c>IsDebuff</c> follows <c>Negative</c>
/// independently of <c>Ending</c>.
///
/// XAML binding coverage note:
///   MainWindow.axaml uses two <c>Border</c> elements — one visible when
///   <c>IsDebuff</c> is true, the other when false.  Both branches bind
///   <c>Text="{Binding Icon}"</c>.  Verifying which Border appears at
///   runtime requires a headless Avalonia platform, which the test project
///   does not reference.  The model-level tests below confirm that every
///   combination of Negative/Ending produces the correct Icon and IsDebuff
///   values; the XAML is validated at build time (XAML compile) and
///   manually.
/// </summary>
public sealed class StatusEffectTests
{
    // ====================================================================
    // FromCore — all four Negative / Ending combinations
    // ====================================================================

    [Fact]
    public void FromCore_BuffNotEnding()
    {
        var affect = new CharacterAffect(
            "Błogosławieństwo", "Zwiększa celność",
            Negative: false, Ending: false, ExtraValue: "10m");

        var effect = StatusEffect.FromCore(affect);

        Assert.Equal("Błogosławieństwo", effect.Name);
        Assert.Equal("[+]", effect.Icon);
        Assert.False(effect.IsDebuff);
        Assert.False(effect.Negative);
        Assert.False(effect.Ending);
        Assert.Equal("10m", effect.Duration);
        Assert.Equal("Zwiększa celność", effect.Description);
        Assert.True(effect.HasDescription);
    }

    [Fact]
    public void FromCore_DebuffNotEnding()
    {
        var affect = new CharacterAffect(
            "Zatrucie", "Trucizna w organizmie",
            Negative: true, Ending: false, ExtraValue: "30s");

        var effect = StatusEffect.FromCore(affect);

        Assert.Equal("Zatrucie", effect.Name);
        Assert.Equal("[-]", effect.Icon);
        Assert.True(effect.IsDebuff);
        Assert.True(effect.Negative);
        Assert.False(effect.Ending);
        Assert.Equal("30s", effect.Duration);
        Assert.Equal("Trucizna w organizmie", effect.Description);
        Assert.True(effect.HasDescription);
    }

    [Fact]
    public void FromCore_BuffEnding()
    {
        var affect = new CharacterAffect(
            "Krótki buff", "Za chwilę zniknie",
            Negative: false, Ending: true, ExtraValue: "5s");

        var effect = StatusEffect.FromCore(affect);

        Assert.Equal("Krótki buff", effect.Name);
        // Ending takes precedence over Negative → "[!]" not "[+]"
        Assert.Equal("[!]", effect.Icon);
        Assert.False(effect.IsDebuff);
        Assert.False(effect.Negative);
        Assert.True(effect.Ending);
        Assert.Equal("5s", effect.Duration);
        Assert.Equal("Za chwilę zniknie", effect.Description);
        Assert.True(effect.HasDescription);
    }

    [Fact]
    public void FromCore_DebuffEnding()
    {
        var affect = new CharacterAffect(
            "Trucizna kończy się", "Ostatnie chwile",
            Negative: true, Ending: true, ExtraValue: "3s");

        var effect = StatusEffect.FromCore(affect);

        // Ending takes precedence over Negative → Icon is "[!]" not "[-]"
        Assert.Equal("[!]", effect.Icon);
        // IsDebuff follows Negative, independent of Ending
        Assert.True(effect.IsDebuff);
        Assert.True(effect.Negative);
        Assert.True(effect.Ending);
        Assert.Equal("Trucizna kończy się", effect.Name);
        Assert.Equal("3s", effect.Duration);
        Assert.Equal("Ostatnie chwile", effect.Description);
        Assert.True(effect.HasDescription);
    }

    // ====================================================================
    // Edge cases
    // ====================================================================

    [Fact]
    public void FromCore_NullExtraValue_EmptyDuration()
    {
        var affect = new CharacterAffect(
            "Test", "desc",
            Negative: false, Ending: false, ExtraValue: null);

        var effect = StatusEffect.FromCore(affect);

        Assert.Empty(effect.Duration);
        Assert.Null(effect.ExtraValue);
    }

    [Fact]
    public void FromCore_EmptyDescription_HasDescriptionFalse()
    {
        var affect = new CharacterAffect(
            "NoDesc", string.Empty,
            Negative: false, Ending: false, ExtraValue: null);

        var effect = StatusEffect.FromCore(affect);

        Assert.False(effect.HasDescription);
    }

    // ====================================================================
    // HasDuration  (Duration = ExtraValue ?? string.Empty)
    // ====================================================================

    [Fact]
    public void HasDuration_WithValue_ReturnsTrue()
    {
        var effect = new StatusEffect(
            "Test", "[+]", "10m",
            false, "desc", false, false, "10m");

        Assert.True(effect.HasDuration);
    }

    [Fact]
    public void HasDuration_EmptyString_ReturnsFalse()
    {
        var effect = new StatusEffect(
            "Test", "[+]", string.Empty,
            false, "desc", false, false, null);

        Assert.False(effect.HasDuration);
    }

    [Fact]
    public void HasDuration_WhitespaceString_ReturnsFalse()
    {
        var effect = new StatusEffect(
            "Test", "[+]", "   ",
            false, "desc", false, false, "   ");

        Assert.False(effect.HasDuration);
    }

    [Fact]
    public void FromCore_HasDuration_FollowsExtraValue()
    {
        // ExtraValue null → Duration empty → HasDuration false
        var nullEv = new CharacterAffect(
            "A", "d", Negative: false, Ending: false, ExtraValue: null);
        Assert.False(StatusEffect.FromCore(nullEv).HasDuration);

        // ExtraValue "5m" → Duration "5m" → HasDuration true
        var withEv = new CharacterAffect(
            "B", "d", Negative: false, Ending: false, ExtraValue: "5m");
        Assert.True(StatusEffect.FromCore(withEv).HasDuration);
    }

    // ====================================================================
    // Existing test coverage note:
    //   IsDebuff  — exercised in all four FromCore tests above.
    //   Icon      — exercised in all four FromCore tests above.
    //   HasDescription — exercised in FromCore_EmptyDescription_HasDescriptionFalse
    //                    and in all four FromCore tests (positive).
    //   HasDuration    — see HasDuration_* and FromCore_HasDuration_* above.
    //   Duration       — exercised in all four FromCore tests and
    //                    FromCore_NullExtraValue_EmptyDuration above.
    // ====================================================================
}
