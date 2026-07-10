using System.Text.Json;

namespace MudClient.Core.Map;

public sealed class MapRoom
{
    public required int Id { get; init; }

    public required int AreaId { get; init; }

    public string? Name { get; init; }

    public required MapCoordinates Coordinates { get; init; }

    public int? Environment { get; init; }

    public double? Weight { get; init; }

    public string? Symbol { get; init; }

    public IReadOnlyList<MapExit> Exits { get; init; } = [];

    public IReadOnlyDictionary<string, JsonElement>? UserData { get; init; }

    public string? Vnum => TryGetUserDataString("vnum", out var vnum) ? vnum : null;

    public string? Sector => TryGetUserDataString("sector", out var sector) ? sector : null;

    public bool TryGetUserDataString(string key, out string? value)
    {
        value = null;

        if (UserData is null || !UserData.TryGetValue(key, out var element))
        {
            return false;
        }

        value = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.ToString(),
            _ => null,
        };

        return value is not null;
    }
}
