using MudClient.Core.Automation;
using MudClient.Core.Gmcp;

namespace MudClient.Core.Tests;

public sealed class GroupOrderPolicyTests
{
    [Fact]
    public void TryGetCommand_GroupMemberIssuedOrder_ReturnsOnlyCommand()
    {
        var group = Group(Member("Ala"));

        var matched = GroupOrderPolicy.TryGetCommand(
            "Ala rozkazuje ci 'zabij orka'.", "Ja", group, out var command);

        Assert.True(matched);
        Assert.Equal("zabij orka", command);
    }

    [Fact]
    public void TryGetCommand_IsCaseInsensitiveWhenCheckingGroupMember()
    {
        var group = Group(Member("ALA"));

        var matched = GroupOrderPolicy.TryGetCommand(
            "Ala rozkazuje ci 'wstan'.", "Ja", group, out var command);

        Assert.True(matched);
        Assert.Equal("wstan", command);
    }

    [Theory]
    [InlineData("Obcy rozkazuje ci 'zabij orka'.")]
    [InlineData("Ala rozkazuje ci 'zabij;uciekaj'.")]
    [InlineData("Ala rozkazuje ci 'zabij orka'")]
    [InlineData(" Ala rozkazuje ci 'zabij orka'.")]
    [InlineData("Ala mówi ci 'zabij orka'.")]
    public void TryGetCommand_InvalidOrUnauthorizedLine_ReturnsFalse(string line)
    {
        var group = Group(Member("Ala"));

        Assert.False(GroupOrderPolicy.TryGetCommand(line, "Ja", group, out var command));
        Assert.Equal(string.Empty, command);
    }

    [Fact]
    public void TryGetCommand_SelfIssuedOrder_ReturnsFalse()
    {
        var group = Group(Member("Ja"));

        Assert.False(GroupOrderPolicy.TryGetCommand(
            "Ja rozkazuje ci 'usiadz'.", "Ja", group, out _));
    }

    [Fact]
    public void TryGetCommand_WithoutGroupState_ReturnsFalse()
    {
        Assert.False(GroupOrderPolicy.TryGetCommand(
            "Ala rozkazuje ci 'wstan'.", "Ja", null, out _));
    }

    [Fact]
    public void TryGetCommand_NpcGroupMember_ReturnsFalse()
    {
        var group = Group(Member("Ala", isNpc: true));

        Assert.False(GroupOrderPolicy.TryGetCommand(
            "Ala rozkazuje ci 'wstan'.", "Ja", group, out _));
    }

    private static CharacterGroupUpdate Group(params CharacterGroupMember[] members) =>
        new(null, members);

    private static CharacterGroupMember Member(string name, bool isNpc = false) =>
        new(name, "standing", "", null, "", null, null, isNpc, "100", false);
}
