using MudClient.Core.Automation;
using MudClient.Core.Gmcp;

namespace MudClient.Core.Tests;

public sealed class GroupExhaustionRefreshPolicyTests
{
    private readonly GroupExhaustionRefreshPolicy _policy = new();

    private static CharacterGroupMember Member(
        string name, int? mvScale, bool isNpc = false, bool isLeader = false) =>
        new(name, null, string.Empty, null, string.Empty, mvScale, null, isNpc, null, isLeader);

    [Fact]
    public void GetMembersToOrder_ExhaustedMember_ReturnsItOnce()
    {
        var group = new CharacterGroupUpdate("Hero", [Member("Hero", 4, isLeader: true), Member("Companion", 0)]);

        var first = _policy.GetMembersToOrder(true, group, "Hero");
        var second = _policy.GetMembersToOrder(true, group, "Hero");

        Assert.Equal(["Companion"], first);
        Assert.Empty(second);
    }

    [Fact]
    public void GetMembersToOrder_RecoveringThenExhaustedAgain_RefiresOnSecondExhaustion()
    {
        var exhausted = new CharacterGroupUpdate("Hero", [Member("Hero", 4, isLeader: true), Member("Companion", 0)]);
        var recovered = new CharacterGroupUpdate("Hero", [Member("Hero", 4, isLeader: true), Member("Companion", 4)]);

        Assert.Equal(["Companion"], _policy.GetMembersToOrder(true, exhausted, "Hero"));
        Assert.Empty(_policy.GetMembersToOrder(true, exhausted, "Hero"));
        Assert.Empty(_policy.GetMembersToOrder(true, recovered, "Hero"));
        Assert.Equal(["Companion"], _policy.GetMembersToOrder(true, exhausted, "Hero"));
    }

    [Fact]
    public void GetMembersToOrder_ExcludesSelfAndNpcs()
    {
        var group = new CharacterGroupUpdate("Hero",
        [
            Member("Hero", 0, isLeader: true),
            Member("Wolf", 0, isNpc: true),
        ]);

        Assert.Empty(_policy.GetMembersToOrder(true, group, "Hero"));
    }

    [Fact]
    public void GetMembersToOrder_Disabled_ReturnsEmptyAndClearsState()
    {
        var group = new CharacterGroupUpdate("Hero", [Member("Hero", 4, isLeader: true), Member("Companion", 0)]);

        Assert.Equal(["Companion"], _policy.GetMembersToOrder(true, group, "Hero"));
        Assert.Empty(_policy.GetMembersToOrder(false, group, "Hero"));

        // Re-enabling with the same still-exhausted member fires again, since disabling cleared it.
        Assert.Equal(["Companion"], _policy.GetMembersToOrder(true, group, "Hero"));
    }

    [Fact]
    public void GetMembersToOrder_MemberLeavesGroup_ReArmsOnRejoin()
    {
        var withMember = new CharacterGroupUpdate("Hero", [Member("Hero", 4, isLeader: true), Member("Companion", 0)]);
        var withoutMember = new CharacterGroupUpdate("Hero", [Member("Hero", 4, isLeader: true)]);

        Assert.Equal(["Companion"], _policy.GetMembersToOrder(true, withMember, "Hero"));
        Assert.Empty(_policy.GetMembersToOrder(true, withoutMember, "Hero"));
        Assert.Equal(["Companion"], _policy.GetMembersToOrder(true, withMember, "Hero"));
    }

    [Fact]
    public void GetMembersToOrder_NullGroup_ReturnsEmpty()
    {
        Assert.Empty(_policy.GetMembersToOrder(true, null, "Hero"));
    }
}
