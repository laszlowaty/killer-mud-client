using MudClient.Core.Map;

namespace MudClient.App.ViewModels;

/// <summary>A player-placed local marker (see <see cref="MudClient.App.Models.MapMarker"/>)
/// resolved to the <see cref="MapRoom"/> its vnum points at.</summary>
public sealed record RoomMapMarker(MapRoom Room, string Symbol);
