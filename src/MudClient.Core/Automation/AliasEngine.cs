using System.Text.RegularExpressions;

namespace MudClient.Core.Automation;

public sealed class AliasEngine
{
    private static readonly Regex AliasCallRegex = new(
        @"^\s*alias\((.*)\)\s*$", RegexOptions.Compiled | RegexOptions.Singleline);

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
    /// Equivalent to <c>ProcessCommands(command, null)</c>.
    /// </summary>
    public IReadOnlyList<string> ProcessCommands(string command) =>
        ProcessCommands(command, separator: null);

    /// <summary>
    /// Like <see cref="ProcessCommands(string)"/> but also splits the
    /// replacement result on <paramref name="separator"/> when it is
    /// non-empty (in addition to newlines).
    /// </summary>
    public IReadOnlyList<string> ProcessCommands(string command, string? separator)
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
                return CommandStacker.Split(text, separator);
            }
        }

        return new[] { command };
    }

    /// <summary>
    /// Expands an explicit <c>alias(...)</c> call, preserving the command
    /// literally when it is not a call or no alias matches it.
    /// </summary>
    public IReadOnlyList<string> ProcessAliasCall(string command, string? separator = null)
    {
        var match = AliasCallRegex.Match(command);
        return match.Success
            ? ProcessCommands(match.Groups[1].Value, separator)
            : [command];
    }
}
