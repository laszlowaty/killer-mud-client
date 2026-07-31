namespace MudClient.App.Models;

/// <summary>Which side of the Terminal (which is always centered) an overlay's stack renders on
/// — see <see cref="TerminalOverlayEntry.Side"/>.</summary>
public enum OverlaySide
{
    Left,
    Right,
}

/// <summary>
/// Persisted state for one panel pinned as a floating overlay on the Terminal. Several of these
/// can exist at once, stacked in one column on either side of the Terminal (which always stays
/// centered) — see <see cref="AppSettings.TerminalOverlays"/> and
/// <see cref="MudClient.App.Docking.MudDockFactory.OverlayTools"/>. Position within a side's
/// stack and the columns' overall width are not per-panel: only the relative height within the
/// stack and which side it's on are.
/// </summary>
public sealed class TerminalOverlayEntry
{
    public required string PanelId { get; set; }

    /// <summary>This panel's height relative to the others in its side's stack — a Grid star
    /// weight, so e.g. 2.0 next to a sibling's 1.0 renders twice as tall. Adjusted by dragging the
    /// splitter between two stacked overlay cards.</summary>
    public double HeightWeight { get; set; } = AppSettings.DefaultTerminalOverlayHeightWeight;

    /// <summary>Right by default — matches the single right-aligned stack this feature originally
    /// shipped with, so existing saved entries (with no persisted Side) keep their prior position.</summary>
    public OverlaySide Side { get; set; } = OverlaySide.Right;
}
