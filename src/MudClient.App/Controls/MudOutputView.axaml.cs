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
    private SelectableTextBlock _currentLine;
    private SelectableTextBlock _liveTailCurrentLine;
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

        _currentLine = CreateLine();
        _scrollbackPanel.Children.Add(_currentLine);

        _liveTailCurrentLine = CreateLine();
        _liveTailPanel.Children.Add(_liveTailCurrentLine);

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
                    // CR in MUD streams normally belongs to CRLF. Cursor-return semantics are
                    // deliberately ignored by this append-only line renderer.
                    break;
            }
        }

        // Auto-scroll behavior:
        // When split mode is off, auto-scroll the scrollback pane to the newest output.
        // The live-tail pane is always auto-scrolled (harmless when hidden).
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
        // Deterministically return to single-pane layout so the next append starts
        // from a clean default state.
        SetSplitMode(false);

        _scrollbackPanel.Children.Clear();
        _liveTailPanel.Children.Clear();
        _completedLineTexts.Clear();
        _currentLineText.Clear();
        _currentLine = CreateLine();
        _scrollbackPanel.Children.Add(_currentLine);
        _liveTailCurrentLine = CreateLine();
        _liveTailPanel.Children.Add(_liveTailCurrentLine);
    }

    private void AppendRun(AnsiTextToken token)
    {
        var scrollbackRun = CreateRun(token);
        var liveTailRun = CreateRun(token);

        _currentLine.Inlines?.Add(scrollbackRun);
        _liveTailCurrentLine.Inlines?.Add(liveTailRun);
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

        // The current live-tail line is already in the panel with all inlines
        // mirrored by AppendRun. Start a fresh live-tail line for the next row.
        _liveTailCurrentLine = CreateLine();
        _liveTailPanel.Children.Add(_liveTailCurrentLine);

        while (_liveTailPanel.Children.Count > LiveTailMaxLines)
        {
            _liveTailPanel.Children.RemoveAt(0);
        }

        _currentLine = CreateLine();
        _scrollbackPanel.Children.Add(_currentLine);

        while (_scrollbackPanel.Children.Count > MaximumLines)
        {
            _scrollbackPanel.Children.RemoveAt(0);
        }

        while (_completedLineTexts.Count > MaximumLines)
        {
            _completedLineTexts.RemoveAt(0);
        }
    }

    private SelectableTextBlock CreateLine() => new()
    {
        FontFamily = OutputFontFamily,
        FontSize = OutputFontSize,
        Foreground = new SolidColorBrush(Color.FromRgb(215, 221, 230)),
        TextWrapping = TextWrapping.NoWrap,
        MinHeight = OutputFontSize + 4,
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

    /// <summary>Re-styles lines already on screen so a settings change takes effect immediately.</summary>
    private void ApplyFontToExistingLines()
    {
        foreach (var panel in new[] { _scrollbackPanel, _liveTailPanel })
        {
            foreach (var child in panel.Children)
            {
                if (child is SelectableTextBlock line)
                {
                    line.FontFamily = OutputFontFamily;
                    line.FontSize = OutputFontSize;
                    line.MinHeight = OutputFontSize + 4;
                }
            }
        }
    }

    private async void CopySelection_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        var selected = _currentLine.SelectedText;
        var text = string.IsNullOrEmpty(selected)
            ? FindAnySelectedText()
            : selected;

        await CopyToClipboardAsync(text);
    }

    private string? FindAnySelectedText()
    {
        // Search the scrollback panel first (most likely to have user selections).
        for (var i = _scrollbackPanel.Children.Count - 1; i >= 0; i--)
        {
            if (_scrollbackPanel.Children[i] is SelectableTextBlock { SelectedText.Length: > 0 } line)
            {
                return line.SelectedText;
            }
        }

        // Fall back to the live tail panel.
        for (var i = _liveTailPanel.Children.Count - 1; i >= 0; i--)
        {
            if (_liveTailPanel.Children[i] is SelectableTextBlock { SelectedText.Length: > 0 } line)
            {
                return line.SelectedText;
            }
        }

        return null;
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
        // Require a larger offset for enabling split to avoid flicker from layout recalculations.
        const double splitActivationThreshold = 30.0;

        double distanceFromBottom = _scrollbackScroller.Extent.Height
            - _scrollbackScroller.Viewport.Height
            - _scrollbackScroller.Offset.Y;

        // Disable split when the user scrolls all the way to the bottom.
        if (distanceFromBottom <= bottomTolerance && _isSplitMode)
        {
            SetSplitMode(false);
            return;
        }

        // Enable split only when the user actively scrolls upward (negative OffsetDelta.Y).
        // Extent growth from AppendText() also increases distanceFromBottom, but the offset
        // hasn't changed — we must ignore those events so that split mode is only triggered
        // by deliberate user scroll-up, not by content-height increases.
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
