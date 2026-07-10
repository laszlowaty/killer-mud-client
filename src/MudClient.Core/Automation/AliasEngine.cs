namespace MudClient.Core.Automation;

public sealed class AliasEngine
{
    private readonly List<AliasRule> _rules = [];

    public IReadOnlyList<AliasRule> Rules => _rules;

    public void Add(AliasRule rule) => _rules.Add(rule);

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
}
