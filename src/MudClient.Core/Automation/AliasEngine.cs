namespace MudClient.Core.Automation;

public sealed class AliasEngine
{
    private readonly List<AliasRule> _rules = [];

    public IReadOnlyList<AliasRule> Rules => _rules;

    public void Add(AliasRule rule) => _rules.Add(rule);

    public void Clear() => _rules.Clear();

    public bool Remove(string name)
    {
        var index = _rules.FindIndex(rule =>
            string.Equals(rule.Name, name, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            return false;
        }

        _rules.RemoveAt(index);
        return true;
    }

    public string Process(string command)
    {
        foreach (var rule in _rules)
        {
            if (!rule.Enabled)
            {
                continue;
            }

            var match = rule.Regex.Match(command);
            if (match.Success)
            {
                return match.Result(rule.Replacement);
            }
        }

        return command;
    }

    /// <summary>
    /// Like <see cref="Process"/> but splits the replacement result into
    /// multiple commands (one per line).  Empty/whitespace lines are skipped.
    /// </summary>
    public IReadOnlyList<string> ProcessCommands(string command)
    {
        foreach (var rule in _rules)
        {
            if (!rule.Enabled)
            {
                continue;
            }

            var match = rule.Regex.Match(command);
            if (match.Success)
            {
                var text = match.Result(rule.Replacement);
                return SplitCommands(text);
            }
        }

        return new[] { command };
    }

    private static IReadOnlyList<string> SplitCommands(string text) =>
        (text ?? string.Empty)
            .Split('\n')
            .Select(line => line.Trim().TrimEnd('\r'))
            .Where(line => line.Length > 0)
            .ToList();
}
