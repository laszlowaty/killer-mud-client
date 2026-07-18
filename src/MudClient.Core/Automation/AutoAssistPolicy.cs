using MudClient.Core.Gmcp;

namespace MudClient.Core.Automation;

/// <summary>
/// Detects the transition into a state where another group member is fighting
/// in the player's current room. Members fighting an explicitly excluded enemy
/// do not qualify. Repeated GMCP updates do not retrigger it.
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
        IReadOnlyList<RoomPerson> people,
        IReadOnlyCollection<string> excludedEnemyNames)
    {
        lock (_sync)
        {
            var shouldAssist = enabled
                && !selfIsFighting
                && !string.IsNullOrWhiteSpace(currentRoom)
                && group is not null
                && HasFightingMemberInRoom(
                    currentRoom,
                    selfName,
                    group,
                    people,
                    excludedEnemyNames);

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
        IReadOnlyList<RoomPerson> people,
        IReadOnlyCollection<string> excludedEnemyNames)
    {
        foreach (var member in group.Members)
        {
            if (string.Equals(member.Name, selfName, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(member.Room?.Trim(), currentRoom.Trim(), StringComparison.Ordinal))
            {
                continue;
            }

            var roomPerson = people.FirstOrDefault(person =>
                string.Equals(person.Name, member.Name, StringComparison.OrdinalIgnoreCase));
            var isFighting = string.Equals(
                                 member.Position,
                                 "fighting",
                                 StringComparison.OrdinalIgnoreCase)
                             || roomPerson?.IsFighting == true;

            if (!isFighting)
            {
                continue;
            }

            // Char.Group can report "fighting" before Room.People delivers the enemy.
            // With exclusions configured, wait for that precise association instead of
            // sending "as" early and learning only afterwards that the mob was excluded.
            if (excludedEnemyNames.Count > 0
                && (roomPerson?.IsFighting != true
                    || string.IsNullOrWhiteSpace(roomPerson.Enemy)))
            {
                continue;
            }

            if (roomPerson?.Enemy is { } enemy
                && excludedEnemyNames.Any(excluded =>
                    string.Equals(
                        excluded.Trim(),
                        enemy.Trim(),
                        StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            return true;
        }

        return false;
    }
}
