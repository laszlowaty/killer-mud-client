namespace MudClient.App.Models;

/// <summary>
/// Persisted per-character configuration: notes, aliases, triggers and timers.
/// </summary>
public sealed class ProfileData
{
    public string Name { get; set; } = string.Empty;

    public List<ProfileNote> Notes { get; set; } = [];

    public List<ProfileRule> Rules { get; set; } = [];

    public List<ProfileTimer> Timers { get; set; } = [];

    public List<ProfileLocation> Locations { get; set; } = [];
}

/// <summary>A named autowalk target room stored per character.</summary>
public sealed class ProfileLocation
{
    public string Name { get; set; } = string.Empty;

    public string Vnum { get; set; } = string.Empty;
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

    public bool IsEnabled { get; set; }
}

public sealed class ProfileNote
{
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string CreatedAt { get; set; } = string.Empty;
}

public sealed class ProfileRule
{
    public string Name { get; set; } = string.Empty;

    /// <summary>"alias", "trigger" or "timer".</summary>
    public string Type { get; set; } = "alias";

    public string Pattern { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public bool IsEnabled { get; set; } = true;
}
