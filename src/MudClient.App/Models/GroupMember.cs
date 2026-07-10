namespace MudClient.App.Models;

/// <summary>
/// A member of the player's group.
/// </summary>
public sealed record GroupMember(string Name, string LeaderMarker, int HpPercent, int SpPercent, int EpPercent);
