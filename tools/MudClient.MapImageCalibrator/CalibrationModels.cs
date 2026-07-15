using System.Text.Json;

namespace MudClient.MapImageCalibrator;

public sealed record RoomPoint(
    int Id,
    string? Vnum,
    string? Name,
    string? Sector,
    int AreaId,
    double X,
    double Y,
    double Z,
    int[] Exits)
{
    public override string ToString() => $"{Vnum ?? "?"}: {Name ?? "(bez nazwy)"}";
}

public sealed record MapAtlas(int Id, string Name, IReadOnlyList<RoomPoint> Rooms)
{
    public override string ToString() => $"{Name} ({Rooms.Count} roomów)";
}

public sealed class CalibrationRepository
{
    public CalibrationRepository(string mapDirectory)
    {
        MapDirectory = mapDirectory;
    }

    public string MapDirectory { get; }

    public List<MapAtlas> LoadAtlases()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(MapDirectory, "world-map.json")));
        var result = new List<MapAtlas>();
        foreach (var area in document.RootElement.GetProperty("areas").EnumerateArray())
        {
            var areaId = area.GetProperty("id").GetInt32();
            var areaName = area.TryGetProperty("name", out var rawAreaName)
                ? rawAreaName.GetString() ?? $"Atlas {areaId}"
                : $"Atlas {areaId}";
            var rooms = new List<RoomPoint>();
            foreach (var room in area.GetProperty("rooms").EnumerateArray())
            {
                var coordinates = room.GetProperty("coordinates");
                string? vnum = null;
                string? sector = null;
                if (room.TryGetProperty("userData", out var userData))
                {
                    if (userData.TryGetProperty("vnum", out var rawVnum)) vnum = rawVnum.ToString();
                    if (userData.TryGetProperty("sector", out var rawSector)) sector = rawSector.GetString();
                }

                var exits = room.TryGetProperty("exits", out var rawExits)
                    ? rawExits.EnumerateArray().Select(exit => exit.GetProperty("exitId").GetInt32()).ToArray()
                    : [];
                rooms.Add(new RoomPoint(
                    room.GetProperty("id").GetInt32(),
                    vnum,
                    room.TryGetProperty("name", out var name) ? name.GetString() : null,
                    sector,
                    areaId,
                    coordinates[0].GetDouble(),
                    coordinates[1].GetDouble(),
                    coordinates[2].GetDouble(),
                    exits));
            }
            result.Add(new MapAtlas(areaId, areaName, rooms));
        }
        return result.OrderBy(atlas => atlas.Id).ToList();
    }

    public static string FindMapDirectory(string? requestedRoot)
    {
        var current = new DirectoryInfo(Path.GetFullPath(requestedRoot ?? Environment.CurrentDirectory));
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "MudClient.App", "Assets", "Map");
            if (File.Exists(Path.Combine(candidate, "world-map.json"))) return candidate;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException(
            "Nie znaleziono src/MudClient.App/Assets/Map. Uruchom narzędzie z repozytorium lub podaj katalog repo jako argument.");
    }
}
