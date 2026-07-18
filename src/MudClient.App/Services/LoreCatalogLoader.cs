using System.IO.Compression;
using System.Reflection;
using System.Text.Json;
using MudClient.App.Models;

namespace MudClient.App.Services;

internal static class LoreCatalogLoader
{
    private const string FileName = "lore-catalog.json.gz";
    private const string ResourceName = "MudClient.App.Assets.Data.lore-catalog.json.gz";
    private static readonly Lazy<LoreCatalogData> Catalog = new(LoadCore);

    public static LoreCatalogData Load() => Catalog.Value;

    internal static LoreCatalogData Load(string dataRoot) => LoadCore(dataRoot);

    internal static LoreCatalogData Load(Stream stream, string sourceText) => Parse(stream, sourceText, null);

    internal static LoreCatalogData LoadFile(string path)
    {
        using var file = File.OpenRead(path);
        return Parse(file, path, null);
    }

    internal static LoreCatalogData LoadEmbedded()
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Brak wbudowanego katalogu lore: {ResourceName}.");
        return Parse(resource, "katalog wbudowany", null);
    }

    private static LoreCatalogData LoadCore() => LoadCore(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "KillerMudClient"));

    private static LoreCatalogData LoadCore(string dataRoot)
    {
        var warnings = new List<string>();
        var candidates = new[]
        {
            Path.Combine(dataRoot, "Data", FileName),
            Path.Combine(AppContext.BaseDirectory, "Data", FileName),
        }.Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var path in candidates)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                using var file = File.OpenRead(path);
                return Parse(file, path, warnings.Count == 0 ? null : string.Join(Environment.NewLine, warnings));
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or JsonException
                or ArgumentException)
            {
                // External catalogs are optional overrides. Keep the application usable by
                // reporting the failure and trying the packaged or embedded fallback.
                warnings.Add($"Nie udało się wczytać {path}: {exception.Message}");
            }
        }

        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Brak wbudowanego katalogu lore: {ResourceName}.");
        return Parse(resource, "katalog wbudowany", warnings.Count == 0 ? null : string.Join(Environment.NewLine, warnings));
    }

    private static LoreCatalogData Parse(Stream stream, string sourceText, string? warning)
    {
        using var gzip = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
        var catalog = JsonSerializer.Deserialize<CatalogDto>(gzip, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        }) ?? throw new InvalidDataException("Katalog lore jest pusty.");

        if (catalog.SchemaVersion != 1)
        {
            throw new InvalidDataException($"Nieobsługiwana wersja katalogu lore: {catalog.SchemaVersion}.");
        }

        var articles = catalog.Entries.ToDictionary(article => article.EntityId, StringComparer.Ordinal);
        var entriesById = new Dictionary<string, LoreEntry>(StringComparer.Ordinal);
        var directRelatedIds = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var record in catalog.Records)
        {
            articles.TryGetValue(record.Id, out var article);
            var sourceRefs = record.SourceRefs
                .Concat(article?.Sections.SelectMany(section => section.SourceRefs) ?? [])
                .Select(FormatSource)
                .Distinct(StringComparer.Ordinal)
                .Select(text => new LoreSourceReference(text))
                .ToArray();
            var map = MergeMapReferences(record.MapReferences, article?.MapReferences);
            var entry = new LoreEntry
            {
                Id = record.Id,
                ArticleId = article?.Id,
                Name = article?.Title ?? record.Name,
                Category = CategoryFor(record.RecordType, record.Kind),
                KindText = KindLabel(record.RecordType, record.Kind),
                Summary = article?.Summary ?? record.Summary,
                Description = record.Description,
                StatusText = StatusLabel(record.Status),
                TruthText = TruthLabel(record.TruthStatus),
                TimeText = record.Time?.Label ?? string.Empty,
                Aliases = record.Aliases,
                Domains = record.Domains,
                Tags = record.Tags.Concat(article?.Tags ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                Sections = article?.Sections.Select(section => new LoreSection(section.Title, section.Content, section.Type)).ToArray() ?? [],
                Facts = record.Facts.Select(fact => new LoreFact(
                    PredicateLabel(fact.Predicate),
                    JsonValueText(fact.Value),
                    FactQualifier(fact.TruthStatus, fact.EvidenceStatus, fact.Confidence))).ToArray(),
                Sources = sourceRefs,
                AreaFiles = map.AreaFiles,
                RoomVnums = map.RoomVnums,
            };
            entriesById.Add(entry.Id, entry);
            directRelatedIds[entry.Id] = record.RelatedIds
                .Concat(article?.RelatedIds ?? [])
                .ToHashSet(StringComparer.Ordinal);
        }

        foreach (var article in catalog.Entries.Where(article => !entriesById.ContainsKey(article.EntityId)))
        {
            var entry = new LoreEntry
            {
                Id = article.EntityId,
                ArticleId = article.Id,
                Name = article.Title,
                Category = CategoryFor("article", article.Category),
                KindText = KindLabel("article", article.Category),
                Summary = article.Summary,
                StatusText = "Artykuł",
                Tags = article.Tags,
                Sections = article.Sections.Select(section => new LoreSection(section.Title, section.Content, section.Type)).ToArray(),
                Sources = article.Sections.SelectMany(section => section.SourceRefs)
                    .Select(FormatSource)
                    .Distinct(StringComparer.Ordinal)
                    .Select(text => new LoreSourceReference(text))
                    .ToArray(),
                AreaFiles = article.MapReferences.AreaFiles,
                RoomVnums = article.MapReferences.RoomVnums,
            };
            entriesById.Add(entry.Id, entry);
            directRelatedIds[entry.Id] = article.RelatedIds.ToHashSet(StringComparer.Ordinal);
        }

        foreach (var (entryId, relatedIds) in directRelatedIds)
        {
            var entry = entriesById[entryId];
            foreach (var targetId in relatedIds)
            {
                AddLink(entry, entriesById, targetId, "Powiązane");
            }
        }

        foreach (var relation in catalog.Relations)
        {
            if (!entriesById.TryGetValue(relation.SubjectId, out var subject)
                || !entriesById.TryGetValue(relation.TargetId, out var target))
            {
                continue;
            }

            AddLink(subject, entriesById, target.Id, RelationLabel(relation.Predicate, inverse: false));
            AddLink(target, entriesById, subject.Id, RelationLabel(relation.Predicate, inverse: true));
        }

        var categoryOrder = new[]
        {
            "Miejsca", "Postacie", "Organizacje", "Bóstwa", "Artefakty",
            "Wydarzenia", "Legendy i wierzenia", "Ludy i kultury", "Pozostałe",
        };
        var order = categoryOrder.Select((category, index) => (category, index))
            .ToDictionary(pair => pair.category, pair => pair.index, StringComparer.Ordinal);
        var entries = entriesById.Values
            .OrderBy(entry => order.GetValueOrDefault(entry.Category, int.MaxValue))
            .ThenBy(entry => entry.Name, StringComparer.Create(new System.Globalization.CultureInfo("pl-PL"), true))
            .ToArray();

        return new LoreCatalogData(entries, catalog.GeneratedAt, sourceText, warning);
    }

    private static void AddLink(
        LoreEntry source,
        IReadOnlyDictionary<string, LoreEntry> entriesById,
        string targetId,
        string relationText)
    {
        if (targetId == source.Id || !entriesById.TryGetValue(targetId, out var target))
        {
            return;
        }

        var existingIndex = source.Links.FindIndex(link => link.TargetId == targetId);
        if (existingIndex >= 0)
        {
            var existing = source.Links[existingIndex];
            if (existing.RelationText == relationText || relationText == "Powiązane")
            {
                return;
            }

            var mergedRelation = existing.RelationText == "Powiązane"
                ? relationText
                : $"{existing.RelationText} · {relationText}";
            source.Links[existingIndex] = existing with { RelationText = mergedRelation };
            return;
        }

        source.Links.Add(new LoreLink(targetId, target.Name, relationText, target.Category));
        source.Links.Sort((left, right) => string.Compare(left.TargetName, right.TargetName, StringComparison.OrdinalIgnoreCase));
    }

    private static MapReferencesDto MergeMapReferences(MapReferencesDto primary, MapReferencesDto? secondary) => new()
    {
        AreaFiles = primary.AreaFiles.Concat(secondary?.AreaFiles ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
        RoomVnums = primary.RoomVnums.Concat(secondary?.RoomVnums ?? []).Distinct(StringComparer.Ordinal).ToArray(),
    };

    private static string CategoryFor(string recordType, string kind) => (recordType, kind) switch
    {
        ("entity", "city" or "settlement" or "landmark" or "region" or "continent" or "world") => "Miejsca",
        ("entity", "character") => "Postacie",
        ("entity", "organization") => "Organizacje",
        ("entity", "deity") => "Bóstwa",
        ("entity", "artifact") => "Artefakty",
        ("entity", "culture" or "people" or "language") => "Ludy i kultury",
        ("event", _) => "Wydarzenia",
        ("narrative", _) => "Legendy i wierzenia",
        ("article", "city" or "settlement" or "landmark" or "region") => "Miejsca",
        _ => "Pozostałe",
    };

    private static string KindLabel(string recordType, string kind) => (recordType, kind) switch
    {
        ("entity", "city") => "Miasto",
        ("entity", "settlement") => "Osada",
        ("entity", "landmark") => "Miejsce",
        ("entity", "region") => "Region",
        ("entity", "continent") => "Kontynent",
        ("entity", "world") => "Świat",
        ("entity", "character") => "Postać",
        ("entity", "organization") => "Organizacja",
        ("entity", "deity") => "Bóstwo",
        ("entity", "artifact") => "Artefakt",
        ("entity", "culture") => "Kultura",
        ("entity", "people") => "Lud",
        ("entity", "language") => "Język",
        ("event", _) => "Wydarzenie",
        ("narrative", "legend") => "Legenda",
        ("narrative", "myth") => "Mit",
        ("narrative", "belief") => "Wierzenie",
        ("narrative", "artifact-legend") => "Legenda artefaktu",
        ("narrative", "prophecy") => "Proroctwo",
        ("narrative", "folktale") => "Podanie ludowe",
        ("narrative", "rumor") => "Plotka",
        ("narrative", "other") => "Inna opowieść",
        _ => "Inny typ",
    };

    private static string StatusLabel(string status) => status switch
    {
        "canonical" => "Kanoniczne",
        "reviewed" => "Zweryfikowane",
        "extracted" => "Wydobyte ze źródeł",
        _ => "Stan roboczy",
    };

    private static string TruthLabel(string? truthStatus) => truthStatus switch
    {
        "accepted" => "Fakt przyjęty",
        "belief" => "Wierzenie",
        "legend" => "Legenda",
        "rumor" => "Plotka",
        "unknown" => "Niepewne",
        _ => string.Empty,
    };

    private static string PredicateLabel(string predicate) => predicate switch
    {
        "economic_character" => "Charakter gospodarczy",
        "population_character" => "Ludność",
        "office" => "Urząd",
        "religious_institution" => "Instytucja religijna",
        "urban_character" => "Charakter miasta",
        "regional_influence" => "Znaczenie regionalne",
        "historical_function" => "Dawna funkcja",
        "current_function" => "Obecna funkcja",
        "worship_teaching" => "Nauczanie kultu",
        "divine_domain" => "Domena",
        "attacked" => "Atak",
        "is_application_map_region" => "Region mapy klienta",
        "contains_named_peak" => "Szczyt pasma",
        "current_use" => "Obecne wykorzystanie",
        "foundation_account" => "Przekaz o założeniu",
        "local_role" => "Rola lokalna",
        "local_status" => "Status lokalny",
        "local_use" => "Lokalne wykorzystanie",
        "patron_deity" => "Bóstwo opiekuńcze",
        "regional_scale" => "Skala regionalna",
        "religious_symbol" => "Symbol religijny",
        "role" => "Rola",
        "settlement_character" => "Charakter osady",
        "settlement_layout" => "Układ osady",
        "strategic_function" => "Znaczenie strategiczne",
        "terrain" => "Ukształtowanie terenu",
        "winter_state" => "Warunki zimowe",
        _ => "Inna właściwość",
    };

    private static string RelationLabel(string predicate, bool inverse) => (predicate, inverse) switch
    {
        ("contains_on_application_map", false) => "Zawiera na mapie",
        ("contains_on_application_map", true) => "Należy do regionu mapy",
        ("north_of", false) => "Leży na północ od",
        ("north_of", true) => "Leży na południe od",
        ("connected_by_road", _) => "Połączone traktem",
        ("flows_through", false) => "Przepływa przez",
        ("flows_through", true) => "Leży nad",
        ("governs", false) => "Zarządza",
        ("governs", true) => "Zarządzane przez",
        ("commands", false) => "Dowodzi",
        ("commands", true) => "Dowodzone przez",
        ("hosts", false) => "Mieści",
        ("hosts", true) => "Znajduje się w",
        ("worships", false) => "Czczone bóstwo",
        ("worships", true) => "Czczone w",
        ("founded_by_tradition", false) => "Według tradycji założone przez",
        ("founded_by_tradition", true) => "Według tradycji założył",
        ("adjacent_to", _) => "Sąsiaduje z",
        ("based_in", false) => "Działa w",
        ("based_in", true) => "Siedziba organizacji",
        ("founded_by_account", false) => "Według przekazu założone przez",
        ("founded_by_account", true) => "Według przekazu założył",
        ("occupied_by", false) => "Zajmowane przez",
        ("occupied_by", true) => "Zajmuje",
        ("resides_in", false) => "Mieszka w",
        ("resides_in", true) => "Miejsce zamieszkania",
        _ => "Powiązane",
    };

    private static string FactQualifier(string truth, string evidence, string confidence)
    {
        var truthText = TruthLabel(truth);
        var evidenceText = evidence switch
        {
            "explicit" => "źródło bezpośrednie",
            "inferred" => "wniosek",
            "disputed" => "sporne",
            _ => evidence,
        };
        var confidenceText = confidence switch
        {
            "high" => "wysoka pewność odczytu",
            "medium" => "średnia pewność odczytu",
            "low" => "niska pewność odczytu",
            _ => confidence,
        };
        return string.Join(" · ", new[] { truthText, evidenceText, confidenceText }.Where(text => !string.IsNullOrWhiteSpace(text)));
    }

    private static string JsonValueText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? string.Empty,
        JsonValueKind.True => "tak",
        JsonValueKind.False => "nie",
        JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(JsonValueText)),
        JsonValueKind.Object => value.GetRawText(),
        JsonValueKind.Null => "brak danych",
        _ => value.GetRawText(),
    };

    private static string FormatSource(SourceRefDto source)
    {
        var parts = new List<string> { source.File };
        if (!string.IsNullOrWhiteSpace(source.Section))
        {
            parts.Add(source.Section);
        }

        if (source.Vnum.ValueKind is JsonValueKind.String or JsonValueKind.Number)
        {
            parts.Add($"vnum {JsonValueText(source.Vnum)}");
        }

        if (!string.IsNullOrWhiteSpace(source.Field))
        {
            parts.Add(source.Field);
        }

        if (!string.IsNullOrWhiteSpace(source.Keyword))
        {
            parts.Add($"hasło {source.Keyword}");
        }

        return string.Join(" · ", parts);
    }

    private sealed class CatalogDto
    {
        public int SchemaVersion { get; init; }
        public DateTimeOffset? GeneratedAt { get; init; }
        public ArticleDto[] Entries { get; init; } = [];
        public RecordDto[] Records { get; init; } = [];
        public RelationDto[] Relations { get; init; } = [];
    }

    private sealed class ArticleDto
    {
        public string Id { get; init; } = string.Empty;
        public string EntityId { get; init; } = string.Empty;
        public string Category { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public SectionDto[] Sections { get; init; } = [];
        public string[] RelatedIds { get; init; } = [];
        public MapReferencesDto MapReferences { get; init; } = new();
        public string[] Tags { get; init; } = [];
    }

    private sealed class RecordDto
    {
        public string Id { get; init; } = string.Empty;
        public string RecordType { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string[] Aliases { get; init; } = [];
        public string Summary { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string[] Domains { get; init; } = [];
        public string[] Tags { get; init; } = [];
        public string Status { get; init; } = string.Empty;
        public string? TruthStatus { get; init; }
        public TimeDto? Time { get; init; }
        public MapReferencesDto MapReferences { get; init; } = new();
        public SourceRefDto[] SourceRefs { get; init; } = [];
        public string[] RelatedIds { get; init; } = [];
        public FactDto[] Facts { get; init; } = [];
    }

    private sealed class RelationDto
    {
        public string SubjectId { get; init; } = string.Empty;
        public string Predicate { get; init; } = string.Empty;
        public string TargetId { get; init; } = string.Empty;
    }

    private sealed class SectionDto
    {
        public string Type { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public SourceRefDto[] SourceRefs { get; init; } = [];
    }

    private sealed class FactDto
    {
        public string Predicate { get; init; } = string.Empty;
        public JsonElement Value { get; init; }
        public string EvidenceStatus { get; init; } = string.Empty;
        public string TruthStatus { get; init; } = string.Empty;
        public string Confidence { get; init; } = string.Empty;
    }

    private sealed class TimeDto
    {
        public string Label { get; init; } = string.Empty;
    }

    private sealed class MapReferencesDto
    {
        public string[] AreaFiles { get; init; } = [];
        public string[] RoomVnums { get; init; } = [];
    }

    private sealed class SourceRefDto
    {
        public string File { get; init; } = string.Empty;
        public string Section { get; init; } = string.Empty;
        public JsonElement Vnum { get; init; }
        public string Field { get; init; } = string.Empty;
        public string Keyword { get; init; } = string.Empty;
    }
}
