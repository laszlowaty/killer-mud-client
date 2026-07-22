namespace MudClient.App.Models;

/// <summary>
/// Persisted per-character configuration: notes, aliases, triggers and timers.
/// </summary>
public sealed class ProfileData
{
    /// <summary>Local account label used in the picker and as the profile file name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>MUD login sent to the server; empty in legacy files means <see cref="Name"/>.</summary>
    public string Login { get; set; } = string.Empty;

    /// <summary>Server address used by this account.</summary>
    public string Host { get; set; } = "killer-mud.pl";

    /// <summary>Server port used by this account.</summary>
    public int Port { get; set; } = 4004;

    /// <summary>
    /// Text encoding used to talk to this account's server (see <see cref="Core.Networking.MudTextEncodings"/>).
    /// Defaults to auto-detection; empty/legacy files also fall back to auto-detect.
    /// </summary>
    public string Encoding { get; set; } = Core.Networking.MudTextEncodings.Auto;

    /// <summary>Account password encrypted with DPAPI (base64); empty = no password stored.</summary>
    public string EncryptedPassword { get; set; } = string.Empty;

    /// <summary>
    /// True for a freshly created account that has never been registered on the
    /// MUD. On the first connection the client sends the character-creation
    /// sequence (login, "t", password, password, space) instead of a plain login;
    /// the flag is cleared afterwards so later logins send only name + password.
    /// </summary>
    public bool NeedsRegistration { get; set; }

    public List<ProfileNote> Notes { get; set; } = [];

    public List<ProfileRule> Rules { get; set; } = [];

    public List<ProfileTimer> Timers { get; set; } = [];

    public List<ProfileLocation> Locations { get; set; } = [];

    /// <summary>Grouping folders (per kind) owned by this profile.</summary>
    public List<ProfileFolder> Folders { get; set; } = [];

    /// <summary>Last 10 death locations, newest first.</summary>
    public List<ProfileDeath> Deaths { get; set; } = [];

    /// <summary>Buff names the character wants to keep active (see BuffWatchEntry).</summary>
    public List<string> RequiredBuffs { get; set; } = [];
}

/// <summary>
/// Rules, timers and autowalk locations marked as global — shared by all
/// profiles and stored in a single file next to the per-profile ones.
/// </summary>
public sealed class GlobalData
{
    public List<ProfileNote> Notes { get; set; } = [];

    public List<ProfileRule> Rules { get; set; } = [];

    public List<ProfileTimer> Timers { get; set; } = [];

    public List<ProfileLocation> Locations { get; set; } = [];

    /// <summary>Grouping folders (per kind) shared by all profiles.</summary>
    public List<ProfileFolder> Folders { get; set; } = [];
}

/// <summary>
/// A grouping folder persisted per character or in the shared global file.
/// Folders form a tree via <see cref="ParentId"/> and group items of a single
/// <see cref="Kind"/>.
/// </summary>
public sealed class ProfileFolder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Parent folder id, or null for a root folder.</summary>
    public string? ParentId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Domain this folder belongs to (Timers/Aliases/Triggers/Notes/Autowalk).</summary>
    public FolderKind Kind { get; set; }

    /// <summary>True when stored in the shared global file, not a profile.</summary>
    public bool IsGlobal { get; set; }
}

/// <summary>A named autowalk target room stored per character.</summary>
public sealed class ProfileLocation
{
    public string Name { get; set; } = string.Empty;

    public string Vnum { get; set; } = string.Empty;

    /// <summary>True when stored in the shared global file, not a profile.</summary>
    public bool IsGlobal { get; set; }

    /// <summary>Id of the containing folder, or null when loose.</summary>
    public string? FolderId { get; set; }
}

/// <summary>A death location stored per character (newest first, max 10).</summary>
public sealed class ProfileDeath
{
    public string Vnum { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;

    public string When { get; set; } = string.Empty;
}

/// <summary>
/// A repeating timer stored per character. Fires every
/// Minutes/Seconds/Milliseconds and sends Commands in order until disabled.
/// </summary>
public sealed class ProfileTimer
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public int Minutes { get; set; }

    public int Seconds { get; set; }

    public int Milliseconds { get; set; }

    /// <summary>Commands sent in this order on every tick.</summary>
    public List<string> Commands { get; set; } = [];

    /// <summary>
    /// Raw command text preserving the original user input (e.g. "look;exa").
    /// When non-empty, <see cref="MakeTimerEntry"/> uses this instead of
    /// joining <see cref="Commands"/> with newlines, so the user's chosen
    /// separator characters are not lost across save/load cycles.
    /// </summary>
    public string CommandsText { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }

    /// <summary>True when stored in the shared global file, not a profile.</summary>
    public bool IsGlobal { get; set; }

    /// <summary>Id of the containing folder, or null when loose.</summary>
    public string? FolderId { get; set; }
}

public sealed class ProfileNote
{
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string CreatedAt { get; set; } = string.Empty;

    /// <summary>True when stored in the shared global file, not a profile.</summary>
    public bool IsGlobal { get; set; }

    /// <summary>Id of the containing folder, or null when loose.</summary>
    public string? FolderId { get; set; }
}

public sealed class ProfileRule
{
    public string Name { get; set; } = string.Empty;

    /// <summary>"alias", "trigger" or "timer".</summary>
    public string Type { get; set; } = "alias";

    public string Pattern { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;

    /// <summary>True when stored in the shared global file, not a profile.</summary>
    public bool IsGlobal { get; set; }

    /// <summary>Id of the containing folder, or null when loose.</summary>
    public string? FolderId { get; set; }
}
