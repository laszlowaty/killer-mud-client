using System.Text.RegularExpressions;

namespace MudClient.Core.Text;

/// <summary>Strips ANSI/VT100 escape sequences from MUD text so it can be pattern-matched as
/// plain text (colors are still delivered to the UI separately for display).</summary>
public static partial class AnsiText
{
    public static string StripAnsi(string value) => AnsiRegex().Replace(value, string.Empty);

    [GeneratedRegex("\\x1B\\[[0-?]*[ -/]*[@-~]", RegexOptions.CultureInvariant)]
    private static partial Regex AnsiRegex();
}
