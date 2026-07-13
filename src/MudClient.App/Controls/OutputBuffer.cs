using Avalonia.Media.TextFormatting;

namespace MudClient.App.Controls;

internal readonly record struct OutputSegment(string Text, AnsiStyle Style);

/// <summary>
/// One logical output line: styled segments plus lazily built plain text.
/// A line is mutable only while it is the last (current) line of the buffer.
/// </summary>
internal sealed class OutputLine
{
    private readonly List<OutputSegment> _segments = [];
    private string? _cachedText;

    internal int MutationVersion;

    public IReadOnlyList<OutputSegment> Segments => _segments;

    public int Length { get; private set; }

    public string Text => _cachedText ??= BuildText();

    public void Append(string text, AnsiStyle style)
    {
        if (text.Length == 0)
        {
            return;
        }

        if (_segments.Count > 0 && _segments[^1].Style == style)
        {
            _segments[^1] = new OutputSegment(_segments[^1].Text + text, style);
        }
        else
        {
            _segments.Add(new OutputSegment(text, style));
        }

        Length += text.Length;
        _cachedText = null;
        MutationVersion++;
    }

    private string BuildText()
    {
        if (_segments.Count == 1)
        {
            return _segments[0].Text;
        }

        var builder = new System.Text.StringBuilder(Length);
        foreach (var segment in _segments)
        {
            builder.Append(segment.Text);
        }

        return builder.ToString();
    }
}

/// <summary>
/// Ring buffer of output lines with O(1) append and O(1) trim. The last line is always
/// the incomplete "current" line so prompts without a trailing newline show up immediately.
/// Line positions are addressed by a monotonically growing global index, so trimming old
/// lines never shifts selection anchors held by views.
/// </summary>
internal sealed class OutputBuffer
{
    private readonly OutputLine[] _lines;
    private int _start;

    public OutputBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 2);
        _lines = new OutputLine[capacity];
        _lines[0] = new OutputLine();
        Count = 1;
    }

    /// <summary>Number of lines currently held, including the incomplete current line.</summary>
    public int Count { get; private set; }

    public int Capacity => _lines.Length;

    /// <summary>Global index of the oldest line still in the buffer.</summary>
    public long FirstGlobalIndex { get; private set; }

    /// <summary>
    /// Longest line (in characters) seen so far; used only to size the horizontal scrollbar,
    /// so it is deliberately monotonic instead of being recomputed on trim.
    /// </summary>
    public int MaxLineLength { get; private set; }

    /// <summary>Raised with the number of lines dropped from the front of the buffer.</summary>
    public event Action<int>? LinesTrimmed;

    public OutputLine this[int index] => _lines[(_start + index) % _lines.Length];

    public void Append(string text, AnsiStyle style)
    {
        var current = this[Count - 1];
        current.Append(text, style);

        if (current.Length > MaxLineLength)
        {
            MaxLineLength = current.Length;
        }
    }

    public void CompleteLine()
    {
        if (Count == _lines.Length)
        {
            _lines[_start] = null!;
            _start = (_start + 1) % _lines.Length;
            Count--;
            FirstGlobalIndex++;
            LinesTrimmed?.Invoke(1);
        }

        _lines[(_start + Count) % _lines.Length] = new OutputLine();
        Count++;
    }

    public void Clear()
    {
        Array.Clear(_lines);
        _start = 0;
        Count = 1;
        _lines[0] = new OutputLine();
        FirstGlobalIndex = 0;
        MaxLineLength = 0;
    }

    public string GetAllText()
    {
        var builder = new System.Text.StringBuilder();
        for (var i = 0; i < Count; i++)
        {
            if (i > 0)
            {
                builder.Append(Environment.NewLine);
            }

            builder.Append(this[i].Text);
        }

        return builder.ToString();
    }
}
