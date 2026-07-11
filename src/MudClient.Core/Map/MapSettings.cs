namespace MudClient.Core.Map;

public sealed class MapSettings
{
    public string WorldFile { get; init; } = "world-map.json";

    public string SectorDirectory { get; init; } = "Sectors";

    public string SectorManifest { get; init; } = "Sectors/sectors.json";

    public double PixelsPerCoordinateUnit { get; init; } = 18.0;

    public double RoomSize { get; init; } = 20.0;

    public double MinimumZoom { get; init; } = 0.15;

    public double MaximumZoom { get; init; } = 5.0;

    public int SpatialBucketSize { get; init; } = 32;

    public MapGmcpLocationSettings GmcpLocation { get; init; } = new();

    public static MapSettings CreateDefault() => new();
}

public sealed class MapGmcpLocationSettings
{
    public IReadOnlyList<string> Packages { get; init; } = ["Room.Info"];

    public IReadOnlyList<string> VnumPaths { get; init; } =
    [
        "vnum",
        "num",
        "room.vnum",
        "room.num",
        "location.vnum",
        "location.num",
    ];
}
