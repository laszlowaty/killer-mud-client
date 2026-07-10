namespace MudClient.App.Models;

/// <summary>
/// A person (NPC or player) visible in the current room.
/// </summary>
public sealed record PersonEntry(string Name, string Flags, bool IsPlayer);
