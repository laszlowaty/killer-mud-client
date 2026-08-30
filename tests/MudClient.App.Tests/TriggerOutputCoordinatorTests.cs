using MudClient.App.Services;

namespace MudClient.App.Tests;

public sealed class TriggerOutputCoordinatorTests
{
    [Fact]
    public void ResolveNextLine_DeletesOnlySelectedCompleteLineAndPreservesOrder()
    {
        var output = new List<string>();
        var scheduled = new Queue<Action>();
        var coordinator = new TriggerOutputCoordinator(output.Add, scheduled.Enqueue);

        coordinator.Feed("pierwsza\nukryta\ntrzecia\n");
        coordinator.ResolveLine(coordinator.ClaimNextLine(), deleteLine: false);
        coordinator.ResolveLine(coordinator.ClaimNextLine(), deleteLine: true);
        coordinator.ResolveLine(coordinator.ClaimNextLine(), deleteLine: false);

        Assert.Equal(["pierwsza\n", "trzecia\n"], output);
    }

    [Fact]
    public void ResolveLine_WaitsForEarlierDecisionBeforeEmittingLaterLine()
    {
        var output = new List<string>();
        var coordinator = new TriggerOutputCoordinator(output.Add, _ => { });

        coordinator.Feed("pierwsza\ndruga\n");
        var first = coordinator.ClaimNextLine();
        var second = coordinator.ClaimNextLine();
        coordinator.ResolveLine(second, deleteLine: false);

        Assert.Empty(output);

        coordinator.ResolveLine(first, deleteLine: true);

        Assert.Equal(["druga\n"], output);
    }

    [Fact]
    public void ScheduledFlush_ShowsIncompletePrompt()
    {
        var output = new List<string>();
        var scheduled = new Queue<Action>();
        var coordinator = new TriggerOutputCoordinator(output.Add, scheduled.Enqueue);

        coordinator.Feed("HP: 100> ");
        Assert.Empty(output);

        Assert.True(scheduled.TryDequeue(out var flush));
        flush();

        Assert.Equal(["HP: 100> "], output);
    }
}
