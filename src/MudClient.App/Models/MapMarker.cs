namespace MudClient.App.Models;

/// <summary>
/// A player-placed local marker on a specific room, keyed by vnum. One marker per vnum — setting
/// a new symbol on an already-marked room replaces it. Phase 1: purely local, not yet shared.
/// </summary>
public sealed record MapMarker(string Vnum, string Symbol);

public sealed class MapMarkerDocument
{
    public List<MapMarker> Markers { get; set; } = [];
}
