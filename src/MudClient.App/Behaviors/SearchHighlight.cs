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
    private static readonly IBrush HighlightBrush = new SolidColorBrush(Color.FromArgb(0x8C, 0xFF, 0xC8, 0x2E));

    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, string?>("Text", typeof(SearchHighlight));

    public static readonly AttachedProperty<string?> TermsProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, string?>("Terms", typeof(SearchHighlight));

    static SearchHighlight()
    {
        TextProperty.Changed.AddClassHandler<TextBlock>((textBlock, _) => Update(textBlock));
        TermsProperty.Changed.AddClassHandler<TextBlock>((textBlock, _) => Update(textBlock));
    }

    public static string? GetText(TextBlock element) => element.GetValue(TextProperty);

    public static void SetText(TextBlock element, string? value) => element.SetValue(TextProperty, value);

    public static string? GetTerms(TextBlock element) => element.GetValue(TermsProperty);

    public static void SetTerms(TextBlock element, string? value) => element.SetValue(TermsProperty, value);

    private static void Update(TextBlock textBlock)
    {
        var text = GetText(textBlock) ?? string.Empty;
        var ranges = SearchText.FindMatchRanges(text, GetTerms(textBlock));
        if (ranges.Count == 0)
        {
            textBlock.Inlines = null;
            textBlock.Text = text;
            return;
        }

        var inlines = new InlineCollection();
        var position = 0;
        foreach (var (start, length) in ranges)
        {
            if (start > position)
            {
                inlines.Add(new Run(text[position..start]));
            }

            inlines.Add(new Run(text.Substring(start, length)) { Background = HighlightBrush });
            position = start + length;
        }

        if (position < text.Length)
        {
            inlines.Add(new Run(text[position..]));
        }

        textBlock.Inlines = inlines;
    }
}
