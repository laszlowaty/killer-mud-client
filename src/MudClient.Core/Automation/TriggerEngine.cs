namespace MudClient.Core.Automation;

public sealed class TriggerEngine
{
    private readonly List<TriggerRule> _rules = [];

    public IReadOnlyList<TriggerRule> Rules => _rules;

    public void Add(TriggerRule rule) => _rules.Add(rule);

    public void Clear() => _rules.Clear();

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
                var text = match.Result(rule.CommandTemplate);
                commands.AddRange(SplitCommands(text));
            }
        }

        return commands;
    }

    private static IReadOnlyList<string> SplitCommands(string text) =>
        (text ?? string.Empty)
            .Split('\n')
            .Select(line => line.Trim().TrimEnd('\r'))
            .Where(line => line.Length > 0)
            .ToList();
}
