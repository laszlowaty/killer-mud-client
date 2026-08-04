using System.Text;

namespace MudClient.App.Services;

/// <summary>
/// Coalesces bursts of network output into one scheduled UI callback. At most one callback
/// is queued at a time, so a temporarily busy renderer cannot create an unbounded dispatcher
/// backlog that retains every received text chunk and delays keyboard input.
/// </summary>
internal sealed class UiOutputBatcher(
    Action<string> emit,
    Action<Action> schedule)
{
    private readonly object _gate = new();
    private readonly StringBuilder _pending = new();
    private bool _isScheduled;

    public void Enqueue(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        var shouldSchedule = false;
        lock (_gate)
        {
            _pending.Append(text);
            if (!_isScheduled)
            {
                _isScheduled = true;
                shouldSchedule = true;
            }
        }

        if (shouldSchedule)
        {
            schedule(Drain);
        }
    }

    private void Drain()
    {
        string batch;
        lock (_gate)
        {
            batch = _pending.ToString();
            _pending.Clear();
        }

        var shouldScheduleNext = false;
        try
        {
            emit(batch);
        }
        finally
        {
            lock (_gate)
            {
                if (_pending.Length == 0)
                {
                    _isScheduled = false;
                }
                else
                {
                    shouldScheduleNext = true;
                }
            }

            if (shouldScheduleNext)
            {
                schedule(Drain);
            }
        }
    }
}
