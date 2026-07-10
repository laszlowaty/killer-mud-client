using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace MudClient.App.Controls;

public partial class MudOutputView : UserControl
{
    public static readonly StyledProperty<FontFamily> OutputFontFamilyProperty =
        AvaloniaProperty.Register<MudOutputView, FontFamily>(
            nameof(OutputFontFamily), new FontFamily("Consolas"));

    public static readonly StyledProperty<double> OutputFontSizeProperty =
        AvaloniaProperty.Register<MudOutputView, double>(
            nameof(OutputFontSize), 14);

    public FontFamily OutputFontFamily
    {
        get => GetValue(OutputFontFamilyProperty);
        set => SetValue(OutputFontFamilyProperty, value);
    }

    public double OutputFontSize
    {
        get => GetValue(OutputFontSizeProperty);
        set => SetValue(OutputFontSizeProperty, value);
    }

    private const int MaximumLines = 5_000;
    private const int LiveTailMaxLines = 100;

    private readonly AnsiStreamParser _parser = new();
    private readonly ScrollViewer _scrollbackScroller;
    private readonly StackPanel _scrollbackPanel;
    private readonly ScrollViewer _liveTailScroller;
    private readonly StackPanel _liveTailPanel;
    private readonly GridSplitter _splitter;
    private readonly Grid _grid;
    private readonly List<string> _completedLineTexts = [];
    private readonly StringBuilder _currentLineText = new();
    private readonly List<int> _lineInlineCounts = [];
    private SelectableTextBlock _scrollbackBlock = null!;
    private SelectableTextBlock _liveTailBlock = null!;
    private int _currentLineInlineCount;
    private int _scrollbackLineOffset;
    private int _liveTailLineOffset;
    private bool _isSplitMode;

    public MudOutputView()
    {
        InitializeComponent();
        _scrollbackScroller = this.FindControl<ScrollViewer>("ScrollbackScroller")
            ?? throw new InvalidOperationException("ScrollbackScroller not found.");
        _scrollbackPanel = this.FindControl<StackPanel>("ScrollbackPanel")
            ?? throw new InvalidOperationException("ScrollbackPanel not found.");
        _liveTailScroller = this.FindControl<ScrollViewer>("LiveTailScroller")
            ?? throw new InvalidOperationException("LiveTailScroller not found.");
        _liveTailPanel = this.FindControl<StackPanel>("LiveTailPanel")
            ?? throw new InvalidOperationException("LiveTailPanel not found.");
        _splitter = this.FindControl<GridSplitter>("OutputSplitter")
            ?? throw new InvalidOperationException("OutputSplitter not found.");
        _grid = this.FindControl<Grid>("OutputGrid")
            ?? throw new InvalidOperationException("OutputGrid not found.");

        _scrollbackBlock = CreatePaneBlock();
        _scrollbackPanel.Children.Add(_scrollbackBlock);

        _liveTailBlock = CreatePaneBlock();
        _liveTailPanel.Children.Add(_liveTailBlock);

        // Start in single-pane mode.
        _isSplitMode = false;
        _splitter.IsVisible = false;
        _liveTailScroller.IsVisible = false;

        _scrollbackScroller.ScrollChanged += OnScrollbackScrollChanged;
    }

    public void AppendText(string text)
    {
        foreach (var token in _parser.Feed(text))
        {
            switch (token)
            {
                case AnsiTextToken textToken:
                    AppendRun(textToken);
                    break;

                case AnsiNewLineToken:
                    StartNewLine();
                    break;

                case AnsiCarriageReturnToken:
                    break;
            }
        }

        Dispatcher.UIThread.Post(
            () =>
            {
                if (!_isSplitMode)
                    _scrollbackScroller.Offset = new Vector(_scrollbackScroller.Offset.X, double.MaxValue);
            },
            DispatcherPriority.Background);

        Dispatcher.UIThread.Post(
            () => _liveTailScroller.Offset = new Vector(_liveTailScroller.Offset.X, double.MaxValue),
            DispatcherPriority.Background);
    }

    public void Clear()
    {
        SetSplitMode(false);

        _scrollbackPanel.Children.Clear();
        _liveTailPanel.Children.Clear();
        _completedLineTexts.Clear();
        _currentLineText.Clear();
        _lineInlineCounts.Clear();
        _currentLineInlineCount = 0;
        _scrollbackLineOffset = 0;
        _liveTailLineOffset = 0;

        _scrollbackBlock = CreatePaneBlock();
        _scrollbackPanel.Children.Add(_scrollbackBlock);

        _liveTailBlock = CreatePaneBlock();
        _liveTailPanel.Children.Add(_liveTailBlock);
    }

    private void AppendRun(AnsiTextToken token)
    {
        var scrollbackRun = CreateRun(token);
        var liveTailRun = CreateRun(token);

        _scrollbackBlock.Inlines?.Add(scrollbackRun);
        _liveTailBlock.Inlines?.Add(liveTailRun);
        _currentLineInlineCount++;
        _currentLineText.Append(token.Text);
    }

    private static Run CreateRun(AnsiTextToken token)
    {
        var run = new Run
        {
            Text = token.Text,
            FontWeight = token.Style.Bold ? FontWeight.Bold : FontWeight.Normal,
        };

        if (token.Style.Foreground is { } foreground)
        {
            run.Foreground = new SolidColorBrush(foreground);
        }

        if (token.Style.Background is { } background)
        {
            run.Background = new SolidColorBrush(background);
        }

        if (token.Style.Underline)
        {
            run.TextDecorations = TextDecorations.Underline;
        }

        return run;
    }

    private void StartNewLine()
    {
        _completedLineTexts.Add(_currentLineText.ToString());
        _currentLineText.Clear();

        if (_currentLineInlineCount == 0)
        {
            var placeholder = new Run { Text = "\u200B" };
            _scrollbackBlock.Inlines?.Add(placeholder);
            _liveTailBlock.Inlines?.Add(placeholder);
            _currentLineInlineCount++;
        }

        _scrollbackBlock.Inlines?.Add(new LineBreak());
        _liveTailBlock.Inlines?.Add(new LineBreak());
        _currentLineInlineCount++;

        _lineInlineCounts.Add(_currentLineInlineCount);
        _currentLineInlineCount = 0;

        TrimBlock(_scrollbackBlock, ref _scrollbackLineOffset, MaximumLines);
        TrimBlock(_liveTailBlock, ref _liveTailLineOffset, LiveTailMaxLines);

        TrimCompletedLineTexts();
    }

    private void TrimBlock(SelectableTextBlock block, ref int lineOffset, int maxLines)
    {
        int lineCount = _lineInlineCounts.Count - lineOffset;
        while (lineCount > maxLines)
        {
            int count = _lineInlineCounts[lineOffset];
            var inlines = block.Inlines;
            for (int i = 0; i < count && inlines?.Count > 0; i++)
            {
                inlines.RemoveAt(0);
            }
            lineOffset++;
            lineCount--;
        }
    }

    private void TrimCompletedLineTexts()
    {
        int trimmed = Math.Min(_scrollbackLineOffset, _liveTailLineOffset);
        if (trimmed > 0)
        {
            _completedLineTexts.RemoveRange(0, trimmed);
            _lineInlineCounts.RemoveRange(0, trimmed);
            _scrollbackLineOffset -= trimmed;
            _liveTailLineOffset -= trimmed;
        }
    }

    private SelectableTextBlock CreatePaneBlock() => new()
    {
        FontFamily = OutputFontFamily,
        FontSize = OutputFontSize,
        Foreground = new SolidColorBrush(Color.FromRgb(215, 221, 230)),
        TextWrapping = TextWrapping.NoWrap,
    };

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if ((change.Property == OutputFontFamilyProperty || change.Property == OutputFontSizeProperty)
            && _scrollbackPanel is not null)
        {
            ApplyFontToExistingLines();
        }
    }

    private void ApplyFontToExistingLines()
    {
        foreach (var block in new[] { _scrollbackBlock, _liveTailBlock })
        {
            block.FontFamily = OutputFontFamily;
            block.FontSize = OutputFontSize;
        }
    }

    private async void CopySelection_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        var text = _scrollbackBlock.SelectedText;
        if (string.IsNullOrEmpty(text))
            text = _liveTailBlock.SelectedText;

        await CopyToClipboardAsync(text);
    }

    private void ClearOutput_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        Clear();
    }

    private async void CopyAll_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        var allLines = new List<string>(_completedLineTexts) { _currentLineText.ToString() };
        await CopyToClipboardAsync(string.Join(Environment.NewLine, allLines));
    }

    private async Task CopyToClipboardAsync(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetValueAsync(DataFormat.Text, text);
        }
    }

    private void OnScrollbackScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        const double bottomTolerance = 10.0;
        const double splitActivationThreshold = 30.0;

        double distanceFromBottom = _scrollbackScroller.Extent.Height
            - _scrollbackScroller.Viewport.Height
            - _scrollbackScroller.Offset.Y;

        if (distanceFromBottom <= bottomTolerance && _isSplitMode)
        {
            SetSplitMode(false);
            return;
        }

        if (distanceFromBottom > splitActivationThreshold
            && !_isSplitMode
            && e.OffsetDelta.Y < 0)
        {
            SetSplitMode(true);
        }
    }

    private void SetSplitMode(bool enabled)
    {
        if (_isSplitMode == enabled)
            return;

        _isSplitMode = enabled;

        if (enabled)
        {
            _grid.RowDefinitions[0].Height = new GridLength(2, GridUnitType.Star);
            _grid.RowDefinitions[2].Height = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            _grid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            _grid.RowDefinitions[2].Height = new GridLength(0);
        }

        _splitter.IsVisible = enabled;
        _liveTailScroller.IsVisible = enabled;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
