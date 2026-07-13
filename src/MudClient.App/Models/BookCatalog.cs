using System.Text.Json.Serialization;

namespace MudClient.App.Models;

public sealed class BookCatalogDocument
{
    public DateTimeOffset? GeneratedAtUtc { get; set; }

    public List<string> Classes { get; set; } = [];

    public List<BookEntry> Books { get; set; } = [];
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
}
