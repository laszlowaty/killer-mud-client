using System.Text;

namespace MudClient.App.Services;

/// <summary>
/// Keeps complete MUD lines in receive order until trigger JavaScript decides
/// whether each line should be displayed. Incomplete prompts are released on
/// the next UI pass so the terminal remains responsive. Complete lines decoded
/// from one received text batch are emitted together to avoid line-by-line rendering.
/// </summary>
internal sealed class TriggerOutputCoordinator(
    Action<string> emit,
    Action<Action> schedule)
{
    private readonly object _gate = new();
    private readonly Queue<PendingLine> _lines = new();
    private readonly StringBuilder _partial = new();
    private bool _partialFlushScheduled;
    private long _nextBatchId;
    private long _nextLineId;

    public void Feed(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        var completedLine = new StringBuilder();
        var shouldSchedulePartialFlush = false;
        lock (_gate)
        {
            var batchId = ++_nextBatchId;
            foreach (var character in text)
            {
                completedLine.Append(character);
                if (character != '\n')
                {
                    continue;
                }

                _partial.Append(completedLine);
                _lines.Enqueue(new PendingLine(++_nextLineId, batchId, _partial.ToString()));
                _partial.Clear();
                completedLine.Clear();
            }

            _partial.Append(completedLine);
            shouldSchedulePartialFlush = RequestPartialFlush();
        }

        if (shouldSchedulePartialFlush)
        {
            schedule(FlushPartial);
        }
    }

    public long ClaimNextLine()
    {
        lock (_gate)
        {
            var line = _lines.FirstOrDefault(candidate => !candidate.IsClaimed);
            if (line is null)
            {
                return 0;
            }

            line.IsClaimed = true;
            return line.Id;
        }
    }

    public void ResolveLine(long lineId, bool deleteLine)
    {
        string output;
        var shouldSchedulePartialFlush = false;
        lock (_gate)
        {
            var line = _lines.FirstOrDefault(candidate => candidate.Id == lineId);
            if (line is null)
            {
                return;
            }

            line.IsResolved = true;
            line.ShouldDelete = deleteLine;
            output = DrainResolvedLines();
            shouldSchedulePartialFlush = RequestPartialFlush();
        }

        if (output.Length > 0)
        {
            emit(output);
        }

        if (shouldSchedulePartialFlush)
        {
            schedule(FlushPartial);
        }
    }

    private bool RequestPartialFlush()
    {
        if (_lines.Count > 0 || _partial.Length == 0 || _partialFlushScheduled)
        {
            return false;
        }

        _partialFlushScheduled = true;
        return true;
    }

    private void FlushPartial()
    {
        string output;
        lock (_gate)
        {
            _partialFlushScheduled = false;
            output = DrainResolvedLines();
            if (_lines.Count == 0 && _partial.Length > 0)
            {
                output += _partial.ToString();
                _partial.Clear();
            }
        }

        if (output.Length > 0)
        {
            emit(output);
        }
    }

    private string DrainResolvedLines()
    {
        var output = new StringBuilder();
        while (_lines.TryPeek(out var line))
        {
            var batchId = line.BatchId;
            var isBatchResolved = true;
            foreach (var candidate in _lines)
            {
                if (candidate.BatchId != batchId)
                {
                    break;
                }

                if (!candidate.IsResolved)
                {
                    isBatchResolved = false;
                    break;
                }
            }

            if (!isBatchResolved)
            {
                break;
            }

            while (_lines.TryPeek(out line) && line.BatchId == batchId)
            {
                _lines.Dequeue();
                if (!line.ShouldDelete)
                {
                    output.Append(line.Text);
                }
            }
        }

        return output.ToString();
    }

    private sealed class PendingLine(long id, long batchId, string text)
    {
        public long Id { get; } = id;

        public long BatchId { get; } = batchId;

        public string Text { get; } = text;

        public bool IsClaimed { get; set; }

        public bool IsResolved { get; set; }

        public bool ShouldDelete { get; set; }
    }
}
