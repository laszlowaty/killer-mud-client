using System.Text.RegularExpressions;

namespace MudClient.Core.Text;

/// <summary>
/// Recognizes complete KillerMUD conversation lines after the Telnet byte stream has
/// been decoded and assembled into lines. The patterns mirror the established kchat
/// module from MudletScripts; ANSI SGR sequences are ignored only for classification.
/// </summary>
public static partial class ChatLineClassifier
{
    public static bool IsChatLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            return false;
        }

        var plainText = AnsiSequenceRegex().Replace(line, string.Empty);
        return ChatLineRegex().IsMatch(plainText);
    }

    [GeneratedRegex(
        @"^((\w+) (m[oó]wi|nuci|dudni|grzmi|piszczy|warczy|miauczy|szczeka|ryczy|syczy|[sś]piewa|zawodzi|wydaje d[zź]wi[ęe]k|pieje|skrzeczy).*'(.+)'|(\w+) (pyta|nuci|dudni|piszczy|warczy|miauczy|szczeka|ryczy|syczy|[sś]piewa|pieje|skrzeczy).*'(.+)'|()(M[oó]wisz|Nucisz|Dudnisz|Grzmisz|Piszczysz|Warczysz|Miauczysz|Szczekasz|Ryczysz|Syczysz|[ŚS]piewasz|Zawodzisz|Wydajesz d[zź]wi[eę]k|Piejesz|[ŚS]piewasz).*'(.+)'|()Pytasz.*'(.+)'|()Wykrzykujesz.*'(.+)'|()Krzyczysz '(.+)'|()Wrzeszczysz '(.+)'|(\w+) wrzeszczy '(.+)'|(\w+) krzyczy.*'(.+)'|(\w+) wykrzykuje.*'(.+)'|\[(\w+)\]:\s(.+))$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ChatLineRegex();

    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*[@-~]", RegexOptions.CultureInvariant)]
    private static partial Regex AnsiSequenceRegex();
}
