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

    /// <summary>Default for <see cref="CommandStackingSeparator"/>.</summary>
    public const string DefaultCommandStackingSeparator = ";";

    /// <summary>Font used for text received from the MUD in the main output view.</summary>
    public string OutputFontFamily { get; set; } = DefaultOutputFontFamily;

    public double OutputFontSize { get; set; } = DefaultOutputFontSize;

    /// <summary>Wraps long MUD output lines to the terminal width.</summary>
    public bool OutputWordWrap { get; set; } = true;

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

    /// <summary>Executes strictly formatted orders issued by current GMCP group members.</summary>
    public bool GroupOrdersEnabled { get; set; }
}
