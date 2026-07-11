namespace MudClient.Core.Automation;

public sealed class TriggerEngine
{
    private readonly List<TriggerRule> _rules = [];

    public IReadOnlyList<TriggerRule> Rules => _rules;

    public void Add(TriggerRule rule) => _rules.Add(rule);

    public void Clear() => _rules.Clear();

    /// <summary>
    /// Evaluates all enabled trigger rules against <paramref name="line"/>.
    /// Equivalent to <c>Evaluate(line, null)</c>.
    /// </summary>
    public IReadOnlyList<string> Evaluate(string line) =>
        Evaluate(line, separator: null);

    /// <summary>
    /// Evaluates all enabled trigger rules against <paramref name="line"/>,
    /// also splitting the command template on <paramref name="separator"/>
    /// when it is non-empty (in addition to newlines).
    /// </summary>
    public IReadOnlyList<string> Evaluate(string line, string? separator)
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
                commands.AddRange(CommandStacker.Split(text, separator));
            }
        }

        return commands;
    }
}
