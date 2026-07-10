using MudClient.App.Models;
using MudClient.Core.Gmcp;

namespace MudClient.App.Tests.Models;

public sealed class GroupMemberTests
{
    // ====================================================================
    // Constructor – RoomDisplay is set to the provided value
    // ====================================================================

    [Fact]
    public void Constructor_SetsRoomDisplayToGivenValue()
    {
        var member = new GroupMember(
            "Test", false, null, "hp", null, "mv", null, null, false,
            "100", "Resolved Name");

        Assert.Equal("Resolved Name", member.RoomDisplay);
        Assert.Equal("100", member.Room);
    }

    [Fact]
    public void Constructor_RoomDisplayIsIndependentOfRoom()
    {
        var member = new GroupMember(
            "Test", false, null, "hp", null, "mv", null, null, false,
            "999", "Town Square");

        Assert.Equal("999", member.Room);
        Assert.Equal("Town Square", member.RoomDisplay);
    }

    // ====================================================================
    // FromCore – default roomDisplay argument
    // ====================================================================

    [Fact]
    public void FromCore_WithoutRoomDisplay_DefaultsToCoreRoom()
    {
        var core = new CharacterGroupMember(
            "Hero", "standing", "zadnych sladow", 7, "wypoczety", 4, 0,
            false, "Temple", true);

        var member = GroupMember.FromCore(core);

        Assert.Equal("Temple", member.RoomDisplay);
        Assert.Equal("Temple", member.Room);
    }

    [Fact]
    public void FromCore_WithNullRoom_DefaultsToQuestionMark()
    {
        var core = new CharacterGroupMember(
            "Hero", "standing", "zadnych sladow", 7, "wypoczety", 4, 0,
            false, null, true);

        var member = GroupMember.FromCore(core);

        Assert.Equal("?", member.RoomDisplay);
        Assert.Null(member.Room);
    }

    // ====================================================================
    // FromCore – explicit roomDisplay override
    // ====================================================================

    [Fact]
    public void FromCore_WithExplicitRoomDisplay_UsesProvidedValue()
    {
        var core = new CharacterGroupMember(
            "Hero", "standing", "zadnych sladow", 7, "wypoczety", 4, 0,
            false, "Temple", true);

        var member = GroupMember.FromCore(core, "Great Temple of Light");

        Assert.Equal("Great Temple of Light", member.RoomDisplay);
        Assert.Equal("Temple", member.Room);  // raw Room is unaffected
    }

    [Fact]
    public void FromCore_WithExplicitRoomDisplayAndNullRoom_UsesProvidedValue()
    {
        var core = new CharacterGroupMember(
            "Hero", "standing", "zadnych sladow", 7, "wypoczety", 4, 0,
            false, null, true);

        var member = GroupMember.FromCore(core, "Safe Room");

        Assert.Equal("Safe Room", member.RoomDisplay);
        Assert.Null(member.Room);
    }

    // ====================================================================
    // Other derived properties are unaffected by RoomDisplay
    // ====================================================================

    [Fact]
    public void OtherDerivedProperties_UnaffectedByRoomDisplay()
    {
        var member = new GroupMember(
            "Hero", true, "standing", "zadnych sladow", 7, "wypoczety", 4, 3,
            false, "Temple", "Great Temple");

        Assert.Equal("*", member.LeaderMarker);
        Assert.Equal("standing", member.PositionDisplay);
        Assert.Equal("zadnych sladow (7/7)", member.HpDisplay);
        Assert.Equal("wypoczety (4/4)", member.MvDisplay);
        Assert.Equal("MEM 3", member.MemDisplay);
        Assert.Equal("[Gracz]", member.NpcDisplay);
    }

    [Fact]
    public void RoomDisplay_DoesNotAffectNpcDisplay()
    {
        var member = new GroupMember(
            "Orc", false, null, "hp", null, "mv", null, null, true,
            null, "some room");

        Assert.Equal("[NPC]", member.NpcDisplay);
    }
}
