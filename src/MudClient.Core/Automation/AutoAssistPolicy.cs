using MudClient.Core.Gmcp;

namespace MudClient.Core.Automation;

/// <summary>
/// Detects the transition into a state where another group member is fighting
/// in the player's current room. Repeated GMCP updates do not retrigger it.
/// </summary>
public sealed class AutoAssistPolicy
{
    private readonly object _sync = new();
    private bool _assistRequested;

    public bool ShouldAssist(
        bool enabled,
        string? currentRoom,
        string? selfName,
        bool selfIsFighting,
        CharacterGroupUpdate? group,
        IReadOnlyList<RoomPerson> people)
    {
        lock (_sync)
        {
            var shouldAssist = enabled
                && !selfIsFighting
                && !string.IsNullOrWhiteSpace(currentRoom)
                && group is not null
                && HasFightingMemberInRoom(currentRoom, selfName, group, people);

            if (!shouldAssist)
            {
                _assistRequested = false;
                return false;
            }

            if (_assistRequested)
            {
                return false;
            }

            _assistRequested = true;
            return true;
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            _assistRequested = false;
        }
    }

    private static bool HasFightingMemberInRoom(
        string currentRoom,
        string? selfName,
        CharacterGroupUpdate group,
        IReadOnlyList<RoomPerson> people)
    {
        foreach (var member in group.Members)
        {
            if (string.Equals(member.Name, selfName, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(member.Room?.Trim(), currentRoom.Trim(), StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(member.Position, "fighting", StringComparison.OrdinalIgnoreCase)
                || people.Any(person =>
                    person.IsFighting
                    && string.Equals(person.Name, member.Name, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
