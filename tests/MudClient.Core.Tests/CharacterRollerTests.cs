using MudClient.Core.Automation;

namespace MudClient.Core.Tests;

public sealed class CharacterRollerTests
{
    [Fact]
    public void CompleteFirstRoll_RequestsConfiguration()
    {
        var roller = new CharacterRoller();

        Assert.Equal(CharacterRollerAction.None, roller.ObserveLine("STR: 79  INT: 78"));
        Assert.Equal(CharacterRollerAction.None, roller.ObserveLine("WIS: 83  DEX: 70"));
        Assert.Equal(
            CharacterRollerAction.RequestConfiguration,
            roller.ObserveLine("CON: 87  CHA: 68"));

        Assert.Equal(465, roller.LastRoll?.Sum);
    }

    [Fact]
    public void Configure_IgnoresNullTargets_AndRollsAgainWhenRequiredTargetMisses()
    {
        var roller = RollerWithRoll(79, 99, 82, 99, 87, 99);
        var configuration = new CharacterRollerConfiguration(
            Sum: null,
            Strength: 79,
            Intelligence: null,
            Wisdom: 83,
            Dexterity: null,
            Constitution: 87,
            Charisma: null,
            FinishCharacterCreation: true);

        Assert.Equal(CharacterRollerAction.RollAgain, roller.Configure(configuration));

        roller.ObserveLine("STR: 79 INT: 1");
        roller.ObserveLine("WIS: 83 DEX: 1");
        Assert.Equal(
            CharacterRollerAction.FinishCharacterCreation,
            roller.ObserveLine("CON: 87 CHA: 1"));
    }

    [Fact]
    public void AcceptedRoll_CanStopWithoutFinishingCharacterCreation()
    {
        var roller = RollerWithRoll(80, 80, 80, 80, 80, 80);
        var configuration = new CharacterRollerConfiguration(
            Sum: 450,
            Strength: null,
            Intelligence: null,
            Wisdom: null,
            Dexterity: null,
            Constitution: null,
            Charisma: null,
            FinishCharacterCreation: false);

        Assert.Equal(CharacterRollerAction.Accepted, roller.Configure(configuration));
    }

    [Fact]
    public void StatRows_MayContainInterveningLines()
    {
        var roller = new CharacterRoller();

        roller.ObserveLine("STR: 79 INT: 78");
        roller.ObserveLine("linia dodatkowa");
        roller.ObserveLine("WIS: 83 DEX: 70");
        roller.ObserveLine("jeszcze jedna");

        Assert.Equal(
            CharacterRollerAction.RequestConfiguration,
            roller.ObserveLine("CON: 87 CHA: 68"));
    }

    [Fact]
    public void PauseForConfiguration_StopsAutomaticDecisionsUntilReconfigured()
    {
        var roller = RollerWithRoll(10, 10, 10, 10, 10, 10);
        var configuration = new CharacterRollerConfiguration(
            Sum: 500,
            Strength: null,
            Intelligence: null,
            Wisdom: null,
            Dexterity: null,
            Constitution: null,
            Charisma: null,
            FinishCharacterCreation: true);
        Assert.Equal(CharacterRollerAction.RollAgain, roller.Configure(configuration));

        roller.PauseForConfiguration();
        roller.ObserveLine("STR: 10 INT: 10");
        roller.ObserveLine("WIS: 10 DEX: 10");

        Assert.Equal(CharacterRollerAction.None, roller.ObserveLine("CON: 10 CHA: 10"));
        Assert.Equal(CharacterRollerAction.RollAgain, roller.Configure(configuration));
    }

    [Fact]
    public void ResetForNewSession_KeepsTargetsButRequestsConfigurationAgain()
    {
        var roller = RollerWithRoll(80, 80, 80, 80, 80, 80);
        var configuration = CharacterRollerConfiguration.Default with { Sum = 999 };
        roller.Configure(configuration);

        roller.ResetForNewSession();
        roller.ObserveLine("STR: 80 INT: 80");
        roller.ObserveLine("WIS: 80 DEX: 80");

        Assert.Equal(configuration, roller.Configuration);
        Assert.Equal(
            CharacterRollerAction.RequestConfiguration,
            roller.ObserveLine("CON: 80 CHA: 80"));
    }

    private static CharacterRoller RollerWithRoll(
        int strength,
        int intelligence,
        int wisdom,
        int dexterity,
        int constitution,
        int charisma)
    {
        var roller = new CharacterRoller();
        roller.ObserveLine($"STR: {strength} INT: {intelligence}");
        roller.ObserveLine($"WIS: {wisdom} DEX: {dexterity}");
        roller.ObserveLine($"CON: {constitution} CHA: {charisma}");
        return roller;
    }
}
