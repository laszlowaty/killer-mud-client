using System.Text.RegularExpressions;
using MudClient.Core.Text;

namespace MudClient.Core.Combat;

/// <summary>
/// Maps the combat verbs this MUD sends when the local character lands a hit (e.g. "Ranisz",
/// "ranisz") to their approximate numeric damage tier.
///
/// Every tier has both a 2nd-person form ("Ranisz golema mieczem." — you land the hit, ends in
/// "sz"/"SZ") and a 3rd-person form ("Golem rani cię." / "Golem rani Aragorna." — someone or
/// something else is the subject, whether that's a mob hitting you or bystander-visible combat
/// between others). Only the 2nd-person forms are included here: they're the only ones that
/// unambiguously mean "you dealt this damage", which is what the numeric-damage display is for.
/// A couple of encoding-mangled variants (e.g. the "Å" mojibake) are kept too, in case a client
/// encoding misdetection ever produces them — they cost nothing to keep around.
/// </summary>
public static class DamagePhrases
{
    private static readonly IReadOnlyDictionary<string, int> Values = new Dictionary<string, int>
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

    private static readonly Regex PhrasePattern = BuildPattern();

    private static Regex BuildPattern()
    {
        var alternation = string.Join(
            '|', Values.Keys.OrderByDescending(key => key.Length).Select(Regex.Escape));
        return new Regex($@"\b(?:{alternation})\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    /// <summary>Finds the first recognized "you dealt damage" phrase in <paramref name="line"/>
    /// (ANSI escape codes are stripped before matching) and returns its numeric tier.</summary>
    public static bool TryGetDamage(string line, out int damage)
    {
        var match = PhrasePattern.Match(AnsiText.StripAnsi(line));
        if (!match.Success)
        {
            damage = 0;
            return false;
        }

        damage = Values[match.Value];
        return true;
    }
}
