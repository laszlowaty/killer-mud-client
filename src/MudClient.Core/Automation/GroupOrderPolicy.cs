using System.Text.RegularExpressions;
using MudClient.Core.Gmcp;

namespace MudClient.Core.Automation;

/// <summary>Validates commands ordered by a member of the current GMCP group.</summary>
public static class GroupOrderPolicy
{
    private static readonly Regex OrderRegex = new(
        "^(?<issuer>[A-Za-z]+) rozkazuje ci '(?<command>[\\w\\s]+)'\\.$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryGetCommand(
        string line,
        string? selfName,
        CharacterGroupUpdate? group,
        out string command)
    {
        command = string.Empty;
        if (group is null)
        {
            return false;
        }

        var match = OrderRegex.Match(line);
        if (!match.Success)
        {
            return false;
        }

        var issuer = match.Groups["issuer"].Value;
        if (string.Equals(issuer, selfName, StringComparison.OrdinalIgnoreCase)
            || !group.Members.Any(member =>
                !member.IsNpc
                && string.Equals(member.Name, issuer, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        command = match.Groups["command"].Value.Trim();
        return command.Length > 0;
    }
}
