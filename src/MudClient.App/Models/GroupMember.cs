using MudClient.Core.Gmcp;

namespace MudClient.App.Models;

/// <summary>
/// A member of the player's group, populated from Char.Group GMCP.
/// </summary>
public sealed record GroupMember
{
    public string Name { get; }
    public bool IsLeader { get; }
    public string? Position { get; }
    public string HpText { get; }
    public int? HpScale { get; }
    public string MvText { get; }
    public int? MvScale { get; }
    public int? Mem { get; }
    public bool IsNpc { get; }
    public string? Room { get; }

    public string ShortName => Name.Length > 3 ? Name.Substring(0, 3) : Name;

    /// <summary>
    /// Final display string for the room column. Set by the view model after
    /// resolving the raw <see cref="Room"/> vnum through the loaded map index.
    /// Possible values: a map room name, "pokój {vnum}" (fallback), or "?" (no room).
    /// </summary>
    public string RoomDisplay { get; }

    public string LeaderMarker => IsLeader ? "*" : " ";
    public string PositionDisplay => string.IsNullOrWhiteSpace(Position) ? "—" : Position;

    /// <summary>E.g. "żadnych śladów (7/7)" when scale is known, just raw text otherwise.</summary>
    public string HpDisplay =>
        HpScale is { } scale
            ? $"{HpText} ({scale}/7)"
            : HpText;

    /// <summary>E.g. "wypoczęty (4/4)" when scale is known, just raw text otherwise.</summary>
    public string MvDisplay =>
        MvScale is { } scale
            ? $"{MvText} ({scale}/4)"
            : MvText;

    /// <summary>"MEM 1" or empty string when mem is unknown.</summary>
    public string MemDisplay =>
        Mem is { } mem
            ? $"MEM {mem}"
            : string.Empty;

    public string NpcDisplay => IsNpc ? "[NPC]" : "[Gracz]";

    /// <summary>HP scale as a 0-100 value for progress bar display.</summary>
    public double HpPercent => HpScale is { } scale ? scale / 7.0 * 100 : 0;

    /// <summary>MV scale as a 0-100 value for progress bar display.</summary>
    public double MvPercent => MvScale is { } scale ? scale / 4.0 * 100 : 0;

    public GroupMember(
        string name,
        bool isLeader,
        string? position,
        string hpText,
        int? hpScale,
        string mvText,
        int? mvScale,
        int? mem,
        bool isNpc,
        string? room,
        string roomDisplay)
    {
        Name = name;
        IsLeader = isLeader;
        Position = position;
        HpText = hpText;
        HpScale = hpScale;
        MvText = mvText;
        MvScale = mvScale;
        Mem = mem;
        IsNpc = isNpc;
        Room = room;
        RoomDisplay = roomDisplay;
    }

    public static GroupMember FromCore(CharacterGroupMember core, string? roomDisplay = null) =>
        new(
            name: core.Name,
            isLeader: core.IsLeader,
            position: core.Position,
            hpText: core.HpText,
            hpScale: core.HpScale,
            mvText: core.MvText,
            mvScale: core.MvScale,
            mem: core.Mem,
            isNpc: core.IsNpc,
            room: core.Room,
            roomDisplay: roomDisplay ?? core.Room ?? "?");
}
