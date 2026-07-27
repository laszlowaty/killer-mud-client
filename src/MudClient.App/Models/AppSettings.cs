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

    /// <summary>Default/limits for the terminal overlay's transparency and geometry (see
    /// <see cref="TerminalOverlayOpacity"/> and the fraction properties below).</summary>
    public const double DefaultTerminalOverlayOpacity = 0.85;
    public const double MinTerminalOverlayOpacity = 0.2;
    public const double MaxTerminalOverlayOpacity = 1.0;
    public const double MinTerminalOverlaySizeFraction = 0.15;
    public const double MaxTerminalOverlaySizeFraction = 0.95;
    public const double DefaultTerminalOverlayXFraction = 0.55;
    public const double DefaultTerminalOverlayYFraction = 0.05;
    public const double DefaultTerminalOverlayWidthFraction = 0.42;
    public const double DefaultTerminalOverlayHeightFraction = 0.42;

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

    /// <summary>Executes strictly formatted orders issued by current GMCP group members.</summary>
    public bool GroupOrdersEnabled { get; set; }

    /// <summary>Uses stable group-order numbers instead of member names on map markers.</summary>
    public bool ShowGroupMembersAsNumbers { get; set; }

    /// <summary>Enables creator-only map actions backed by server-side lord commands.</summary>
    public bool LordModeEnabled { get; set; }

    /// <summary>Id of the panel currently pinned as a floating overlay on the Terminal, or null
    /// if no panel is overlaid. Independent of the per-layout dock snapshots (see
    /// <see cref="MudClient.App.Docking.MudDockFactory.OverlayTool"/>).</summary>
    public string? TerminalOverlayPanelId { get; set; }

    /// <summary>Overlay position/size as fractions (0..1) of the Terminal panel's own bounds, so
    /// the overlay always stays proportionally inside the terminal regardless of window size.</summary>
    public double TerminalOverlayXFraction { get; set; } = DefaultTerminalOverlayXFraction;

    public double TerminalOverlayYFraction { get; set; } = DefaultTerminalOverlayYFraction;

    public double TerminalOverlayWidthFraction { get; set; } = DefaultTerminalOverlayWidthFraction;

    public double TerminalOverlayHeightFraction { get; set; } = DefaultTerminalOverlayHeightFraction;

    /// <summary>0 (fully transparent) .. 1 (opaque). Lets the terminal text show through the overlay.</summary>
    public double TerminalOverlayOpacity { get; set; } = DefaultTerminalOverlayOpacity;
}
