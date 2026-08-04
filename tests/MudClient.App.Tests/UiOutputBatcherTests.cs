using MudClient.App.Services;
using Xunit;

namespace MudClient.App.Tests;

public sealed class UiOutputBatcherTests
{
    [Fact]
    public void BurstQueuesOneCallbackAndPreservesTextOrder()
    {
        var scheduled = new Queue<Action>();
        var emitted = new List<string>();
        var batcher = new UiOutputBatcher(emitted.Add, scheduled.Enqueue);

        batcher.Enqueue("pierwszy ");
        batcher.Enqueue("drugi ");
        batcher.Enqueue("trzeci");

        Assert.Single(scheduled);
        scheduled.Dequeue()();

        Assert.Equal(["pierwszy drugi trzeci"], emitted);
        Assert.Empty(scheduled);
    }

    [Fact]
    public void TextReceivedDuringEmissionIsScheduledAsNextBatch()
    {
        var scheduled = new Queue<Action>();
        var emitted = new List<string>();
        UiOutputBatcher? batcher = null;
        batcher = new UiOutputBatcher(
            text =>
            {
                emitted.Add(text);
                if (emitted.Count == 1)
                {
                    batcher!.Enqueue("później");
                }
            },
            scheduled.Enqueue);

        batcher.Enqueue("teraz");
        scheduled.Dequeue()();

        Assert.Single(scheduled);
        scheduled.Dequeue()();
        Assert.Equal(["teraz", "później"], emitted);
    }
}
