using System.Text.RegularExpressions;

namespace MudClient.Core.Automation;

public sealed class TriggerEngine
{
    private static readonly Regex AliasCallRegex = new(
        @"^\s*alias\((.*)\)\s*$", RegexOptions.Compiled | RegexOptions.Singleline);

    private readonly List<TriggerRule> _rules = [];

    /// <summary>
    /// When set, a trigger command matching <c>alias(regexAliasa)</c> is not
    /// sent verbatim. Instead the text inside the parentheses is run through
    /// this <see cref="AliasEngine"/>, and whatever the matching alias
    /// expands to is emitted in its place. Lets a trigger invoke an existing
    /// alias instead of duplicating its command template.
    /// </summary>
    public AliasEngine? Aliases { get; set; }

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
                foreach (var command in CommandStacker.Split(text, separator))
                {
                    commands.AddRange(ExpandAliasCall(command, separator));
                }
            }
        }

        return commands;
    }

    private IReadOnlyList<string> ExpandAliasCall(string command, string? separator)
    {
        var aliasMatch = AliasCallRegex.Match(command);
        if (!aliasMatch.Success || Aliases is null)
        {
            return [command];
        }

        return Aliases.ProcessCommands(aliasMatch.Groups[1].Value, separator);
    }
}
