using MudClient.App.Controls;

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

    /// <summary>Default/limits for the terminal overlay's shared transparency (see
    /// <see cref="TerminalOverlayOpacity"/>).</summary>
    public const double DefaultTerminalOverlayOpacity = 0.85;
    public const double MinTerminalOverlayOpacity = 0.2;
    public const double MaxTerminalOverlayOpacity = 1.0;

    /// <summary>Default/limits for one overlay column's width in pixels (see
    /// <see cref="TerminalOverlayEntry.ColumnWidth"/>). Columns float on top of the Terminal and
    /// never resize it, so this is a plain pixel size rather than a fraction of anything.</summary>
    public const double DefaultTerminalOverlayColumnWidth = 320;
    public const double MinTerminalOverlayColumnWidth = 200;
    public const double MaxTerminalOverlayColumnWidth = 900;

    /// <summary>Default/limits for one overlay column's overall height, as a fraction (0..1) of
    /// the Terminal's own height (see <see cref="TerminalOverlayEntry.ColumnHeightFraction"/>).
    /// The stack is anchored to the top, so shrinking this reveals terminal below the last
    /// card.</summary>
    public const double DefaultTerminalOverlayColumnHeightFraction = 1.0;
    public const double MinTerminalOverlayColumnHeightFraction = 0.2;
    public const double MaxTerminalOverlayColumnHeightFraction = 1.0;

    /// <summary>Default/limits for one overlay's height relative to the others stacked in the
    /// same column (a Grid star weight — see <see cref="TerminalOverlayEntry.HeightWeight"/>).</summary>
    public const double DefaultTerminalOverlayHeightWeight = 1.0;
    public const double MinTerminalOverlayHeightWeight = 0.2;
    public const double MaxTerminalOverlayHeightWeight = 5.0;

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

    /// <summary>Shows each effect's count/duration and description alongside its name in the
    /// Effects panel, instead of just the name.</summary>
    public bool ShowExtendedEffects { get; set; }

    /// <summary>Last chosen "Tryb mapy" (Proceduralna/Prosta) — restored on the next launch.</summary>
    public MapDisplayMode MapDisplayMode { get; set; } = MapDisplayMode.Procedural;

    /// <summary>Double-clicking a room on the map immediately starts walking there, instead of
    /// only previewing the route until confirmed.</summary>
    public bool AutoWalkOnMapDoubleClick { get; set; } = true;

    /// <summary>Enables autowalk's built-in low-movement recovery (cast refresh if memorized,
    /// otherwise rest then stand back up) before each step. Disabling lets autowalk keep walking
    /// without ever pausing for this — see <see cref="MudClient.Core.Automation.AutowalkRecoveryPolicy"/>.</summary>
    public bool AutowalkMovementRecoveryEnabled { get; set; } = true;

    /// <summary>Sends "rest" automatically as soon as autowalk reaches its destination.</summary>
    public bool AutowalkRestOnArrivalEnabled { get; set; } = true;

    /// <summary>Default/limits for <see cref="AutowalkLowMovementThresholdPercent"/>.</summary>
    public const int DefaultAutowalkLowMovementThresholdPercent = 10;
    public const int MinAutowalkLowMovementThresholdPercent = 1;
    public const int MaxAutowalkLowMovementThresholdPercent = 50;

    /// <summary>Movement percentage (of max) at or below which autowalk triggers recovery.</summary>
    public int AutowalkLowMovementThresholdPercent { get; set; } = DefaultAutowalkLowMovementThresholdPercent;

    /// <summary>Default/limits for <see cref="AutowalkRestSeconds"/>.</summary>
    public const int DefaultAutowalkRestSeconds = 30;
    public const int MinAutowalkRestSeconds = 5;
    public const int MaxAutowalkRestSeconds = 300;

    /// <summary>How long autowalk rests (in seconds) before standing back up, when "refresh" isn't
    /// memorized.</summary>
    public int AutowalkRestSeconds { get; set; } = DefaultAutowalkRestSeconds;

    /// <summary>When standing up while leading a group, orders every other group member to stand
    /// too ("order &lt;name&gt; stand"). Only fires while the local character is the GMCP-reported
    /// group leader.</summary>
    public bool AutoStandOrderEnabled { get; set; }

    /// <summary>Mirrors <see cref="AutoStandOrderEnabled"/> for resting — fires when the local
    /// character's own GMCP position becomes "resting" (the "rest" command, not "sitting"/"sit")
    /// and orders every other group member to rest too ("order &lt;name&gt; rest").</summary>
    public bool AutoRestOrderEnabled { get; set; }

    /// <summary>Orders a group member to cast refresh on themselves ("order &lt;name&gt; cast
    /// refresh") as soon as GMCP reports their movement at the worst tier ("zamęczony"). Fires once
    /// per exhaustion (not on every GMCP update) and re-arms once they recover or leave the
    /// group.</summary>
    public bool AutoGroupRefreshOnExhaustedEnabled { get; set; }

    /// <summary>Orders every NPC in the current GMCP group (a summoned/charmed pet, which GMCP
    /// reports as a group member with <c>IsNpc</c> true) to assist as soon as the local character's
    /// own position becomes "fighting" ("order &lt;npc&gt; assist"). Unlike
    /// <see cref="AutoStandOrderEnabled"/>/<see cref="AutoRestOrderEnabled"/>, this doesn't require
    /// being the group leader — ordering your own pet doesn't need it.</summary>
    public bool AutoAssistNpcEnabled { get; set; }

    /// <summary>Panels currently pinned as floating overlays on the Terminal, in pin (stacking)
    /// order, each with its relative height weight. Only meaningful in TRANSPARENCY mode — see
    /// <see cref="MudClient.App.Docking.MudDockFactory.IsTransparencyLayout"/> and
    /// <see cref="MudClient.App.Docking.MudDockFactory.OverlayTools"/>.</summary>
    public List<TerminalOverlayEntry> TerminalOverlays { get; set; } = [];

    /// <summary>0 (fully transparent) .. 1 (opaque). Shared by every overlay — lets the terminal
    /// text show through. One setting for all of them, not one per panel.</summary>
    public double TerminalOverlayOpacity { get; set; } = DefaultTerminalOverlayOpacity;
}
