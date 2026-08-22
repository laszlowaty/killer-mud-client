using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.TextFormatting;
using Avalonia.Utilities;

namespace MudClient.App.Controls;

public sealed class JavaScriptTextBox : TextBox
{
    public static readonly StyledProperty<bool> IsSyntaxHighlightingEnabledProperty =
        AvaloniaProperty.Register<JavaScriptTextBox, bool>(nameof(IsSyntaxHighlightingEnabled));

    public bool IsSyntaxHighlightingEnabled
    {
        get => GetValue(IsSyntaxHighlightingEnabledProperty);
        set => SetValue(IsSyntaxHighlightingEnabledProperty, value);
    }
}

public sealed class JavaScriptTextPresenter : TextPresenter
{
    private static readonly IBrush KeywordBrush = new ImmutableSolidColorBrush(Color.Parse("#0000CC"));
    private static readonly IBrush StringBrush = new ImmutableSolidColorBrush(Color.Parse("#A31515"));
    private static readonly IBrush NumberBrush = new ImmutableSolidColorBrush(Color.Parse("#098658"));
    private static readonly IBrush CommentBrush = new ImmutableSolidColorBrush(Color.Parse("#008000"));
    private Size _layoutConstraint = Size.Infinity;

    public static readonly StyledProperty<bool> IsSyntaxHighlightingEnabledProperty =
        AvaloniaProperty.Register<JavaScriptTextPresenter, bool>(nameof(IsSyntaxHighlightingEnabled));

    public bool IsSyntaxHighlightingEnabled
    {
        get => GetValue(IsSyntaxHighlightingEnabledProperty);
        set => SetValue(IsSyntaxHighlightingEnabledProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsSyntaxHighlightingEnabledProperty)
        {
            InvalidateTextLayout();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _layoutConstraint = availableSize;
        return base.MeasureOverride(availableSize);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _layoutConstraint = new Size(Math.Ceiling(finalSize.Width), double.PositiveInfinity);
        return base.ArrangeOverride(finalSize);
    }

    protected override TextLayout CreateTextLayout()
    {
        if (!IsSyntaxHighlightingEnabled || PasswordChar != default && !RevealPassword)
        {
            return base.CreateTextLayout();
        }

        var text = CombineTextWithPreedit(Text, CaretIndex, PreeditText);
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var overrides = new List<ValueSpan<TextRunProperties>>();

        foreach (var span in JavaScriptSyntaxHighlighter.GetSpans(text))
        {
            overrides.Add(new ValueSpan<TextRunProperties>(
                span.Start,
                span.Length,
                new GenericTextRunProperties(
                    typeface,
                    FontSize,
                    foregroundBrush: GetBrush(span.Kind),
                    fontFeatures: FontFeatures)));
        }

        if (!string.IsNullOrEmpty(PreeditText))
        {
            overrides.Add(new ValueSpan<TextRunProperties>(
                CaretIndex,
                PreeditText.Length,
                new GenericTextRunProperties(
                    typeface,
                    FontSize,
                    TextDecorations.Underline,
                    Foreground,
                    fontFeatures: FontFeatures)));
        }
        else if (ShowSelectionHighlight && SelectionForegroundBrush is not null)
        {
            var start = Math.Min(SelectionStart, SelectionEnd);
            var length = Math.Abs(SelectionEnd - SelectionStart);
            if (length > 0)
            {
                overrides.Add(new ValueSpan<TextRunProperties>(
                    start,
                    length,
                    new GenericTextRunProperties(
                        typeface,
                        FontSize,
                        foregroundBrush: SelectionForegroundBrush,
                        fontFeatures: FontFeatures)));
            }
        }

        return new TextLayout(
            text,
            typeface,
            FontSize,
            Foreground,
            textAlignment: TextAlignment,
            textWrapping: TextWrapping,
            textTrimming: TextTrimming.None,
            textDecorations: null,
            flowDirection: FlowDirection,
            maxWidth: _layoutConstraint.Width,
            maxHeight: _layoutConstraint.Height,
            lineHeight: LineHeight,
            letterSpacing: LetterSpacing,
            maxLines: 0,
            fontFeatures: FontFeatures,
            textStyleOverrides: overrides);
    }

    private static IBrush GetBrush(JavaScriptSyntaxKind kind) => kind switch
    {
        JavaScriptSyntaxKind.Keyword => KeywordBrush,
        JavaScriptSyntaxKind.String => StringBrush,
        JavaScriptSyntaxKind.Number => NumberBrush,
        JavaScriptSyntaxKind.Comment => CommentBrush,
        _ => Brushes.Black,
    };

    private static string CombineTextWithPreedit(string? text, int caretIndex, string? preeditText)
    {
        text ??= string.Empty;
        if (string.IsNullOrEmpty(preeditText))
        {
            return text;
        }

        var insertionIndex = Math.Clamp(caretIndex, 0, text.Length);
        return text.Insert(insertionIndex, preeditText);
    }
}

public sealed class JavaScriptLineNumberMargin : Control
{
    private const double HorizontalPadding = 6;
    private static readonly IBrush NumberBrush = new ImmutableSolidColorBrush(Color.Parse("#6B7280"));
    private static readonly IBrush GutterBackgroundBrush = new ImmutableSolidColorBrush(Color.Parse("#F5F5F5"));
    private static readonly IBrush SeparatorBrush = new ImmutableSolidColorBrush(Color.Parse("#D1D5DB"));

    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<JavaScriptLineNumberMargin, string?>(nameof(Text));

    public static readonly StyledProperty<JavaScriptTextPresenter?> PresenterProperty =
        AvaloniaProperty.Register<JavaScriptLineNumberMargin, JavaScriptTextPresenter?>(nameof(Presenter));

    public static readonly StyledProperty<Vector> ScrollOffsetProperty =
        AvaloniaProperty.Register<JavaScriptLineNumberMargin, Vector>(nameof(ScrollOffset));

    static JavaScriptLineNumberMargin()
    {
        AffectsMeasure<JavaScriptLineNumberMargin>(TextProperty, PresenterProperty);
        AffectsRender<JavaScriptLineNumberMargin>(TextProperty, PresenterProperty, ScrollOffsetProperty);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public JavaScriptTextPresenter? Presenter
    {
        get => GetValue(PresenterProperty);
        set => SetValue(PresenterProperty, value);
    }

    public Vector ScrollOffset
    {
        get => GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    internal int LineCount => CountLines(Text);

    protected override Size MeasureOverride(Size availableSize)
    {
        using var measurement = CreateNumberLayout(new string('9', LineCount.ToString().Length));
        return new Size(Math.Ceiling(measurement.Width) + HorizontalPadding * 2, 0);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        context.FillRectangle(GutterBackgroundBrush, Bounds.WithX(0).WithY(0));
        context.DrawLine(
            new Pen(SeparatorBrush),
            new Point(Bounds.Width - 0.5, 0),
            new Point(Bounds.Width - 0.5, Bounds.Height));

        if (Presenter is null)
        {
            return;
        }

        var lineStarts = GetLogicalLineStarts(Text);
        var lineNumberByStart = new Dictionary<int, int>(lineStarts.Count);
        for (var index = 0; index < lineStarts.Count; index++)
        {
            lineNumberByStart[lineStarts[index]] = index + 1;
        }

        var y = -ScrollOffset.Y;
        foreach (var textLine in Presenter.TextLayout.TextLines)
        {
            if (lineNumberByStart.TryGetValue(textLine.FirstTextSourceIndex, out var lineNumber))
            {
                using var numberLayout = CreateNumberLayout(lineNumber.ToString());
                var x = Bounds.Width - HorizontalPadding - numberLayout.Width;
                numberLayout.Draw(context, new Point(x, y));
            }

            y += textLine.Height;
        }
    }

    internal static IReadOnlyList<int> GetLogicalLineStarts(string? text)
    {
        var starts = new List<int> { 0 };
        if (string.IsNullOrEmpty(text))
        {
            return starts;
        }

        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n' || text[index] == '\r' &&
                (index + 1 >= text.Length || text[index + 1] != '\n'))
            {
                starts.Add(index + 1);
            }
        }

        return starts;
    }

    private static int CountLines(string? text) => GetLogicalLineStarts(text).Count;

    private TextLayout CreateNumberLayout(string text)
    {
        var presenter = Presenter;
        var typeface = presenter is null
            ? new Typeface("Arial")
            : new Typeface(
                presenter.FontFamily,
                presenter.FontStyle,
                presenter.FontWeight,
                presenter.FontStretch);

        return new TextLayout(text, typeface, presenter?.FontSize ?? 12, NumberBrush);
    }
}

internal enum JavaScriptSyntaxKind
{
    Keyword,
    String,
    Number,
    Comment,
}

internal readonly record struct JavaScriptSyntaxSpan(int Start, int Length, JavaScriptSyntaxKind Kind);

internal static class JavaScriptSyntaxHighlighter
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "async", "await", "break", "case", "catch", "class", "const", "continue", "debugger",
        "default", "delete", "do", "else", "export", "extends", "false", "finally", "for",
        "function", "if", "import", "in", "instanceof", "let", "new", "null", "of", "return",
        "static", "super", "switch", "this", "throw", "true", "try", "typeof", "undefined",
        "var", "void", "while", "with", "yield",
    };

    public static IReadOnlyList<JavaScriptSyntaxSpan> GetSpans(string? text)
    {
        var spans = new List<JavaScriptSyntaxSpan>();
        if (string.IsNullOrEmpty(text))
        {
            return spans;
        }

        for (var index = 0; index < text.Length;)
        {
            if (text[index] == '/' && index + 1 < text.Length && text[index + 1] == '/')
            {
                var start = index;
                index += 2;
                while (index < text.Length && text[index] is not '\r' and not '\n')
                {
                    index++;
                }

                spans.Add(new JavaScriptSyntaxSpan(start, index - start, JavaScriptSyntaxKind.Comment));
                continue;
            }

            if (text[index] == '/' && index + 1 < text.Length && text[index + 1] == '*')
            {
                var start = index;
                index += 2;
                while (index + 1 < text.Length && (text[index] != '*' || text[index + 1] != '/'))
                {
                    index++;
                }

                index = index + 1 < text.Length ? index + 2 : text.Length;
                spans.Add(new JavaScriptSyntaxSpan(start, index - start, JavaScriptSyntaxKind.Comment));
                continue;
            }

            if (text[index] is '\'' or '"' or '`')
            {
                var start = index;
                var delimiter = text[index++];
                while (index < text.Length)
                {
                    if (text[index] == '\\')
                    {
                        index = Math.Min(text.Length, index + 2);
                        continue;
                    }

                    if (text[index++] == delimiter)
                    {
                        break;
                    }
                }

                spans.Add(new JavaScriptSyntaxSpan(start, index - start, JavaScriptSyntaxKind.String));
                continue;
            }

            if (char.IsDigit(text[index]) ||
                text[index] == '.' && index + 1 < text.Length && char.IsDigit(text[index + 1]))
            {
                var start = index++;
                while (index < text.Length &&
                       (char.IsLetterOrDigit(text[index]) || text[index] is '.' or '_'))
                {
                    index++;
                }

                spans.Add(new JavaScriptSyntaxSpan(start, index - start, JavaScriptSyntaxKind.Number));
                continue;
            }

            if (IsIdentifierStart(text[index]))
            {
                var start = index++;
                while (index < text.Length && IsIdentifierPart(text[index]))
                {
                    index++;
                }

                if (Keywords.Contains(text[start..index]))
                {
                    spans.Add(new JavaScriptSyntaxSpan(start, index - start, JavaScriptSyntaxKind.Keyword));
                }

                continue;
            }

            index++;
        }

        return spans;
    }

    private static bool IsIdentifierStart(char character) =>
        character is '_' or '$' || char.IsLetter(character);

    private static bool IsIdentifierPart(char character) =>
        IsIdentifierStart(character) || char.IsDigit(character);
}
