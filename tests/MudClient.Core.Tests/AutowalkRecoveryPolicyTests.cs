using MudClient.Core.Automation;
using MudClient.Core.Gmcp;

namespace MudClient.Core.Tests;

public sealed class AutowalkRecoveryPolicyTests
{
    [Fact]
    public void GetLowMovementAction_AtTenPercent_UsesMemorizedRefresh()
    {
        var spells = new[]
        {
            new MemorizedSpell(1, 3, "Refresh", Memed: true, Meming: false),
        };

        var action = AutowalkRecoveryPolicy.GetLowMovementAction(10, 100, spells);

        Assert.Equal(LowMovementAction.CastRefresh, action);
    }

    [Fact]
    public void GetLowMovementAction_AtTenPercent_WithoutReadyRefresh_Rests()
    {
        var spells = new[]
        {
            new MemorizedSpell(1, 3, "refresh", Memed: false, Meming: true),
        };

        var action = AutowalkRecoveryPolicy.GetLowMovementAction(5, 50, spells);

        Assert.Equal(LowMovementAction.Rest, action);
    }

    [Fact]
    public void GetLowMovementAction_AboveTenPercent_DoesNothing()
    {
        var action = AutowalkRecoveryPolicy.GetLowMovementAction(11, 100, []);

        Assert.Equal(LowMovementAction.None, action);
    }

    [Theory]
    [InlineData("Brama jest zamknięta na klucz.")]
    [InlineData("Brama jest zamknieta na klucz.")]
    [InlineData("Brama jest zamknięta.")]
    [InlineData("Brama jest zamknieta.")]
    [InlineData("\u001b[31mBrama jest zamknięta na klucz.\u001b[0m")]
    public void IsLockedGateMessage_AcceptsPolishAndAsciiVariants(string line)
    {
        Assert.True(AutowalkRecoveryPolicy.IsLockedGateMessage(line));
    }

    [Theory]
    [InlineData("Drzwi są zamknięte.")]
    [InlineData("Brama otwiera się.")]
    public void IsLockedGateMessage_RejectsOtherLines(string line)
    {
        Assert.False(AutowalkRecoveryPolicy.IsLockedGateMessage(line));
    }
}
