namespace MudClient.App.Models;

/// <summary>
/// Persisted state for one panel pinned as a floating overlay on the Terminal. Several of these
/// can exist at once, stacked in one right-aligned column — see
/// <see cref="AppSettings.TerminalOverlays"/> and
/// <see cref="MudClient.App.Docking.MudDockFactory.OverlayTools"/>. Position and the column's
/// overall width are not per-panel: only the relative height within the stack is.
/// </summary>
public sealed class TerminalOverlayEntry
{
    public required string PanelId { get; set; }

    /// <summary>This panel's height relative to the others in the stack — a Grid star weight, so
    /// e.g. 2.0 next to a sibling's 1.0 renders twice as tall. Adjusted by dragging the splitter
    /// between two stacked overlay cards.</summary>
    public double HeightWeight { get; set; } = AppSettings.DefaultTerminalOverlayHeightWeight;
}
