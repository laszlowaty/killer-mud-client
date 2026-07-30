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

    /// <summary>Default/limits for the overlay column's overall width, as a fraction (0..1) of
    /// the Terminal's own width (see <see cref="TerminalOverlayColumnWidthFraction"/>).</summary>
    public const double DefaultTerminalOverlayColumnWidthFraction = 0.42;
    public const double MinTerminalOverlayColumnWidthFraction = 0.2;
    public const double MaxTerminalOverlayColumnWidthFraction = 0.7;

    /// <summary>Default/limits for the overlay column's overall height, as a fraction (0..1) of
    /// the Terminal's own height (see <see cref="TerminalOverlayColumnHeightFraction"/>). The
    /// stack is anchored to the top, so shrinking this reveals terminal below the last card.</summary>
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

    /// <summary>Last chosen "Tryb mapy" (Proceduralna/Prosta) — restored on the next launch.</summary>
    public MapDisplayMode MapDisplayMode { get; set; } = MapDisplayMode.Procedural;

    /// <summary>Panels currently pinned as floating overlays on the Terminal, in pin (stacking)
    /// order, each with its relative height weight. Only meaningful in TRANSPARENCY mode — see
    /// <see cref="MudClient.App.Docking.MudDockFactory.IsTransparencyLayout"/> and
    /// <see cref="MudClient.App.Docking.MudDockFactory.OverlayTools"/>.</summary>
    public List<TerminalOverlayEntry> TerminalOverlays { get; set; } = [];

    /// <summary>0 (fully transparent) .. 1 (opaque). Shared by every overlay — lets the terminal
    /// text show through. One setting for all of them, not one per panel.</summary>
    public double TerminalOverlayOpacity { get; set; } = DefaultTerminalOverlayOpacity;

    /// <summary>Width of the overlay column as a fraction (0..1) of the Terminal's own width.
    /// Shared by every overlay — they all live in one right-aligned column.</summary>
    public double TerminalOverlayColumnWidthFraction { get; set; } = DefaultTerminalOverlayColumnWidthFraction;

    /// <summary>Height of the overlay column as a fraction (0..1) of the Terminal's own height.
    /// The stack is anchored to the top; dragging the handle below the last card shrinks this to
    /// reveal terminal beneath it.</summary>
    public double TerminalOverlayColumnHeightFraction { get; set; } = DefaultTerminalOverlayColumnHeightFraction;
}
