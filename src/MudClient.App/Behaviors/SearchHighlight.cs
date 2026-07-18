using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using MudClient.App.Services;

namespace MudClient.App.Behaviors;

/// <summary>
/// Attached properties that render <see cref="TextBlock"/> content with the fragments
/// matching the search terms highlighted, using the same diacritics-insensitive
/// normalization as the Killeropedia filters.
/// </summary>
public static class SearchHighlight
{
    private static readonly IBrush HighlightBrush = new SolidColorBrush(Color.FromArgb(0xB0, 0xD5, 0xA3, 0x4A));
    private static readonly IBrush HighlightForegroundBrush = new SolidColorBrush(Color.FromRgb(0x2C, 0x21, 0x10));

    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, string?>("Text", typeof(SearchHighlight));

    public static readonly AttachedProperty<string?> TermsProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, string?>("Terms", typeof(SearchHighlight));

    public static readonly AttachedProperty<bool> IsMatchProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, bool>("IsMatch", typeof(SearchHighlight));

    static SearchHighlight()
    {
        TextProperty.Changed.AddClassHandler<TextBlock>((textBlock, _) => Update(textBlock));
        TermsProperty.Changed.AddClassHandler<TextBlock>((textBlock, _) => Update(textBlock));
    }

    public static string? GetText(TextBlock element) => element.GetValue(TextProperty);

    public static void SetText(TextBlock element, string? value) => element.SetValue(TextProperty, value);

    public static string? GetTerms(TextBlock element) => element.GetValue(TermsProperty);

    public static void SetTerms(TextBlock element, string? value) => element.SetValue(TermsProperty, value);

    public static bool GetIsMatch(TextBlock element) => element.GetValue(IsMatchProperty);

    internal static void SetIsMatch(TextBlock element, bool value) => element.SetValue(IsMatchProperty, value);

    internal static Run CreateHighlightedRun(TextBlock host, string text) => new(text)
    {
        Background = HighlightBrush,
        Foreground = host.Foreground ?? HighlightForegroundBrush,
        FontFamily = host.FontFamily,
        FontSize = host.FontSize,
        FontStyle = host.FontStyle,
        FontWeight = FontWeight.Bold,
        LetterSpacing = host.LetterSpacing,
    };

    internal static Run CreatePlainRun(TextBlock host, string text) => new(text)
    {
        Foreground = host.Foreground,
        FontFamily = host.FontFamily,
        FontSize = host.FontSize,
        FontStyle = host.FontStyle,
        FontWeight = host.FontWeight,
        LetterSpacing = host.LetterSpacing,
    };

    private static void Update(TextBlock textBlock)
    {
        var text = GetText(textBlock) ?? string.Empty;
        var ranges = SearchText.FindMatchRanges(text, GetTerms(textBlock));
        if (ranges.Count == 0)
        {
            textBlock.SetValue(IsMatchProperty, false);
            textBlock.Inlines = null;
            textBlock.Text = text;
            return;
        }

        textBlock.SetValue(IsMatchProperty, true);

        var inlines = new InlineCollection();
        var position = 0;
        foreach (var (start, length) in ranges)
        {
            if (start > position)
            {
                inlines.Add(CreatePlainRun(textBlock, text[position..start]));
            }

            inlines.Add(CreateHighlightedRun(textBlock, text.Substring(start, length)));
            position = start + length;
        }

        if (position < text.Length)
        {
            inlines.Add(CreatePlainRun(textBlock, text[position..]));
        }

        textBlock.Inlines = inlines;
    }
}
