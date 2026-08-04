namespace MudClient.App.Models;

/// <summary>
/// Application-wide (not per-profile) settings, stored in %AppData%\KillerMudClient\settings.json.
/// </summary>
public sealed class AppSettings
{
    public const string DefaultOutputFontFamily = "Consolas";
    public const double DefaultOutputFontSize = 14;
    public const double MinOutputFontSize = 9;
    public const double MaxOutputFontSize = 28;
    public const string DefaultWidgetFontFamily = "Inter";
    public const double DefaultWidgetFontSize = 13;
    public const double MinWidgetFontSize = 9;
    public const double MaxWidgetFontSize = 24;
    public const string DefaultTelnetColorScheme = "Ciepłe";
    public const string DefaultFloatingButtonSetName = "Domyślny";
    public const double DefaultMobileControlsOpacity = 0.76;
    public const double MinMobileControlsOpacity = 0.25;
    public const double MaxMobileControlsOpacity = 1.0;
    public const double DefaultMobileFloatingButtonScale = 1.0;
    public const double DefaultMobileMovementButtonScale = 1.0;
    public const double MinMobileButtonScale = 0.7;
    public const double MaxMobileButtonScale = 1.5;

    /// <summary>Default for <see cref="CommandStackingSeparator"/>.</summary>
    public const string DefaultCommandStackingSeparator = ";";

    /// <summary>Font used for text received from the MUD in the main output view.</summary>
    public string OutputFontFamily { get; set; } = DefaultOutputFontFamily;

    public double OutputFontSize { get; set; } = DefaultOutputFontSize;

    public bool OutputFontBold { get; set; }

    /// <summary>Font shared by all dockable widgets except the terminal.</summary>
    public string WidgetFontFamily { get; set; } = DefaultWidgetFontFamily;

    public double WidgetFontSize { get; set; } = DefaultWidgetFontSize;

    public bool WidgetFontBold { get; set; }

    /// <summary>Wraps long MUD output lines to the terminal width.</summary>
    public bool OutputWordWrap { get; set; } = true;

    /// <summary>Adds numeric ranges to KillerMUD's descriptive character-stat lines.</summary>
    public bool ShowNumericCharacterStatRanges { get; set; } = true;

    /// <summary>Adds numeric tiers to KillerMUD combat-damage phrases.</summary>
    public bool ShowNumericCombatDamage { get; set; } = true;

    /// <summary>Shows the vertical HP and MV indicators beside the terminal.</summary>
    public bool ShowTerminalVitalsBars { get; set; } = true;

    /// <summary>Clears the terminal command input after a manually submitted command.</summary>
    public bool ClearCommandInputAfterSend { get; set; }

    /// <summary>Palette used for the standard 16 ANSI colors (including indices 0-15).</summary>
    public string TelnetColorScheme { get; set; } = DefaultTelnetColorScheme;

    /// <summary>
    /// Separator character used for command stacking (e.g. ";").
    /// Multiple commands in one text value are split on newlines and on this
    /// separator.  Set to empty to disable stacking (only newlines remain).
    /// Applied to typed commands, alias replacements, trigger actions, and
    /// timer commands.
    /// </summary>
    public string CommandStackingSeparator { get; set; } = DefaultCommandStackingSeparator;

    /// <summary>Automatically sends "as" when a group member fights in the current room.</summary>
    public bool AutoAssistEnabled { get; set; }

    /// <summary>Exact GMCP enemy names for which autoassist must not send "as".</summary>
    public List<string> AutoAssistExcludedMobNames { get; set; } = [];

    /// <summary>Commands sent immediately after an automatic "as" command.</summary>
    public string AutoAssistFollowUpCommands { get; set; } = string.Empty;

    /// <summary>Lets autowalk cast a memorized refresh instead of resting at low movement.</summary>
    public bool AutowalkUseRefreshes { get; set; }

    /// <summary>Sends recuperate immediately after autowalk starts resting at low movement.</summary>
    public bool AutowalkUseRecuperate { get; set; }

    /// <summary>Executes strictly formatted orders issued by current GMCP group members.</summary>
    public bool GroupOrdersEnabled { get; set; }

    /// <summary>Uses stable group-order numbers instead of member names on map markers.</summary>
    public bool ShowGroupMembersAsNumbers { get; set; }

    /// <summary>Enables creator-only map actions backed by server-side lord commands.</summary>
    public bool LordModeEnabled { get; set; } = false;

    /// <summary>
    /// Legacy mirror of the active Android button set. Kept so older Android
    /// releases can still read buttons exported by a newer client.
    /// </summary>
    public List<FloatingButtonDefinition> FloatingButtons { get; set; } = [];

    /// <summary>Named groups of custom command buttons shown over the Android terminal.</summary>
    public List<FloatingButtonSetDefinition> FloatingButtonSets { get; set; } = [];

    /// <summary>Id of the Android button set currently displayed over the terminal.</summary>
    public string ActiveFloatingButtonSetId { get; set; } = string.Empty;

    /// <summary>Shared opacity of the Android movement pad and floating buttons.</summary>
    public double MobileControlsOpacity { get; set; } = DefaultMobileControlsOpacity;

    /// <summary>Scale of custom floating command buttons in the Android UI.</summary>
    public double MobileFloatingButtonScale { get; set; } = DefaultMobileFloatingButtonScale;

    /// <summary>Scale of the movement pad buttons in the Android UI.</summary>
    public double MobileMovementButtonScale { get; set; } = DefaultMobileMovementButtonScale;
}

public sealed class FloatingButtonSetDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public List<FloatingButtonDefinition> Buttons { get; set; } = [];
}

public sealed class FloatingButtonDefinition
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    /// <summary>Horizontal position in the available viewport, from 0 to 1.</summary>
    public double X { get; set; } = 0.5;

    /// <summary>Vertical position in the available viewport, from 0 to 1.</summary>
    public double Y { get; set; } = 0.55;
}
