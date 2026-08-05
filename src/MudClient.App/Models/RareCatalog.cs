using System.Text.Json.Serialization;

namespace MudClient.App.Models;

public sealed class RareCatalogDocument
{
    public DateTimeOffset? GeneratedAtUtc { get; set; }

    public List<RareEntry> Rares { get; set; } = [];
}

public sealed class RareEntry
{
    public int Vnum { get; set; }

    public string Name { get; set; } = string.Empty;

    public string ItemType { get; set; } = string.Empty;

    public string Slot { get; set; } = string.Empty;

    /// <summary>Single-letter status code from the rarelist line (e.g. "N"/"R"); the game doesn't
    /// document what it means, so it's surfaced as-is rather than translated.</summary>
    public string Flag { get; set; } = string.Empty;

    /// <summary>One of "artefakt", "rzadki", "instancyjny" — which rarelist section the item was
    /// listed under.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Raw text captured from "rarelist &lt;vnum&gt;" the last time the hidden refresh button ran
    /// while connected. The game's exact field layout for this response isn't known ahead of
    /// time, so it is kept verbatim rather than parsed into structured properties. Empty until
    /// a refresh has populated it.
    /// </summary>
    public string Details { get; set; } = string.Empty;

    [JsonIgnore]
    public string VnumText => Vnum.ToString();

    [JsonIgnore]
    public string CategoryText => Category switch
    {
        "artefakt" => "Artefakt",
        "rzadki" => "Rzadki (rare)",
        "instancyjny" => "Instancyjny (artefact/rare)",
        _ => "brak danych",
    };

    [JsonIgnore]
    public string TypeSlotText => string.IsNullOrWhiteSpace(Slot) ? ItemType : $"{ItemType} - {Slot}";

    [JsonIgnore]
    public bool HasDetails => !string.IsNullOrWhiteSpace(Details);

    [JsonIgnore]
    public string SearchableText => string.Join(' ', Vnum, Name, ItemType, Slot, CategoryText, Details);
}
