using MudClient.Core.Map;

namespace MudClient.App.ViewModels;

/// <summary>A recorded death positioned in a map room, resolved from a
/// <see cref="MudClient.App.Models.DeathMarkEntry"/>'s vnum.</summary>
public sealed record DeathMapMarker(MapRoom Room, string Display, string When);
