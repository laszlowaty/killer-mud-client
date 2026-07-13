using CommunityToolkit.Mvvm.ComponentModel;

namespace MudClient.App.Models;

/// <summary>A named target room for autowalk, stored per profile.</summary>
public sealed class AutowalkLocation : ObservableObject, IFolderItem
{
    private bool _isGlobal;
    private string? _folderId;

    public AutowalkLocation(string name, string vnum, string? roomName = null, bool isGlobal = false)
    {
        Name = name;
        Vnum = vnum;
        RoomName = roomName;
        _isGlobal = isGlobal;
    }

    public string Id { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>User-chosen label, e.g. "plac-arras"; used by the /idz command.</summary>
    public string Name { get; }

    /// <summary>Room vnum as reported by GMCP and stored in the map's userData.</summary>
    public string Vnum { get; }

    /// <summary>Resolved map room name, if the vnum exists in the loaded map.</summary>
    public string? RoomName { get; }

    /// <summary>True = shared by all profiles (stored in the global file).</summary>
    public bool IsGlobal
    {
        get => _isGlobal;
        set => SetProperty(ref _isGlobal, value);
    }

    /// <summary>Id of the containing folder, or null when loose.</summary>
    public string? FolderId
    {
        get => _folderId;
        set => SetProperty(ref _folderId, value);
    }

    public string Display => string.IsNullOrWhiteSpace(RoomName)
        ? $"vnum {Vnum}"
        : $"{RoomName} (vnum {Vnum})";
}
