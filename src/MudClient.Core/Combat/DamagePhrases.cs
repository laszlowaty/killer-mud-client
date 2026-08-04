using System.Text.RegularExpressions;
using MudClient.Core.Text;

namespace MudClient.Core.Combat;

/// <summary>
/// Maps this MUD's combat damage verbs to their approximate numeric tier, for lines that mean
/// "you dealt this damage".
///
/// Two message shapes both count as your own damage:
///  - 2nd person: "Ranisz golema mieczem." — you're literally the grammatical subject. Ends in
///    "sz"/"SZ" and is unambiguous on its own.
///  - 3rd person via a named technique: "Twoje miażdżące walnięcie dewastuje sędziwego
///    krasnoluda." — the subject is the technique noun ("Twoje ... walnięcie"), not "you", so the
///    verb conjugates in 3rd person ("dewastuje") even though it's your own hit. On its own this
///    form is ambiguous (the same verb describes a mob hitting you, or bystander-visible combat
///    between others) — it only counts here when the line also contains "Twoj*" ("Twoje"/"Twój"/
///    "Twoja"/"Twoim"/...), confirming the technique is yours.
///
/// A couple of encoding-mangled variants (e.g. the "Å" mojibake) are kept too, in case a client
/// encoding misdetection ever produces them — they cost nothing to keep around.
/// </summary>
public static class DamagePhrases
{
    private static readonly IReadOnlyDictionary<string, int> SelfVerbValues = new Dictionary<string, int>
    {
        ["Chybiasz"] = 0,
        ["chybiasz"] = 0,
        ["chybiajÄc"] = 0,
        ["chybiajac"] = 0,
        ["Siniaczysz"] = 2,
        ["siniaczysz"] = 2,
        ["Muskasz"] = 6,
        ["muskasz"] = 6,
        ["Ledwie ranisz"] = 10,
        ["ledwie ranisz"] = 10,
        ["Lekko ranisz"] = 14,
        ["lekko ranisz"] = 14,
        // The source table only had "Eanisz" (an R→E misread/typo) for this tier's capitalized
        // form, never the correctly spelled "Ranisz" — added here so a hit landing at the start
        // of a sentence is still recognized; "Eanisz" is kept too in case the server really does
        // send it.
        ["Ranisz"] = 18,
        ["Eanisz"] = 18,
        ["ranisz"] = 18,
        ["Mocno ranisz"] = 22,
        ["mocno ranisz"] = 22,
        ["Dotkliwie ranisz"] = 26,
        ["dotkliwie ranisz"] = 26,
        ["Powaznie ranisz"] = 30,
        ["powaznie ranisz"] = 30,
        ["PowaÅ¼nie ranisz"] = 30,
        ["powaÅ¼nie ranisz"] = 30,
        ["Masakrujesz"] = 34,
        ["masakrujesz"] = 34,
        ["Rozpruwasz"] = 38,
        ["rozpruwasz"] = 38,
        ["Dewastujesz"] = 44,
        ["dewastujesz"] = 44,
        ["Grzmocisz"] = 50,
        ["grzmocisz"] = 50,
        ["Niszczysz"] = 55,
        ["niszczysz"] = 55,
        ["NISZCZYSZ"] = 60,
        ["DRUZGOCZESZ"] = 67,
        ["ROZPRUWASZ"] = 75,
        ["ROZRYWASZ"] = 84,
        ["ROZBEBESZASZ"] = 100,
        ["DEKAPITUJESZ"] = 115,
        ["EKSTYRPUJESZ"] = 130,
        ["ANIHILUJESZ"] = 145,
        ["USMIERCASZ"] = 200,
        ["UÅMIERCASZ"] = 200,
        ["UNICESTWIASZ"] = 201,
    };

    private static readonly IReadOnlyDictionary<string, int> TechniqueVerbValues = new Dictionary<string, int>
    {
        ["chybia"] = 0,
        ["siniaczy"] = 2,
        ["muska"] = 6,
        ["ledwie rani"] = 10,
        ["lekko rani"] = 14,
        ["rani"] = 18,
        ["mocno rani"] = 22,
        ["dotkliwie rani"] = 26,
        ["powaznie rani"] = 30,
        ["powaÅ¼nie rani"] = 30,
        ["masakruje"] = 34,
        ["rozpruwa"] = 38,
        ["dewastuje"] = 44,
        ["grzmoci"] = 50,
        ["niszczy"] = 55,
        ["NISZCZY"] = 60,
        ["DRUZGOCZE"] = 67,
        ["ROZPRUWA"] = 75,
        ["ROZRYWA"] = 84,
        ["ROZBEBESZA"] = 100,
        ["DEKAPITUJE"] = 115,
        ["EKSTYRPUJE"] = 130,
        ["ANIHILUJE"] = 145,
        ["USMIERCA"] = 200,
        ["UÅMIERCA"] = 200,
        ["UNICESTWIA"] = 201,
    };

    private static readonly Regex SelfVerbPattern = BuildPattern(SelfVerbValues.Keys);
    private static readonly Regex TechniqueVerbPattern = BuildPattern(TechniqueVerbValues.Keys);
    private static readonly Regex OwnTechniquePattern = new(
        @"\bTwoj\w*\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static Regex BuildPattern(IEnumerable<string> phrases)
    {
        var alternation = string.Join(
            '|', phrases.OrderByDescending(phrase => phrase.Length).Select(Regex.Escape));
        return new Regex($@"\b(?:{alternation})\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    /// <summary>Finds a recognized "you dealt damage" phrase in <paramref name="line"/> (ANSI
    /// escape codes are stripped before matching) and returns its numeric tier.</summary>
    public static bool TryGetDamage(string line, out int damage)
    {
        var plain = AnsiText.StripAnsi(line);

        var selfMatch = SelfVerbPattern.Match(plain);
        if (selfMatch.Success)
        {
            damage = SelfVerbValues[selfMatch.Value];
            return true;
        }

        if (OwnTechniquePattern.IsMatch(plain))
        {
            var techniqueMatch = TechniqueVerbPattern.Match(plain);
            if (techniqueMatch.Success)
            {
                damage = TechniqueVerbValues[techniqueMatch.Value];
                return true;
            }
        }

        damage = 0;
        return false;
    }
}
