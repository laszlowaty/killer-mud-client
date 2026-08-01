using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace MudClient.App.Models;

public sealed class BookCatalogDocument
{
    public DateTimeOffset? GeneratedAtUtc { get; set; }

    public List<string> Classes { get; set; } = [];

    public List<BookEntry> Books { get; set; } = [];
}

/// <summary>One "Ładuje się w(na):" line from a book's description, with the room vnum pulled out
/// of it when the game included one (see <see cref="BookEntry.LoadLocationEntries"/>). Most
/// locations are just "na mobie: &lt;name&gt; (&lt;area&gt;)" with no vnum — the button this backs
/// only shows up for the ones that do carry one.</summary>
public sealed partial record BookLoadLocationEntry(string Text, string? RoomVnum)
{
    public bool HasRoomLocation => !string.IsNullOrWhiteSpace(RoomVnum);

    public static BookLoadLocationEntry Parse(string text) => new(text, ExtractVnum(text));

    private static string? ExtractVnum(string text)
    {
        var match = VnumRegex().Match(text);
        return match.Success ? match.Groups[1].Value : null;
    }

    [GeneratedRegex(@"vnum\D{0,10}(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex VnumRegex();
}

public sealed class BookEntry
{
    public int Vnum { get; set; }

    public string Name { get; set; } = string.Empty;

    public List<string> Classes { get; set; } = [];

    public List<string> Spells { get; set; } = [];

    public List<string> LoadLocations { get; set; } = [];

    [JsonIgnore]
    public string VnumText => Vnum.ToString();

    [JsonIgnore]
    public string ClassesText => Classes.Count == 0 ? "brak danych" : string.Join(", ", Classes);

    [JsonIgnore]
    public string SpellCountText => $"{Spells.Count} zaklęć";

    [JsonIgnore]
    public IReadOnlyList<BookLoadLocationEntry> LoadLocationEntries =>
        LoadLocations.Select(BookLoadLocationEntry.Parse).ToList();
}
