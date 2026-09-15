using MudClient.Core.Automation;

namespace MudClient.Core.Tests;

public sealed class MudTimerServiceTests
{
    [Fact]
    public async Task SeparateServices_RunTimersWithTheSameKeyIndependently()
    {
        await using var first = new MudTimerService();
        await using var second = new MudTimerService();
        var firstFired = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondFired = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        first.StartPeriodic(
            "user-timer:same-id",
            TimeSpan.FromMilliseconds(10),
            _ =>
            {
                firstFired.TrySetResult();
                return Task.CompletedTask;
            });
        second.StartPeriodic(
            "user-timer:same-id",
            TimeSpan.FromMilliseconds(10),
            _ =>
            {
                secondFired.TrySetResult();
                return Task.CompletedTask;
            });

        await Task.WhenAll(firstFired.Task, secondFired.Task)
            .WaitAsync(TimeSpan.FromSeconds(2));
    }
}
