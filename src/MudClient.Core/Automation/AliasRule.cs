using System.Text.RegularExpressions;

namespace MudClient.Core.Automation;

public sealed class AliasRule
{
    public AliasRule(string name, string pattern, string replacement, bool enabled = true)
    {
        Name = name;
        Pattern = pattern;
        Replacement = replacement;
        Enabled = enabled;
        Regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    public string Name { get; }

    public string Pattern { get; }

    public string Replacement { get; }

    public bool Enabled { get; set; }

    internal Regex Regex { get; }
}
