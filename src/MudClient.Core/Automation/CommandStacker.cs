namespace MudClient.Core.Automation;

/// <summary>
/// Splits command text into individual commands.  Each command may be separated
/// by a newline (\n) and, when a stacking separator is configured, also by that
/// separator character.  Whitespace-only items are discarded.
/// </summary>
public static class CommandStacker
{
    /// <summary>Default separator used when none is explicitly configured.</summary>
    public const string DefaultSeparator = ";";

    /// <summary>
    /// Splits <paramref name="text"/> on newlines and, when
    /// <paramref name="separator"/> is non-empty, on the separator character
    /// as well.  Each resulting item is trimmed of whitespace and trailing
    /// carriage-return characters; empty results are skipped.
    /// </summary>
    /// <param name="text">The raw text, e.g. from an alias replacement or timer command list.</param>
    /// <param name="separator">
    /// Configurable separator (e.g. ";").  Pass null or empty to only split on
    /// newlines (stacking disabled except for newlines).
    /// </param>
    /// <returns>Non-null, possibly empty list of non-empty, trimmed command strings.</returns>
    public static IReadOnlyList<string> Split(string? text, string? separator = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<string>();
        }

        var effective = !string.IsNullOrWhiteSpace(separator) ? separator : null;

        // If there is no stacking separator, just split on newlines.
        if (effective is null)
        {
            return text
                .Split('\n')
                .Select(line => line.Trim().TrimEnd('\r'))
                .Where(line => line.Length > 0)
                .ToList();
        }

        // First split on newlines, then split each line on the separator.
        // This preserves the existing newline behaviour while adding stacking.
        var results = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            var trimmed = line.Trim().TrimEnd('\r');
            if (trimmed.Length == 0)
            {
                continue;
            }

            foreach (var part in trimmed.Split(effective))
            {
                var cmd = part.Trim();
                if (cmd.Length > 0)
                {
                    results.Add(cmd);
                }
            }
        }

        return results;
    }
}
