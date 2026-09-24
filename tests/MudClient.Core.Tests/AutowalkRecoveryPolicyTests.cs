using MudClient.Core.Automation;
using MudClient.Core.Gmcp;

namespace MudClient.Core.Tests;

public sealed class AutowalkRecoveryPolicyTests
{
    [Fact]
    public void GetGateOpeningCommands_KnocksBeforeOtherAttempts()
    {
        Assert.Equal(
            ["zapukaj", "pull", "pociagnij", "uderz"],
            AutowalkRecoveryPolicy.GetGateOpeningCommands());
    }

    [Fact]
    public void GetLowMovementAction_AtTenPercent_UsesMemorizedRefresh()
    {
        var spells = new[]
        {
            new MemorizedSpell(1, 3, "Refresh", Memed: true, Meming: false),
        };

        var action = AutowalkRecoveryPolicy.GetLowMovementAction(
            10, 100, spells, useRefreshes: true);

        Assert.Equal(LowMovementAction.CastRefresh, action);
    }

    [Fact]
    public void GetLowMovementAction_AtTenPercent_WithoutReadyRefresh_Rests()
    {
        var spells = new[]
        {
            new MemorizedSpell(1, 3, "refresh", Memed: false, Meming: true),
        };

        var action = AutowalkRecoveryPolicy.GetLowMovementAction(
            5, 50, spells, useRefreshes: true);

        Assert.Equal(LowMovementAction.Rest, action);
    }

    [Fact]
    public void GetLowMovementAction_RefreshesDisabled_RestsDespiteReadySpell()
    {
        var spells = new[]
        {
            new MemorizedSpell(1, 3, "refresh", Memed: true, Meming: false),
        };

        var action = AutowalkRecoveryPolicy.GetLowMovementAction(
            10, 100, spells, useRefreshes: false);

        Assert.Equal(LowMovementAction.Rest, action);
    }

    [Fact]
    public void GetLowMovementAction_AboveTenPercent_DoesNothing()
    {
        var action = AutowalkRecoveryPolicy.GetLowMovementAction(
            11, 100, [], useRefreshes: true);

        Assert.Equal(LowMovementAction.None, action);
    }

    [Theory]
    [InlineData(false, "rest")]
    [InlineData(true, "rest", "recuperate")]
    public void GetRestCommands_ReturnsConfiguredSequence(bool useRecuperate, params string[] expected)
    {
        Assert.Equal(expected, AutowalkRecoveryPolicy.GetRestCommands(useRecuperate));
    }

    [Fact]
    public void GetPostRestCommands_ReadyFloatAndRefresh_CastsBothAfterStanding()
    {
        MemorizedSpell[] spells =
        [
            new(1, 3, "float", Memed: true, Meming: false),
            new(2, 3, "refresh", Memed: true, Meming: false),
        ];

        Assert.Equal(
            ["stand", "cast 'float' self", "cast 'refresh' self"],
            AutowalkRecoveryPolicy.GetPostRestCommands(spells, castRefresh: true));
    }

    [Fact]
    public void GetPostRestCommands_UnreadySpells_OnlyStands()
    {
        MemorizedSpell[] spells =
        [
            new(1, 3, "float", Memed: false, Meming: true),
            new(2, 3, "refresh", Memed: false, Meming: true),
        ];

        Assert.Equal(
            ["stand"],
            AutowalkRecoveryPolicy.GetPostRestCommands(spells, castRefresh: true));
    }

    [Fact]
    public void GetPostRestCommands_RefreshWakeDisabled_DoesNotCastRefresh()
    {
        MemorizedSpell[] spells =
        [
            new(1, 3, "float", Memed: true, Meming: false),
            new(2, 3, "refresh", Memed: true, Meming: false),
        ];

        Assert.Equal(
            ["stand", "cast 'float' self"],
            AutowalkRecoveryPolicy.GetPostRestCommands(spells, castRefresh: false));
    }

    [Theory]
    [InlineData("fighting")]
    [InlineData("Fighting")]
    [InlineData("FIGHTING")]
    public void IsCombatPosition_RecognizesFighting(string position)
    {
        Assert.True(AutowalkRecoveryPolicy.IsCombatPosition(position));
    }

    [Theory]
    [InlineData("standing")]
    [InlineData("resting")]
    [InlineData("sitting")]
    [InlineData("")]
    [InlineData(null)]
    public void IsCombatPosition_RejectsNonCombatPositions(string? position)
    {
        Assert.False(AutowalkRecoveryPolicy.IsCombatPosition(position));
    }

    [Theory]
    [InlineData("sleeping")]
    [InlineData("SLEEPING")]
    [InlineData("sitting")]
    [InlineData("SITTING")]
    public void RequiresStandBeforeMovement_RecognizesNonStandingPositions(string position)
    {
        Assert.True(AutowalkRecoveryPolicy.RequiresStandBeforeMovement(position));
    }

    [Theory]
    [InlineData("standing")]
    [InlineData("resting")]
    [InlineData("RESTING")]
    [InlineData("fighting")]
    [InlineData("")]
    [InlineData(null)]
    public void RequiresStandBeforeMovement_RejectsOtherPositions(string? position)
    {
        Assert.False(AutowalkRecoveryPolicy.RequiresStandBeforeMovement(position));
    }

    [Theory]
    [InlineData("standing", true)]
    [InlineData("Standing", true)]
    [InlineData("sitting", false)]
    [InlineData("fighting", false)]
    [InlineData(null, false)]
    public void IsStandingPosition_RecognizesOnlyStanding(string? position, bool expected)
    {
        Assert.Equal(expected, AutowalkRecoveryPolicy.IsStandingPosition(position));
    }
}
