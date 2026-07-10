namespace MudClient.App.Models;

/// <summary>
/// A person (NPC or player) visible in the current room, from Room.People GMCP.
/// </summary>
public sealed record PersonEntry(string Name, bool IsFighting, string? Enemy)
{
    public string FightingText =>
        Enemy is null ? "⚔ walczy" : $"⚔ walczy z: {Enemy}";
}
