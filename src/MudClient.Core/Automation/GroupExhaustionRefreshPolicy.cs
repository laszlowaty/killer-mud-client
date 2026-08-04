using MudClient.Core.Gmcp;

namespace MudClient.Core.Automation;

/// <summary>
/// Detects when a non-NPC group member's movement drops to the worst GMCP tier ("zamęczony",
/// <see cref="CharacterGroupMember.MvScale"/> 0) and hasn't already been ordered to refresh. Fires
/// once per exhaustion rather than on every GMCP group update; a member re-arms once they recover
/// (a higher MvScale) or leave the group, so a later exhaustion triggers again.
/// </summary>
public sealed class GroupExhaustionRefreshPolicy
{
    private readonly object _sync = new();
    private readonly HashSet<string> _ordered = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Returns the names of members that should now be ordered to refresh.</summary>
    public IReadOnlyList<string> GetMembersToOrder(bool enabled, CharacterGroupUpdate? group, string? selfName)
    {
        lock (_sync)
        {
            if (!enabled || group is null)
            {
                _ordered.Clear();
                return [];
            }

            var toOrder = new List<string>();
            var current = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var member in group.Members)
            {
                if (member.IsNpc
                    || string.Equals(member.Name, selfName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                current.Add(member.Name);

                if (member.MvScale == 0)
                {
                    if (_ordered.Add(member.Name))
                    {
                        toOrder.Add(member.Name);
                    }
                }
                else
                {
                    _ordered.Remove(member.Name);
                }
            }

            _ordered.RemoveWhere(name => !current.Contains(name));
            return toOrder;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _ordered.Clear();
        }
    }
}
