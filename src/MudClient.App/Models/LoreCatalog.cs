namespace MudClient.App.Models;

public sealed record LoreCatalogData(
    IReadOnlyList<LoreEntry> Entries,
    DateTimeOffset? GeneratedAtUtc,
    string SourceText,
    string? Warning = null);

public sealed class LoreEntry
{
    public string Id { get; init; } = string.Empty;

    public string? ArticleId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string KindText { get; init; } = string.Empty;

    public string Summary { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string StatusText { get; init; } = string.Empty;

    public string TruthText { get; init; } = string.Empty;

    public string TimeText { get; init; } = string.Empty;

    public IReadOnlyList<string> Aliases { get; init; } = [];

    public IReadOnlyList<string> Domains { get; init; } = [];

    public IReadOnlyList<string> Tags { get; init; } = [];

    public IReadOnlyList<LoreSection> Sections { get; init; } = [];

    public List<LoreLink> Links { get; } = [];

    public IReadOnlyList<LoreFact> Facts { get; init; } = [];

    public IReadOnlyList<LoreSourceReference> Sources { get; init; } = [];

    public IReadOnlyList<string> AreaFiles { get; init; } = [];

    public IReadOnlyList<string> RoomVnums { get; init; } = [];

    public string AliasesText => Aliases.Count == 0 ? string.Empty : string.Join(", ", Aliases);

    public string DomainsText => Domains.Count == 0 ? string.Empty : string.Join(", ", Domains);

    public string MapText
    {
        get
        {
            var parts = new List<string>();
            if (AreaFiles.Count > 0)
            {
                parts.Add($"krainy: {string.Join(", ", AreaFiles)}");
            }

            if (RoomVnums.Count > 0)
            {
                parts.Add($"pomieszczenia: {string.Join(", ", RoomVnums)}");
            }

            return string.Join(" · ", parts);
        }
    }

    public bool HasAliases => Aliases.Count > 0;

    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);

    public bool HasTruth => !string.IsNullOrWhiteSpace(TruthText);

    public bool HasTime => !string.IsNullOrWhiteSpace(TimeText);

    public bool HasMapReferences => AreaFiles.Count > 0 || RoomVnums.Count > 0;

    public string SearchableText => string.Join(' ',
        Id,
        ArticleId,
        Name,
        Category,
        KindText,
        Summary,
        Description,
        AliasesText,
        DomainsText,
        string.Join(' ', Tags),
        string.Join(' ', Sections.Select(section => $"{section.Title} {section.Content}")),
        string.Join(' ', Facts.Select(fact => $"{fact.Label} {fact.ValueText} {fact.QualifierText}")),
        string.Join(' ', Links.Select(link => $"{link.TargetName} {link.RelationText}")),
        string.Join(' ', Sources.Select(source => source.DisplayText)));
}

public sealed record LoreSection(string Title, string Content, string Type);

public sealed record LoreLink(
    string TargetId,
    string TargetName,
    string RelationText,
    string Category);

public sealed record LoreFact(string Label, string ValueText, string QualifierText);

public sealed record LoreSourceReference(string DisplayText);
