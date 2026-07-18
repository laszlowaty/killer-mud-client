using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using MudClient.App.Models;
using MudClient.App.Services;

namespace MudClient.App.Behaviors;

/// <summary>
/// Renders names of known lore entries as clickable inline links while retaining
/// search highlighting on the surrounding text.
/// </summary>
public static class LoreTextLinks
{
    public static readonly AttachedProperty<string?> TextProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, string?>("Text", typeof(LoreTextLinks));

    public static readonly AttachedProperty<IEnumerable?> EntriesProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, IEnumerable?>("Entries", typeof(LoreTextLinks));

    public static readonly AttachedProperty<ICommand?> CommandProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, ICommand?>("Command", typeof(LoreTextLinks));

    public static readonly AttachedProperty<string?> TermsProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, string?>("Terms", typeof(LoreTextLinks));

    public static readonly AttachedProperty<string?> CurrentEntryIdProperty =
        AvaloniaProperty.RegisterAttached<TextBlock, string?>("CurrentEntryId", typeof(LoreTextLinks));

    static LoreTextLinks()
    {
        TextProperty.Changed.AddClassHandler<TextBlock>((textBlock, _) => Update(textBlock));
        EntriesProperty.Changed.AddClassHandler<TextBlock>((textBlock, _) => Update(textBlock));
        CommandProperty.Changed.AddClassHandler<TextBlock>((textBlock, _) => Update(textBlock));
        TermsProperty.Changed.AddClassHandler<TextBlock>((textBlock, _) => Update(textBlock));
        CurrentEntryIdProperty.Changed.AddClassHandler<TextBlock>((textBlock, _) => Update(textBlock));
    }

    public static string? GetText(TextBlock element) => element.GetValue(TextProperty);
    public static void SetText(TextBlock element, string? value) => element.SetValue(TextProperty, value);
    public static IEnumerable? GetEntries(TextBlock element) => element.GetValue(EntriesProperty);
    public static void SetEntries(TextBlock element, IEnumerable? value) => element.SetValue(EntriesProperty, value);
    public static ICommand? GetCommand(TextBlock element) => element.GetValue(CommandProperty);
    public static void SetCommand(TextBlock element, ICommand? value) => element.SetValue(CommandProperty, value);
    public static string? GetTerms(TextBlock element) => element.GetValue(TermsProperty);
    public static void SetTerms(TextBlock element, string? value) => element.SetValue(TermsProperty, value);
    public static string? GetCurrentEntryId(TextBlock element) => element.GetValue(CurrentEntryIdProperty);
    public static void SetCurrentEntryId(TextBlock element, string? value) => element.SetValue(CurrentEntryIdProperty, value);

    private static void Update(TextBlock textBlock)
    {
        var text = GetText(textBlock) ?? string.Empty;
        var command = GetCommand(textBlock);
        var terms = GetTerms(textBlock);
        var currentEntryId = GetCurrentEntryId(textBlock);
        var candidates = BuildCandidates(GetEntries(textBlock), currentEntryId);
        var matches = FindLinks(text, candidates);

        var isSearchMatch = SearchText.FindMatchRanges(text, terms).Count > 0;
        if (matches.Count == 0)
        {
            SearchHighlight.SetText(textBlock, text);
            SearchHighlight.SetTerms(textBlock, terms);
            return;
        }

        // SearchHighlight also owns TextBlock.Inlines, so clear its attached inputs
        // before composing links and highlighted plain fragments ourselves.
        SearchHighlight.SetText(textBlock, null);
        SearchHighlight.SetTerms(textBlock, null);

        var inlines = new InlineCollection();
        var position = 0;
        foreach (var match in matches)
        {
            AddPlainText(textBlock, inlines, text[position..match.Start], terms);
            inlines.Add(CreateLink(textBlock, text.Substring(match.Start, match.Length), match.Entry, command, terms));
            position = match.Start + match.Length;
        }

        AddPlainText(textBlock, inlines, text[position..], terms);
        textBlock.Text = null;
        textBlock.Inlines = inlines;
        SearchHighlight.SetIsMatch(textBlock, isSearchMatch);
    }

    private static IReadOnlyList<LinkCandidate> BuildCandidates(IEnumerable? entries, string? currentEntryId)
    {
        if (entries is null)
        {
            return [];
        }

        var candidates = new Dictionary<string, LoreEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.OfType<LoreEntry>())
        {
            if (string.Equals(entry.Id, currentEntryId, StringComparison.Ordinal))
            {
                continue;
            }

            AddCandidate(candidates, entry.Name, entry);
            foreach (var alias in entry.Aliases)
            {
                AddCandidate(candidates, alias, entry);
            }
        }

        return candidates
            .Select(pair => new LinkCandidate(pair.Key, pair.Value))
            .OrderByDescending(candidate => candidate.Text.Length)
            .ThenBy(candidate => candidate.Text, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddCandidate(Dictionary<string, LoreEntry> candidates, string text, LoreEntry entry)
    {
        var normalized = text.Trim();
        if (normalized.Length >= 3)
        {
            candidates.TryAdd(normalized, entry);
        }
    }

    private static IReadOnlyList<LinkMatch> FindLinks(string text, IReadOnlyList<LinkCandidate> candidates)
    {
        var matches = new List<LinkMatch>();
        for (var position = 0; position < text.Length;)
        {
            var candidate = candidates.FirstOrDefault(item =>
                position + item.Text.Length <= text.Length
                && text.AsSpan(position, item.Text.Length).Equals(item.Text, StringComparison.OrdinalIgnoreCase)
                && IsBoundary(text, position - 1)
                && IsBoundary(text, position + item.Text.Length));

            if (candidate is null)
            {
                position++;
                continue;
            }

            matches.Add(new LinkMatch(position, candidate.Text.Length, candidate.Entry));
            position += candidate.Text.Length;
        }

        return matches;
    }

    private static bool IsBoundary(string text, int position) =>
        position < 0 || position >= text.Length || !char.IsLetterOrDigit(text[position]);

    private static void AddPlainText(TextBlock host, InlineCollection inlines, string text, string? terms)
    {
        if (text.Length == 0)
        {
            return;
        }

        var ranges = SearchText.FindMatchRanges(text, terms);
        var position = 0;
        foreach (var (start, length) in ranges)
        {
            if (start > position)
            {
                inlines.Add(SearchHighlight.CreatePlainRun(host, text[position..start]));
            }

            inlines.Add(SearchHighlight.CreateHighlightedRun(host, text.Substring(start, length)));
            position = start + length;
        }

        if (position < text.Length)
        {
            inlines.Add(SearchHighlight.CreatePlainRun(host, text[position..]));
        }
    }

    private static InlineUIContainer CreateLink(
        TextBlock host,
        string text,
        LoreEntry entry,
        ICommand? command,
        string? terms)
    {
        var label = new TextBlock
        {
            Text = text,
            FontFamily = host.FontFamily,
            FontSize = host.FontSize,
            FontStyle = host.FontStyle,
            FontWeight = host.FontWeight,
            LetterSpacing = host.LetterSpacing,
        };
        label.Classes.Add("killeropedia-inline-link-text");
        SearchHighlight.SetText(label, text);
        SearchHighlight.SetTerms(label, terms);

        var button = new Button
        {
            Content = label,
            Command = command,
            CommandParameter = new LoreLink(entry.Id, entry.Name, "Wspomniane w tekście", entry.Category),
        };
        button.Classes.Add("killeropedia-inline-link");
        return new InlineUIContainer(button)
        {
            BaselineAlignment = BaselineAlignment.TextBottom,
        };
    }

    private sealed record LinkCandidate(string Text, LoreEntry Entry);
    private sealed record LinkMatch(int Start, int Length, LoreEntry Entry);
}
