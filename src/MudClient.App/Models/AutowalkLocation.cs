namespace MudClient.App.Models;

/// <summary>A named target room for autowalk, stored per profile.</summary>
public sealed class AutowalkLocation
{
    public AutowalkLocation(string name, string vnum, string? roomName = null)
    {
        Name = name;
        Vnum = vnum;
        RoomName = roomName;
    }

    /// <summary>User-chosen label, e.g. "plac-arras"; used by the /idz command.</summary>
    public string Name { get; }

    /// <summary>Room vnum as reported by GMCP and stored in the map's userData.</summary>
    public string Vnum { get; }

    /// <summary>Resolved map room name, if the vnum exists in the loaded map.</summary>
    public string? RoomName { get; }

    public string Display => string.IsNullOrWhiteSpace(RoomName)
        ? $"vnum {Vnum}"
        : $"{RoomName} (vnum {Vnum})";
}
