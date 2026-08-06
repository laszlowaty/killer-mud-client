using System.Text.RegularExpressions;
using MudClient.Core.Text;

namespace MudClient.Core.Killeropedia;

public sealed record RareListEntry(
    int Vnum,
    string Name,
    string ItemType,
    string Slot,
    string Flag,
    string Category);

/// <summary>
/// Parses the textual output of the developer-only MUD <c>rarelist</c> command. Unlike
/// <see cref="BookListParser"/>, the per-vnum detail command (<c>rarelist &lt;vnum&gt;</c>) has no
/// known field layout to parse against, so its output is only ever captured verbatim
/// (<see cref="ExtractDetailText"/>) rather than broken into structured fields.
/// </summary>
public static partial class RareListParser
{
    public static IReadOnlyList<RareListEntry> ParseList(IEnumerable<string> lines)
    {
        var entries = new List<RareListEntry>();
        var category = string.Empty;

        foreach (var rawLine in lines)
        {
            var line = AnsiText.StripAnsi(rawLine).Trim();
            var detectedCategory = DetectCategory(line);
            if (detectedCategory is not null)
            {
                category = detectedCategory;
                continue;
            }

            var match = ItemLineRegex().Match(line);
            if (!match.Success || !int.TryParse(match.Groups["vnum"].Value, out var vnum))
            {
                continue;
            }

            var typeSlot = match.Groups["typeslot"].Value.Split('-', 2);
            entries.Add(new RareListEntry(
                vnum,
                match.Groups["name"].Value.Trim(),
                typeSlot[0].Trim(),
                typeSlot.Length > 1 ? typeSlot[1].Trim() : string.Empty,
                match.Groups["flag"].Value,
                category));
        }

        return entries;
    }

    /// <summary>
    /// The fields <c>rarelist &lt;vnum&gt;</c> reports aren't known ahead of time, so this just
    /// strips ANSI/prompt/pager noise and returns whatever the MUD actually sent back, verbatim.
    /// </summary>
    public static string ExtractDetailText(IEnumerable<string> lines)
    {
        var cleaned = lines
            .Select(line => AnsiText.StripAnsi(line).TrimEnd())
            .Where(line => !IsPrompt(line)
                && !line.Contains("Nacisnij Enter", StringComparison.OrdinalIgnoreCase))
            .ToList();

        while (cleaned.Count > 0 && cleaned[0].Length == 0)
        {
            cleaned.RemoveAt(0);
        }

        while (cleaned.Count > 0 && cleaned[^1].Length == 0)
        {
            cleaned.RemoveAt(cleaned.Count - 1);
        }

        return string.Join('\n', cleaned);
    }

    public static bool ContainsPagerPrompt(IEnumerable<string> lines) =>
        lines.Any(line => AnsiText.StripAnsi(line).Contains("Nacisnij Enter", StringComparison.OrdinalIgnoreCase));

    private static string? DetectCategory(string line)
    {
        if (line.Contains("lista przedmiotow unikalnych", StringComparison.OrdinalIgnoreCase))
        {
            return "artefakt";
        }

        if (line.Contains("lista przedmiotow dostepnych tylko w instancji", StringComparison.OrdinalIgnoreCase))
        {
            return "instancyjny";
        }

        if (line.Contains("lista przedmiotow wyjatkowych", StringComparison.OrdinalIgnoreCase))
        {
            return "rzadki";
        }

        return null;
    }

    private static bool IsPrompt(string line) => line.StartsWith('<') || line.StartsWith('>');

    [GeneratedRegex(
        @"^\+\[(?<decay>-?\d+)\s*d\]\s*\[(?<flag>[A-Za-z])\]\s*\(\s*(?<typeslot>[^)]+?)\s*\)\s*\[\s*(?<vnum>\d+)\s*\]\s*(?<name>.+)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ItemLineRegex();
}
