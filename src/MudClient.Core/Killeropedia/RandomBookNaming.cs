using System.Text.RegularExpressions;

namespace MudClient.Core.Killeropedia;

/// <summary>
/// Random magic-book ("random 'spellbook'") item names in this MUD are algorithmically built from
/// word pools: an optional value/weight adjective, then a generic noun, then a word that marks
/// which spellcasting class the book belongs to — e.g. "duża księga triumfu" = duża (value-large) +
/// księga (noun) + triumfu (Paladyn). Source: killer.fandom.com/pl/wiki/Losowe_księgi_magiczne_(random_'spellbook').
/// </summary>
public static class RandomBookNaming
{
    public static IReadOnlyList<string> WartoscDuzaWords { get; } =
    [
        "duża", "masywna", "kolosalna", "ogromna", "potężna", "spora", "wielgachna", "znaczna",
        "gigantyczna", "gargantuiczna", "twarda", "pokaźna", "monumentalna", "nowa", "nieużywana",
        "idealna", "okuta", "zdobiona", "wzmocniona", "błyszcząca", "lśniąca", "wielka", "szeroka",
        "tęga", "nowiusieńka",
    ];

    public static IReadOnlyList<string> WartoscMalaWords { get; } =
    [
        "mała", "delikatna", "filigranowa", "nieduża", "drobna", "niepokaźna", "kieszonkowa",
        "stara", "używana", "zniszczona", "sfatygowana", "zdezelowana", "złachmaniona",
        "skomkana", "wysłużona", "podniszczona", "zeszmacona", "wytarta", "zmaltretowana",
        "sparciała", "zaśniedziała", "przetarta",
    ];

    public static IReadOnlyList<string> WagaMalaWords { get; } =
    [
        "lekka", "niewielka", "malutka", "wąska", "lilipucia", "tycia", "cienka", "miękka", "płaska",
    ];

    public static IReadOnlyList<string> NazwaWords { get; } =
    [
        "dzieło", "cymelium", "tom", "inkunabuł", "wolumen", "wolumin", "foliał", "foliant",
        "rękopis", "księga", "książka",
    ];

    public static IReadOnlyList<string> MagWords { get; } =
    [
        "magii", "czarów", "dymu", "kurzu", "sztuczek", "trików", "gestów", "mocy", "woli",
        "inteligencji", "wiedzy", "potęgi", "z pentagramem na okładce", "z rubinami", "z szafirami",
        "z topazami", "z granatami", "z czerwoną wstążeczką",
    ];

    public static IReadOnlyList<string> KlerykWords { get; } =
    [
        "błogosławieństwa", "modlitwy", "Zapomnianego Boga", "wiary", "przekleństwa", "zaufania",
        "medytacji", "skupienia", "sakramentu", "przebaczenia", "uzdrowienia", "odpuszczenia",
    ];

    public static IReadOnlyList<string> PaladynWords { get; } =
    [
        "miecza", "tarczy", "buzdyganu", "włóczni", "siły", "Portena", "walki", "parady",
        "satysfakcji", "triumfu", "zwycięstwa",
    ];

    public static IReadOnlyList<string> DruidWords { get; } =
    [
        "lasu", "puszczy", "roślin", "kwiatów", "zwierząt", "owadów", "oceanów", "rzek",
        "górskich szczytów", "bagna", "piargów", "wiatru",
    ];

    public static IReadOnlyList<string> NomadWords { get; } =
    [
        "piasku", "włóczęgi", "scimitara", "zamieci", "tańca", "słońca", "pogardy",
    ];

    private static readonly IReadOnlyDictionary<string, string> ClassByWord = BuildClassLookup();

    private static readonly Regex Pattern = BuildPattern();

    /// <summary>
    /// Finds "&lt;noun&gt; &lt;class word&gt;" (optionally preceded by a recognized value/weight
    /// adjective) in <paramref name="line"/> and appends " (Klasa)" right after each match. Returns
    /// the line unchanged when nothing matches.
    /// </summary>
    public static string AnnotateClasses(string line) =>
        !line.Contains(' ') ? line : Pattern.Replace(line, EvaluateMatch);

    private static string EvaluateMatch(Match match)
    {
        var classWord = match.Groups["classword"].Value;
        return ClassByWord.TryGetValue(classWord, out var className)
            ? $"{match.Value} ({className})"
            : match.Value;
    }

    private static IReadOnlyDictionary<string, string> BuildClassLookup()
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddAll(lookup, MagWords, "Mag");
        AddAll(lookup, KlerykWords, "Kleryk");
        AddAll(lookup, PaladynWords, "Paladyn");
        AddAll(lookup, DruidWords, "Druid");
        AddAll(lookup, NomadWords, "Nomad");
        return lookup;
    }

    private static void AddAll(Dictionary<string, string> lookup, IReadOnlyList<string> words, string className)
    {
        foreach (var word in words)
        {
            lookup[word] = className;
        }
    }

    private static Regex BuildPattern()
    {
        var adjectives = WartoscDuzaWords.Concat(WartoscMalaWords).Concat(WagaMalaWords).Distinct();
        var classWords = MagWords.Concat(KlerykWords).Concat(PaladynWords).Concat(DruidWords).Concat(NomadWords);

        var adjectiveAlternation = Alternation(adjectives);
        var nazwaAlternation = Alternation(NazwaWords);
        var classWordAlternation = Alternation(classWords);

        return new Regex(
            $@"(?:\b(?:{adjectiveAlternation})\s+)?\b(?:{nazwaAlternation})\s+(?<classword>{classWordAlternation})\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }

    private static string Alternation(IEnumerable<string> phrases) =>
        string.Join('|', phrases.OrderByDescending(phrase => phrase.Length).Select(Regex.Escape));
}
