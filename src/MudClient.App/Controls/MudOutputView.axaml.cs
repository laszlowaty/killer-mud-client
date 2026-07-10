using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace MudClient.App.Controls;

public partial class MudOutputView : UserControl
{
    private const int MaximumLines = 5_000;

    private readonly AnsiStreamParser _parser = new();
    private readonly ScrollViewer _scroller;
    private readonly StackPanel _linesPanel;
    private readonly List<string> _completedLineTexts = [];
    private readonly StringBuilder _currentLineText = new();
    private SelectableTextBlock _currentLine;

    public MudOutputView()
    {
        InitializeComponent();
        _scroller = this.FindControl<ScrollViewer>("OutputScroller")
            ?? throw new InvalidOperationException("OutputScroller not found.");
        _linesPanel = this.FindControl<StackPanel>("LinesPanel")
            ?? throw new InvalidOperationException("LinesPanel not found.");

        _currentLine = CreateLine();
        _linesPanel.Children.Add(_currentLine);
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

        Dispatcher.UIThread.Post(
            () => _scroller.Offset = new Vector(_scroller.Offset.X, double.MaxValue),
            DispatcherPriority.Background);
    }

    public void Clear()
    {
        _linesPanel.Children.Clear();
        _completedLineTexts.Clear();
        _currentLineText.Clear();
        _currentLine = CreateLine();
        _linesPanel.Children.Add(_currentLine);
    }

    private void AppendRun(AnsiTextToken token)
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

        _currentLine.Inlines?.Add(run);
        _currentLineText.Append(token.Text);
    }

    private void StartNewLine()
    {
        _completedLineTexts.Add(_currentLineText.ToString());
        _currentLineText.Clear();

        _currentLine = CreateLine();
        _linesPanel.Children.Add(_currentLine);

        while (_linesPanel.Children.Count > MaximumLines)
        {
            _linesPanel.Children.RemoveAt(0);
        }

        while (_completedLineTexts.Count > MaximumLines)
        {
            _completedLineTexts.RemoveAt(0);
        }
    }

    private static SelectableTextBlock CreateLine() => new()
    {
        FontFamily = new FontFamily("monospace"),
        FontSize = 14,
        Foreground = new SolidColorBrush(Color.FromRgb(215, 221, 230)),
        TextWrapping = TextWrapping.NoWrap,
        MinHeight = 18,
    };

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
        for (var i = _linesPanel.Children.Count - 1; i >= 0; i--)
        {
            if (_linesPanel.Children[i] is SelectableTextBlock { SelectedText.Length: > 0 } line)
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

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
