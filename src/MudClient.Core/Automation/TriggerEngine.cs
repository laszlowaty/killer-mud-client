namespace MudClient.Core.Automation;

public sealed class TriggerEngine
{
    private readonly List<TriggerRule> _rules = [];

    public IReadOnlyList<TriggerRule> Rules => _rules;

    public void Add(TriggerRule rule) => _rules.Add(rule);

    public IReadOnlyList<string> Evaluate(string line)
    {
        var commands = new List<string>();

        foreach (var rule in _rules)
        {
            if (!rule.Enabled)
            {
                continue;
            }

            var match = rule.Regex.Match(line);
            if (match.Success)
            {
                commands.Add(match.Result(rule.CommandTemplate));
            }
        }

        return commands;
    }
}
