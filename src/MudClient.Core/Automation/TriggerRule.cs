using System.Text.RegularExpressions;

namespace MudClient.Core.Automation;

public sealed class TriggerRule
{
    public TriggerRule(string name, string pattern, string commandTemplate, bool enabled = true)
    {
        Name = name;
        Pattern = pattern;
        CommandTemplate = commandTemplate;
        Enabled = enabled;
        Regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    public string Name { get; }

    public string Pattern { get; }

    public string CommandTemplate { get; }

    public bool Enabled { get; set; }

    internal Regex Regex { get; }
}
