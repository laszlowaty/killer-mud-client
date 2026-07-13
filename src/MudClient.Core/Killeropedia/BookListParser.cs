using System.Text.RegularExpressions;

namespace MudClient.Core.Killeropedia;

public sealed record BookListSummary(int Vnum, string Name, IReadOnlyList<string> Spells);

public sealed record BookListDetails(
    string Name,
    IReadOnlyList<string> Spells,
    IReadOnlyList<string> LoadLocations);

/// <summary>Parses the textual output of the developer-only MUD <c>booklist</c> command.</summary>
public static partial class BookListParser
{
    public static IReadOnlyList<BookListSummary> ParseClassList(IEnumerable<string> lines)
    {
        var books = new List<BookListSummary>();
        foreach (var rawLine in lines)
        {
            var match = ListEntryRegex().Match(StripAnsi(rawLine).Trim());
            if (!match.Success || !int.TryParse(match.Groups["vnum"].Value, out var vnum))
            {
                continue;
            }

            books.Add(new BookListSummary(
                vnum,
                match.Groups["name"].Value.Trim(),
                ParseQuotedValues(match.Groups["spells"].Value)));
        }

        return books;
    }

    public static BookListDetails ParseDetails(IEnumerable<string> lines)
    {
        var cleanLines = lines.Select(line => StripAnsi(line).Trim()).ToArray();
        var headerIndex = Array.FindIndex(
            cleanLines,
            line => line.Contains("Informacje na temat ksiegi", StringComparison.OrdinalIgnoreCase));
        if (headerIndex < 0)
        {
            throw new FormatException("Odpowiedź nie zawiera nagłówka informacji o księdze.");
        }

        var name = cleanLines
            .Skip(headerIndex + 1)
            .FirstOrDefault(line => line.Length > 0
                && !line.StartsWith("Zaklecia:", StringComparison.OrdinalIgnoreCase)
                && !line.StartsWith("Laduje sie", StringComparison.OrdinalIgnoreCase)
                && !IsDecoration(line))
            ?? throw new FormatException("Odpowiedź nie zawiera nazwy księgi.");

        var spellLine = cleanLines.FirstOrDefault(
            line => line.StartsWith("Zaklecia:", StringComparison.OrdinalIgnoreCase));
        var locationsHeader = Array.FindIndex(
            cleanLines,
            line => line.StartsWith("Laduje sie", StringComparison.OrdinalIgnoreCase));
        var locations = locationsHeader < 0
            ? []
            : cleanLines
                .Skip(locationsHeader + 1)
                .SkipWhile(line => line.Length == 0)
                .TakeWhile(line => line.Length > 0 && !IsPrompt(line) && !IsDecoration(line))
                .Where(line => line.Contains(':'))
                .ToArray();

        return new BookListDetails(
            name,
            spellLine is null ? [] : ParseQuotedValues(spellLine),
            locations);
    }

    public static bool ContainsClassListHeader(IEnumerable<string> lines) =>
        lines.Any(line => StripAnsi(line).Contains("lista ksiag dla klasy", StringComparison.OrdinalIgnoreCase));

    public static bool ContainsDetailsHeader(IEnumerable<string> lines) =>
        lines.Any(line => StripAnsi(line).Contains("Informacje na temat ksiegi", StringComparison.OrdinalIgnoreCase));

    public static bool ContainsPagerPrompt(IEnumerable<string> lines) =>
        lines.Any(line => StripAnsi(line).Contains("Nacisnij Enter", StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<string> ParseQuotedValues(string text) =>
        QuotedValueRegex().Matches(text)
            .Select(match => match.Groups["value"].Value.Trim())
            .Where(value => value.Length > 0)
            .ToArray();

    private static bool IsDecoration(string line) =>
        line.StartsWith("<<", StringComparison.Ordinal) || line.All(character => character is '=' or '<' or '>');

    private static bool IsPrompt(string line) =>
        line.StartsWith('<')
        || line.StartsWith('>')
        || line.Contains("Nacisnij Enter", StringComparison.OrdinalIgnoreCase);

    private static string StripAnsi(string value) => AnsiRegex().Replace(value, string.Empty);

    [GeneratedRegex(@"^\[(?<vnum>\d+)\]\s+(?<name>.*?):\s*(?<spells>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex ListEntryRegex();

    [GeneratedRegex("'(?<value>[^']*)'", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedValueRegex();

    [GeneratedRegex("\\x1B\\[[0-?]*[ -/]*[@-~]", RegexOptions.CultureInvariant)]
    private static partial Regex AnsiRegex();
}
