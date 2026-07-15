using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Avalonia.Media.TextFormatting;
using Avalonia.Rendering;
using Avalonia.Utilities;
using System.Runtime.CompilerServices;

namespace MudClient.App.Controls;

/// <summary>
/// Virtualized MUD output pane. Renders only the lines visible in the viewport, so the cost
/// of drawing is independent of scrollback size — appending never re-lays-out the history.
/// Implements its own text selection (mouse drag + Ctrl+C) because per-line virtualization
/// rules out SelectableTextBlock.
/// </summary>
internal sealed class OutputPaneControl : Control, ILogicalScrollable, ICustomHitTest
{
    private const double ExtentWidthPadding = 8;
    private const double MouseWheelScrollLines = 4;

    private static readonly ImmutableSolidColorBrush DefaultForeground =
        new(Color.FromRgb(215, 221, 230));

    private static readonly ImmutableSolidColorBrush SelectionBrush =
        new(Color.FromArgb(96, 51, 153, 255));

    private static readonly ImmutableSolidColorBrush ImageBackgroundBrush =
        new(Color.FromRgb(16, 19, 24));

    private static readonly Dictionary<uint, ImmutableSolidColorBrush> BrushCache = [];

    private OutputBuffer? _buffer;
    private Vector _offset;
    private Size _viewport;
    private bool _canHorizontallyScroll = true;
    private bool _canVerticallyScroll = true;
    private EventHandler? _scrollInvalidated;

    private FontFamily _fontFamily = new("Consolas");
    private double _fontSize = 14;
    private FontWeight _fontWeight = FontWeight.Normal;
    private Typeface _typeface;
    private Typeface _boldTypeface;
    private double _lineHeight = 16;
    private double _charWidth = 8;
    private int _fontVersion;
    private bool _wordWrap;
    private readonly ConditionalWeakTable<OutputLine, LayoutCache> _layoutCache = new();
    private OutputBuffer? _heightBuffer;
    private double[]? _indexedHeights;
    private long[]? _indexedGlobals;
    private HeightFenwickTree? _heightTree;
    private long _indexedFirst;
    private long _indexedLast = -1;
    private int _heightIndexFontVersion = -1;

    internal int HeightIndexFullScanCount { get; private set; }

    private sealed class LayoutCache
    {
        public TextLayout? Layout;
        public int FontVersion = -1;
        public int MutationVersion = -1;
    }

    private bool _isSelecting;
    private bool _hasSelection;
    private long _anchorLine;
    private int _anchorChar;
    private long _caretLine;
    private int _caretChar;

    public OutputPaneControl()
    {
        ClipToBounds = true;
        UpdateFontMetrics();
    }

    /// <summary>When true the pane keeps itself scrolled to the bottom as content arrives.</summary>
    public bool PinToBottom { get; set; } = true;

    public bool WordWrap
    {
        get => _wordWrap;
        set
        {
            if (_wordWrap == value)
            {
                return;
            }

            _wordWrap = value;
            _fontVersion++;
            _offset = new Vector(0, _offset.Y);
            NotifyContentChanged();
        }
    }

    public OutputBuffer? Buffer
    {
        get => _buffer;
        set
        {
            if (_buffer is not null)
            {
                _buffer.LinesTrimmed -= OnLinesTrimmed;
            }

            _buffer = value;

            if (_buffer is not null)
            {
                _buffer.LinesTrimmed += OnLinesTrimmed;
            }

            ClearSelection();
            NotifyContentChanged();
        }
    }

    public void SetFont(FontFamily fontFamily, double fontSize, FontWeight fontWeight)
    {
        _fontFamily = fontFamily;
        _fontSize = fontSize;
        _fontWeight = fontWeight;
        _fontVersion++;
        UpdateFontMetrics();
        NotifyContentChanged();
    }

    /// <summary>
    /// Called after new text has been appended to the shared buffer. Cheap: it only updates
    /// the scroll extent and schedules a repaint; the actual drawing happens once per frame.
    /// </summary>
    public void NotifyContentChanged()
    {
        if (PinToBottom)
        {
            _offset = new Vector(_offset.X, MaxOffsetY());
        }
        else
        {
            _offset = ClampOffset(_offset);
        }

        _scrollInvalidated?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
    }

    public void ClearSelection()
    {
        if (_hasSelection)
        {
            _hasSelection = false;
            InvalidateVisual();
        }
    }

    public bool SelectAndReveal(long globalLine, int startCharacter, int length)
    {
        var buffer = _buffer;
        if (buffer is null
            || globalLine < buffer.FirstGlobalIndex
            || globalLine >= buffer.FirstGlobalIndex + buffer.Count)
        {
            return false;
        }

        var lineIndex = checked((int)(globalLine - buffer.FirstGlobalIndex));
        var line = buffer[lineIndex];
        var start = Math.Clamp(startCharacter, 0, line.Length);
        var end = Math.Clamp(start + length, start, line.Length);

        _anchorLine = globalLine;
        _anchorChar = start;
        _caretLine = globalLine;
        _caretChar = end;
        _hasSelection = end > start;

        EnsureHeightIndex(buffer);
        var lineTop = PrefixHeight(buffer, lineIndex);
        var lineHeight = GetLayout(line).Height;
        SetIndexedHeight(globalLine, lineHeight);
        _offset = ClampOffset(new Vector(
            _offset.X,
            lineTop - Math.Max(0, (_viewport.Height - lineHeight) / 2)));

        _scrollInvalidated?.Invoke(this, EventArgs.Empty);
        InvalidateVisual();
        return _hasSelection;
    }

    public string? GetSelectedText()
    {
        var buffer = _buffer;
        if (buffer is null || !_hasSelection)
        {
            return null;
        }

        var (startLine, startChar, endLine, endChar) = NormalizedSelection();

        var firstAvailable = buffer.FirstGlobalIndex;
        var lastAvailable = buffer.FirstGlobalIndex + buffer.Count - 1;

        if (endLine < firstAvailable || startLine > lastAvailable)
        {
            return null;
        }

        if (startLine < firstAvailable)
        {
            startLine = firstAvailable;
            startChar = 0;
        }

        if (endLine > lastAvailable)
        {
            endLine = lastAvailable;
            endChar = int.MaxValue;
        }

        var builder = new System.Text.StringBuilder();
        for (var global = startLine; global <= endLine; global++)
        {
            var text = buffer[(int)(global - buffer.FirstGlobalIndex)].Text;
            var from = global == startLine ? Math.Min(startChar, text.Length) : 0;
            var to = global == endLine ? Math.Min(endChar, text.Length) : text.Length;

            if (global > startLine)
            {
                builder.Append(Environment.NewLine);
            }

            if (to > from)
            {
                builder.Append(text, from, to - from);
            }
        }

        var result = builder.ToString();
        return result.Length == 0 ? null : result;
    }

    /// <summary>
    /// Renders only the selected terminal cells to a bitmap suitable for the system clipboard.
    /// The same cached text layouts are used as on screen, preserving ANSI colours and styles.
    /// </summary>
    public RenderTargetBitmap? CreateSelectionBitmap()
    {
        var buffer = _buffer;
        if (buffer is null || !_hasSelection)
        {
            return null;
        }

        var (startLine, startChar, endLine, endChar) = NormalizedSelection();
        var firstAvailable = buffer.FirstGlobalIndex;
        var lastAvailable = firstAvailable + buffer.Count - 1;
        startLine = Math.Max(startLine, firstAvailable);
        endLine = Math.Min(endLine, lastAvailable);

        if (startLine > endLine)
        {
            return null;
        }

        const double padding = 8;
        var rows = new List<(TextLayout Layout, double SourceX, double Width)>();
        var maximumWidth = 0d;

        for (var global = startLine; global <= endLine; global++)
        {
            var line = buffer[(int)(global - firstAvailable)];
            var from = global == startLine ? Math.Min(startChar, line.Length) : 0;
            var to = global == endLine ? Math.Min(endChar, line.Length) : line.Length;
            var layout = BuildLayout(line, wordWrap: false);
            var range = GetSelectionBounds(layout, from, to);
            rows.Add((layout, range.X, range.Width));
            maximumWidth = Math.Max(maximumWidth, range.Width);
        }

        if (rows.Count == 0 || maximumWidth <= 0)
        {
            return null;
        }

        var pixelSize = new PixelSize(
            Math.Max(1, (int)Math.Ceiling(maximumWidth + padding * 2)),
            Math.Max(1, (int)Math.Ceiling(rows.Count * _lineHeight + padding * 2)));
        var bitmap = new RenderTargetBitmap(pixelSize);

        using (var context = bitmap.CreateDrawingContext())
        {
            context.FillRectangle(ImageBackgroundBrush, new Rect(pixelSize.ToSize(1)));

            for (var row = 0; row < rows.Count; row++)
            {
                var (layout, sourceX, width) = rows[row];
                var destinationY = padding + row * _lineHeight;
                using (context.PushClip(new Rect(padding, destinationY, width, _lineHeight)))
                {
                    layout.Draw(context, new Point(padding - sourceX, destinationY));
                }
            }
        }

        return bitmap;
    }

    private Rect GetSelectionBounds(TextLayout layout, int from, int to)
    {
        if (to <= from)
        {
            return new Rect(layout.WidthIncludingTrailingWhitespace, 0, _charWidth, _lineHeight);
        }

        var rectangles = layout.HitTestTextRange(from, to - from);
        if (rectangles.Count() == 0)
        {
            return new Rect(0, 0, _charWidth, _lineHeight);
        }

        var left = rectangles.Min(rectangle => rectangle.X);
        var right = rectangles.Max(rectangle => rectangle.Right);
        return new Rect(left, 0, Math.Max(_charWidth, right - left), _lineHeight);
    }

    // ------------------------------------------------------------------
    // ILogicalScrollable
    // ------------------------------------------------------------------

    public Size Extent
    {
        get
        {
            var buffer = _buffer;
            if (buffer is null)
            {
                return default;
            }

            return new Size(
                _wordWrap ? _viewport.Width : buffer.MaxLineLength * _charWidth + ExtentWidthPadding,
                GetContentHeight(buffer));
        }
    }

    public Vector Offset
    {
        get => _offset;
        set
        {
            var clamped = ClampOffset(value);
            if (clamped == _offset)
            {
                return;
            }

            _offset = clamped;
            InvalidateVisual();
        }
    }

    public Size Viewport => _viewport;

    bool IScrollable.CanHorizontallyScroll => _canHorizontallyScroll;

    bool IScrollable.CanVerticallyScroll => _canVerticallyScroll;

    bool ILogicalScrollable.CanHorizontallyScroll
    {
        get => _canHorizontallyScroll;
        set => _canHorizontallyScroll = value;
    }

    bool ILogicalScrollable.CanVerticallyScroll
    {
        get => _canVerticallyScroll;
        set => _canVerticallyScroll = value;
    }

    bool ILogicalScrollable.IsLogicalScrollEnabled => true;

    Size ILogicalScrollable.ScrollSize => new(
        _charWidth * 4,
        _lineHeight * MouseWheelScrollLines);

    Size ILogicalScrollable.PageScrollSize => _viewport;

    event EventHandler? ILogicalScrollable.ScrollInvalidated
    {
        add => _scrollInvalidated += value;
        remove => _scrollInvalidated -= value;
    }

    bool ILogicalScrollable.BringIntoView(Control target, Rect targetRect) => false;

    Control? ILogicalScrollable.GetControlInDirection(NavigationDirection direction, Control? from) =>
        null;

    void ILogicalScrollable.RaiseScrollInvalidated(EventArgs e) =>
        _scrollInvalidated?.Invoke(this, e);

    // ------------------------------------------------------------------
    // Layout & rendering
    // ------------------------------------------------------------------

    protected override Size MeasureOverride(Size availableSize) => new(
        double.IsFinite(availableSize.Width) ? availableSize.Width : 0,
        double.IsFinite(availableSize.Height) ? availableSize.Height : 0);

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_viewport != finalSize)
        {
            _viewport = finalSize;

            if (_wordWrap)
            {
                _fontVersion++;
            }

            if (PinToBottom)
            {
                _offset = new Vector(_offset.X, MaxOffsetY());
            }

            _scrollInvalidated?.Invoke(this, EventArgs.Empty);
        }

        return finalSize;
    }

    // Text rendering leaves most of the surface unpainted, which would make it invisible
    // to pointer hit-testing; treat the whole bounds as hit-testable instead.
    public bool HitTest(Point point) => new Rect(Bounds.Size).Contains(point);

    // Alpha of 0 would get culled from the compositor's hit-testing entirely, so the
    // hit-test surface is painted with the lowest non-zero alpha instead.
    private static readonly ImmutableSolidColorBrush HitTestSurface =
        new(Color.FromArgb(1, 0, 0, 0));

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(HitTestSurface, new Rect(Bounds.Size));

        var buffer = _buffer;
        if (buffer is null)
        {
            return;
        }

        EnsureHeightIndex(buffer);
        var count = buffer.Count;
        var firstVisible = FindFirstVisibleLine(buffer, _offset.Y);
        var contentY = PrefixHeight(buffer, firstVisible);

        var y = contentY - _offset.Y;
        var (selStartLine, selStartChar, selEndLine, selEndChar) = NormalizedSelection();

        for (var i = firstVisible; i < count && y < Bounds.Height; i++)
        {
            var line = buffer[i];
            var layout = GetLayout(line);
            SetIndexedHeight(buffer.FirstGlobalIndex + i, layout.Height);

            if (_hasSelection)
            {
                var global = buffer.FirstGlobalIndex + i;
                if (global >= selStartLine && global <= selEndLine)
                {
                    var from = global == selStartLine ? Math.Min(selStartChar, line.Length) : 0;
                    var to = global == selEndLine ? Math.Min(selEndChar, line.Length) : line.Length;

                    if (to > from)
                    {
                        foreach (var rect in layout.HitTestTextRange(from, to - from))
                        {
                            context.FillRectangle(
                                SelectionBrush,
                                new Rect(rect.X - _offset.X, y + rect.Y, rect.Width, rect.Height));
                        }
                    }

                    // Show that the line break itself is part of a multi-line selection.
                    if (global < selEndLine)
                    {
                        var newlineX = layout.WidthIncludingTrailingWhitespace - _offset.X;
                        context.FillRectangle(
                            SelectionBrush,
                            new Rect(newlineX, y, _charWidth, _lineHeight));
                    }
                }
            }

            layout.Draw(context, new Point(-_offset.X, y));
            y += layout.Height;
        }
    }

    private TextLayout GetLayout(OutputLine line)
    {
        var cache = _layoutCache.GetOrCreateValue(line);
        if (cache.Layout is { } cached &&
            cache.FontVersion == _fontVersion &&
            cache.MutationVersion == line.MutationVersion)
        {
            return cached;
        }

        var layout = BuildLayout(line, _wordWrap);
        cache.Layout = layout;
        cache.FontVersion = _fontVersion;
        cache.MutationVersion = line.MutationVersion;
        return layout;
    }

    private TextLayout BuildLayout(OutputLine line, bool wordWrap)
    {
        var segments = line.Segments;
        List<ValueSpan<TextRunProperties>>? overrides = null;
        var position = 0;

        foreach (var segment in segments)
        {
            if (segment.Style != default)
            {
                overrides ??= new List<ValueSpan<TextRunProperties>>(segments.Count);
                overrides.Add(new ValueSpan<TextRunProperties>(
                    position,
                    segment.Text.Length,
                    new GenericTextRunProperties(
                        segment.Style.Bold ? _boldTypeface : _typeface,
                        _fontSize,
                        textDecorations: segment.Style.Underline ? TextDecorations.Underline : null,
                        foregroundBrush: GetBrush(segment.Style.Foreground) ?? DefaultForeground,
                        backgroundBrush: GetBrush(segment.Style.Background))));
            }

            position += segment.Text.Length;
        }

        return new TextLayout(
            line.Text,
            _typeface,
            _fontSize,
            DefaultForeground,
            textWrapping: wordWrap ? TextWrapping.Wrap : TextWrapping.NoWrap,
            maxWidth: wordWrap ? Math.Max(1, _viewport.Width) : double.PositiveInfinity,
            textStyleOverrides: overrides?.ToArray());
    }

    private static ImmutableSolidColorBrush? GetBrush(Color? color)
    {
        if (color is not { } value)
        {
            return null;
        }

        var key = value.ToUInt32();
        if (!BrushCache.TryGetValue(key, out var brush))
        {
            brush = new ImmutableSolidColorBrush(value);
            BrushCache[key] = brush;
        }

        return brush;
    }

    private void UpdateFontMetrics()
    {
        _typeface = new Typeface(_fontFamily, weight: _fontWeight);
        _boldTypeface = new Typeface(_fontFamily, weight: FontWeight.Bold);

        var probe = new TextLayout("0", _typeface, _fontSize, DefaultForeground);
        _lineHeight = probe.Height;
        _charWidth = probe.WidthIncludingTrailingWhitespace;
    }

    // ------------------------------------------------------------------
    // Selection input
    // ------------------------------------------------------------------

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var (line, character) = HitTestPosition(e.GetPosition(this));
        _anchorLine = line;
        _anchorChar = character;
        _caretLine = line;
        _caretChar = character;
        _isSelecting = true;
        _hasSelection = false;
        e.Pointer.Capture(this);
        InvalidateVisual();

        // Deliberately not handled: the event must keep bubbling so MainWindow can
        // redirect keyboard focus to the command box. Pointer capture alone is enough
        // to keep receiving the moves that drive the selection.
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (!_isSelecting)
        {
            return;
        }

        var (line, character) = HitTestPosition(e.GetPosition(this));
        if (line != _caretLine || character != _caretChar)
        {
            _caretLine = line;
            _caretChar = character;
            _hasSelection = line != _anchorLine || character != _anchorChar;
            InvalidateVisual();
        }

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_isSelecting)
        {
            _isSelecting = false;
            e.Pointer.Capture(null);
        }
    }

    private (long Line, int Character) HitTestPosition(Point point)
    {
        var buffer = _buffer;
        if (buffer is null)
        {
            return (0, 0);
        }

        var globalY = point.Y + _offset.Y;
        EnsureHeightIndex(buffer);
        var index = Math.Min(FindFirstVisibleLine(buffer, globalY), buffer.Count - 1);
        var lineTop = PrefixHeight(buffer, index);
        var line = buffer[index];
        var layout = GetLayout(line);
        SetIndexedHeight(buffer.FirstGlobalIndex + index, layout.Height);

        var hit = layout.HitTestPoint(new Point(point.X + _offset.X, globalY - lineTop));
        var character = Math.Clamp(
            hit.TextPosition + (hit.IsTrailing ? 1 : 0),
            0,
            line.Length);

        return (buffer.FirstGlobalIndex + index, character);
    }

    private (long StartLine, int StartChar, long EndLine, int EndChar) NormalizedSelection()
    {
        if (_anchorLine < _caretLine ||
            (_anchorLine == _caretLine && _anchorChar <= _caretChar))
        {
            return (_anchorLine, _anchorChar, _caretLine, _caretChar);
        }

        return (_caretLine, _caretChar, _anchorLine, _anchorChar);
    }

    // ------------------------------------------------------------------
    // Buffer callbacks & helpers
    // ------------------------------------------------------------------

    private void OnLinesTrimmed(int count)
    {
        if (!PinToBottom)
        {
            // Keep the visible content stable while lines vanish above it.
            _offset = new Vector(_offset.X, Math.Max(0, _offset.Y - count * _lineHeight));
        }
    }

    private Vector ClampOffset(Vector value)
    {
        var extent = Extent;
        var maxX = Math.Max(0, extent.Width - _viewport.Width);
        var maxY = Math.Max(0, extent.Height - _viewport.Height);
        return new Vector(Math.Clamp(value.X, 0, maxX), Math.Clamp(value.Y, 0, maxY));
    }

    private double MaxOffsetY() => Math.Max(0, Extent.Height - _viewport.Height);

    private double GetContentHeight(OutputBuffer buffer)
    {
        EnsureHeightIndex(buffer);
        return PrefixHeight(buffer, buffer.Count);
    }

    private void EnsureHeightIndex(OutputBuffer buffer)
    {
        var last = buffer.FirstGlobalIndex + buffer.Count - 1;
        if (!ReferenceEquals(_heightBuffer, buffer)
            || _indexedHeights?.Length != buffer.Capacity
            || _heightIndexFontVersion != _fontVersion
            || buffer.FirstGlobalIndex < _indexedFirst
            || last < _indexedLast)
        {
            RebuildHeightIndex(buffer);
            return;
        }

        for (var global = _indexedFirst; global < buffer.FirstGlobalIndex; global++)
        {
            ClearIndexedHeight(global);
        }

        var updateFrom = Math.Max(buffer.FirstGlobalIndex, Math.Min(_indexedLast, last));
        for (var global = updateFrom; global <= last; global++)
        {
            var line = buffer[(int)(global - buffer.FirstGlobalIndex)];
            SetIndexedHeight(global, EstimateLineHeight(line));
        }

        _indexedFirst = buffer.FirstGlobalIndex;
        _indexedLast = last;
    }

    private void RebuildHeightIndex(OutputBuffer buffer)
    {
        _heightBuffer = buffer;
        _indexedHeights = new double[buffer.Capacity];
        _indexedGlobals = new long[buffer.Capacity];
        Array.Fill(_indexedGlobals, long.MinValue);
        _heightTree = new HeightFenwickTree(buffer.Capacity);
        _indexedFirst = buffer.FirstGlobalIndex;
        _indexedLast = buffer.FirstGlobalIndex + buffer.Count - 1;
        _heightIndexFontVersion = _fontVersion;
        HeightIndexFullScanCount++;

        for (var i = 0; i < buffer.Count; i++)
        {
            SetIndexedHeight(buffer.FirstGlobalIndex + i, EstimateLineHeight(buffer[i]));
        }
    }

    private void SetIndexedHeight(long global, double height)
    {
        if (_indexedHeights is null || _indexedGlobals is null || _heightTree is null)
        {
            return;
        }

        var slot = (int)(global % _indexedHeights.Length);
        if (_indexedGlobals[slot] != global)
        {
            if (_indexedGlobals[slot] != long.MinValue)
            {
                _heightTree.Add(slot, -_indexedHeights[slot]);
            }

            _indexedGlobals[slot] = global;
            _indexedHeights[slot] = 0;
        }

        var delta = height - _indexedHeights[slot];
        if (Math.Abs(delta) <= double.Epsilon)
        {
            return;
        }

        _indexedHeights[slot] = height;
        _heightTree.Add(slot, delta);
    }

    private void ClearIndexedHeight(long global)
    {
        if (_indexedHeights is null || _indexedGlobals is null || _heightTree is null)
        {
            return;
        }

        var slot = (int)(global % _indexedHeights.Length);
        if (_indexedGlobals[slot] != global)
        {
            return;
        }

        _heightTree.Add(slot, -_indexedHeights[slot]);
        _indexedHeights[slot] = 0;
        _indexedGlobals[slot] = long.MinValue;
    }

    private double PrefixHeight(OutputBuffer buffer, int count)
    {
        if (count <= 0 || _heightTree is null)
        {
            return 0;
        }

        count = Math.Min(count, buffer.Count);
        var start = (int)(buffer.FirstGlobalIndex % buffer.Capacity);
        var firstSegment = Math.Min(count, buffer.Capacity - start);
        var height = _heightTree.RangeSum(start, start + firstSegment);
        if (firstSegment < count)
        {
            height += _heightTree.RangeSum(0, count - firstSegment);
        }

        return height;
    }

    private int FindFirstVisibleLine(OutputBuffer buffer, double offset)
    {
        var low = 0;
        var high = buffer.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (PrefixHeight(buffer, middle + 1) <= offset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    /// <summary>
    /// Height of a wrapped line without forcing a full <see cref="TextLayout"/> build.
    /// Uses the real layout height when one is already cached and current; otherwise
    /// approximates from character count and viewport width. This keeps
    /// <see cref="GetContentHeight"/> at O(lines) simple arithmetic even when every cached
    /// layout has just been invalidated (e.g. a font or size change), instead of re-laying
    /// out the whole scrollback synchronously on the UI thread. Off-screen estimates are
    /// replaced by exact heights as lines scroll into view and get laid out for real.
    /// </summary>
    private double EstimateLineHeight(OutputLine line)
    {
        if (_layoutCache.TryGetValue(line, out var cache)
            && cache.Layout is { } cached
            && cache.FontVersion == _fontVersion
            && cache.MutationVersion == line.MutationVersion)
        {
            return cached.Height;
        }

        if (_viewport.Width <= 0 || _charWidth <= 0)
        {
            return _lineHeight;
        }

        var charsPerRow = Math.Max(1, (int)(_viewport.Width / _charWidth));
        var rows = Math.Max(1, (int)Math.Ceiling((double)line.Length / charsPerRow));
        return rows * _lineHeight;
    }

    private sealed class HeightFenwickTree(int size)
    {
        private readonly double[] _tree = new double[size + 1];

        public void Add(int index, double delta)
        {
            for (var i = index + 1; i < _tree.Length; i += i & -i)
            {
                _tree[i] += delta;
            }
        }

        public double RangeSum(int start, int end) => PrefixSum(end) - PrefixSum(start);

        private double PrefixSum(int end)
        {
            var sum = 0d;
            for (var i = end; i > 0; i -= i & -i)
            {
                sum += _tree[i];
            }

            return sum;
        }
    }
}
