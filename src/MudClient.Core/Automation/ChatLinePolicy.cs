using System.Text.RegularExpressions;
using MudClient.Core.Text;

namespace MudClient.Core.Automation;

/// <summary>
/// Recognizes lines that carry player communication — say, sayto, tell, clantell, grouptell,
/// yell, and shout — so they can be mirrored into a dedicated chat panel independent of the
/// trigger system. The pattern (including the "race speech verb" variants like nuci/miauczy/
/// szczeka for say, and the generic "[Channel]: text" form covering tell/clantell/grouptell) is
/// ported from the community Mudlet package's kchat module for this MUD, which has matched real
/// server output for these message types.
/// </summary>
public static partial class ChatLinePolicy
{
    public static bool IsCommunicationLine(string line) => ChatRegex().IsMatch(AnsiText.StripAnsi(line));

    [GeneratedRegex(
        "^((\\w+) (m[oó]wi|nuci|dudni|grzmi|piszczy|warczy|miauczy|szczeka|ryczy|syczy|[sś]piewa|zawodzi|wydaje d[zź]wi[ęe]k|pieje|skrzeczy).*'(.+)'" +
        "|(\\w+) (pyta|nuci|dudni|piszczy|warczy|miauczy|szczeka|ryczy|syczy|[sś]piewa|pieje|skrzeczy).*'(.+)'" +
        "|()(M[oó]wisz|Nucisz|Dudnisz|Grzmisz|Piszczysz|Warczysz|Miauczysz|Szczekasz|Ryczysz|Syczysz|[ŚS]piewasz|Zawodzisz|Wydajesz d[zź]wi[eę]k|Piejesz).*'(.+)'" +
        "|()Pytasz.*'(.+)'" +
        "|()Wykrzykujesz.*'(.+)'" +
        "|()Krzyczysz '(.+)'" +
        "|()Wrzeszczysz '(.+)'" +
        "|(\\w+) wrzeszczy '(.+)'" +
        "|(\\w+) krzyczy.*'(.+)'" +
        "|(\\w+) wykrzykuje.*'(.+)'" +
        "|\\[(\\w+)\\]:\\s(.+))$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ChatRegex();
}
