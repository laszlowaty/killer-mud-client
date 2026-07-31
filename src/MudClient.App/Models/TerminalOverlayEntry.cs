namespace MudClient.App.Models;

/// <summary>
/// Persisted state for one panel pinned as a floating overlay on the Terminal. Several of these
/// can exist at once, grouped into columns that float on top of the Terminal (which is never
/// resized by them) and stack right-to-left: column 0 hugs the right edge, higher indices sit
/// further left. Width and height-fraction are logically per-column, not per-panel, but are
/// still stored here (kept in sync across every entry sharing a <see cref="ColumnIndex"/> by
/// <see cref="MudClient.App.Controls.TerminalOverlayHost"/>) — see
/// <see cref="AppSettings.TerminalOverlays"/> and
/// <see cref="MudClient.App.Docking.MudDockFactory.OverlayTools"/>.
/// </summary>
public sealed class TerminalOverlayEntry
{
    public required string PanelId { get; set; }

    /// <summary>This panel's height relative to the others in its column — a Grid star weight, so
    /// e.g. 2.0 next to a sibling's 1.0 renders twice as tall. Adjusted by dragging the splitter
    /// between two stacked overlay cards.</summary>
    public double HeightWeight { get; set; } = AppSettings.DefaultTerminalOverlayHeightWeight;

    /// <summary>Which column this overlay renders in, 0 = hugging the right edge, increasing as
    /// columns move further left toward the Terminal's center. Defaults to 0 — the single
    /// right-aligned column this feature originally shipped with.</summary>
    public int ColumnIndex { get; set; }

    /// <summary>This column's width in pixels. Shared by every entry in the same column.</summary>
    public double ColumnWidth { get; set; } = AppSettings.DefaultTerminalOverlayColumnWidth;

    /// <summary>This column's overall height as a fraction (0..1) of the Terminal's own height.
    /// The stack is anchored to the top, so shrinking this reveals terminal below the last card.
    /// Shared by every entry in the same column.</summary>
    public double ColumnHeightFraction { get; set; } = AppSettings.DefaultTerminalOverlayColumnHeightFraction;
}
