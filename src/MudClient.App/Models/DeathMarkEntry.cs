namespace MudClient.App.Models;

/// <summary>
/// A recorded death location (room vnum + resolved name + timestamp),
/// stored per profile. Only the last 10 deaths are kept.
/// </summary>
public sealed class DeathMarkEntry
{
    public DeathMarkEntry(string vnum, string? roomName, string when)
    {
        Vnum = vnum;
        RoomName = roomName;
        When = when;
    }

    /// <summary>Room vnum as reported by GMCP at the moment of death.</summary>
    public string Vnum { get; }

    /// <summary>Resolved map room name, if the vnum exists in the loaded map.</summary>
    public string? RoomName { get; }

    /// <summary>Local timestamp of the death, e.g. "2026-07-11 21:37".</summary>
    public string When { get; }

    public string Display => string.IsNullOrWhiteSpace(RoomName)
        ? $"vnum {Vnum}"
        : $"{RoomName} (vnum {Vnum})";
}
