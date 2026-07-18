using MudClient.Core.Automation;
using MudClient.Core.Gmcp;

namespace MudClient.Core.Tests;

public sealed class AutoAssistPolicyTests
{
    private readonly AutoAssistPolicy _policy = new();

    [Fact]
    public void ShouldAssist_FightingGroupMemberInCurrentRoom_ReturnsTrueOnce()
    {
        var group = Group(Member("Ala", "fighting", "100"));

        Assert.True(_policy.ShouldAssist(true, "100", "Ja", false, group, [], []));
        Assert.False(_policy.ShouldAssist(true, "100", "Ja", false, group, [], []));
    }

    [Fact]
    public void ShouldAssist_RoomPeopleCanConfirmFight()
    {
        var group = Group(Member("Ala", "standing", "100"));
        RoomPerson[] people = [new("Ala", IsFighting: true, Enemy: "ork")];

        Assert.True(_policy.ShouldAssist(true, "100", "Ja", false, group, people, []));
    }

    [Theory]
    [InlineData(false, "100", "Ala", "fighting", "100")]
    [InlineData(true, "100", "Ala", "fighting", "100")]
    [InlineData(true, "100", "Ala", "fighting", "101")]
    [InlineData(true, null, "Ala", "fighting", "100")]
    public void ShouldAssist_InvalidSituation_ReturnsFalse(
        bool enabled,
        string? currentRoom,
        string selfName,
        string position,
        string memberRoom)
    {
        var group = Group(Member("Ala", position, memberRoom));

        Assert.False(_policy.ShouldAssist(enabled, currentRoom, selfName, false, group, [], []));
    }

    [Fact]
    public void ShouldAssist_AfterFightEnds_CanFireForNextFight()
    {
        var fighting = Group(Member("Ala", "fighting", "100"));
        var standing = Group(Member("Ala", "standing", "100"));

        Assert.True(_policy.ShouldAssist(true, "100", "Ja", false, fighting, [], []));
        Assert.False(_policy.ShouldAssist(true, "100", "Ja", false, standing, [], []));
        Assert.True(_policy.ShouldAssist(true, "100", "Ja", false, fighting, [], []));
    }

    [Fact]
    public void ShouldAssist_WhenSelfStopsFightingAndMemberContinues_ReturnsTrueAgain()
    {
        var group = Group(Member("Ala", "fighting", "100"));

        Assert.True(_policy.ShouldAssist(true, "100", "Ja", false, group, [], []));
        Assert.False(_policy.ShouldAssist(true, "100", "Ja", true, group, [], []));
        Assert.True(_policy.ShouldAssist(true, "100", "Ja", false, group, [], []));
    }

    [Fact]
    public void ShouldAssist_MemberFightsExcludedEnemy_ReturnsFalse()
    {
        var group = Group(Member("Ala", "fighting", "100"));
        RoomPerson[] people = [new("Ala", IsFighting: true, Enemy: "Wielki smok")];

        Assert.False(_policy.ShouldAssist(
            true,
            "100",
            "Ja",
            false,
            group,
            people,
            ["  wielki SMOK "]));
    }

    [Fact]
    public void ShouldAssist_ExcludedNameIsNotSubstringMatch()
    {
        var group = Group(Member("Ala", "fighting", "100"));
        RoomPerson[] people = [new("Ala", IsFighting: true, Enemy: "Wielki smok")];

        Assert.True(_policy.ShouldAssist(true, "100", "Ja", false, group, people, ["smok"]));
    }

    [Fact]
    public void ShouldAssist_AnotherMemberFightsNonExcludedEnemy_ReturnsTrue()
    {
        var group = Group(
            Member("Ala", "fighting", "100"),
            Member("Ela", "fighting", "100"));
        RoomPerson[] people =
        [
            new("Ala", IsFighting: true, Enemy: "Wielki smok"),
            new("Ela", IsFighting: true, Enemy: "Ork"),
        ];

        Assert.True(_policy.ShouldAssist(
            true,
            "100",
            "Ja",
            false,
            group,
            people,
            ["Wielki smok"]));
    }

    [Fact]
    public void ShouldAssist_ExclusionRearmsPolicyWhenRemoved()
    {
        var group = Group(Member("Ala", "fighting", "100"));
        RoomPerson[] people = [new("Ala", IsFighting: true, Enemy: "Ork")];

        Assert.True(_policy.ShouldAssist(true, "100", "Ja", false, group, people, []));
        Assert.False(_policy.ShouldAssist(true, "100", "Ja", false, group, people, ["Ork"]));
        Assert.True(_policy.ShouldAssist(true, "100", "Ja", false, group, people, []));
    }

    [Fact]
    public void ShouldAssist_WithExclusions_WaitsForRoomPeopleEnemyBeforeDecision()
    {
        var group = Group(Member("Ala", "fighting", "100"));
        RoomPerson[] beforeFight = [new("Ala", IsFighting: false, Enemy: null)];
        RoomPerson[] excludedFight = [new("Ala", IsFighting: true, Enemy: "Służący")];
        RoomPerson[] allowedFight = [new("Ala", IsFighting: true, Enemy: "Ork")];

        Assert.False(_policy.ShouldAssist(
            true, "100", "Ja", false, group, beforeFight, ["Służący"]));
        Assert.False(_policy.ShouldAssist(
            true, "100", "Ja", false, group, excludedFight, ["Służący"]));
        Assert.True(_policy.ShouldAssist(
            true, "100", "Ja", false, group, allowedFight, ["Służący"]));
    }

    private static CharacterGroupUpdate Group(params CharacterGroupMember[] members) =>
        new(null, members);

    private static CharacterGroupMember Member(string name, string position, string room) =>
        new(name, position, "", null, "", null, null, false, room, false);
}
