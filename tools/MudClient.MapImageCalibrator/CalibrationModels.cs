using System.Text.Json;
using System.Text.Json.Serialization;

namespace MudClient.MapImageCalibrator;

public sealed class LocationLayer
{
    public int AreaId { get; set; }
    public double Z { get; set; }
    public double MinX { get; set; }
    public double MinY { get; set; }
    public double MaxX { get; set; }
    public double MaxY { get; set; }
    public double Opacity { get; set; } = 0.9;
    public double EdgeFade { get; set; }
    public required string FileName { get; set; }

    [JsonIgnore]
    public double Width => MaxX - MinX;
    [JsonIgnore]
    public double Height => MaxY - MinY;
    public override string ToString() => $"{FileName}  (area {AreaId}, z {Z:0.###})";
}

public sealed record RoomPoint(int Id, string? Vnum, string? Name, int AreaId, double X, double Y, double Z, int[] Exits)
{
    public override string ToString() => $"{Vnum ?? "?"}: {Name ?? "(bez nazwy)"}";
}

public sealed class CalibrationAnchor
{
    public string Label { get; set; } = string.Empty;
    public required string Vnum { get; set; }
    public double ImageX { get; set; }
    public double ImageY { get; set; }
    public double WorldX { get; set; }
    public double WorldY { get; set; }
    public override string ToString() => $"{Vnum} — {Label}  [{ImageX:0},{ImageY:0}]";
}

public sealed class ImageMarker
{
    public int Number { get; set; }
    public string Label { get; set; } = string.Empty;
    public double ImageX { get; set; }
    public double ImageY { get; set; }
    public override string ToString() => $"#{Number} — {Label}  [{ImageX:0},{ImageY:0}]";
}

public sealed class RoomOffset
{
    public int RoomId { get; set; }
    public string? Vnum { get; set; }
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
}

public sealed class CalibrationWorkspace
{
    public required string ImageFile { get; set; }
    public string? LayerName { get; set; }
    public bool IsBlankCanvas { get; set; }
    public List<int> IncludedRoomIds { get; set; } = [];
    public List<RoomReference> Rooms { get; set; } = [];
    public List<ImageMarker> Markers { get; set; } = [];
    public List<RoomOffset> RoomOffsets { get; set; } = [];
}

public sealed class RoomReference
{
    public int RoomId { get; set; }
    public string? Vnum { get; set; }
    public string? Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
}

public sealed class CalibrationRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public CalibrationRepository(string mapDirectory)
    {
        MapDirectory = mapDirectory;
        LocationsDirectory = Path.Combine(mapDirectory, "Locations");
        ManifestPath = Path.Combine(LocationsDirectory, "manifest.json");
    }

    public string MapDirectory { get; }
    public string LocationsDirectory { get; }
    public string ManifestPath { get; }

    public List<LocationLayer> LoadLayers() =>
        JsonSerializer.Deserialize<List<LocationLayer>>(File.ReadAllText(ManifestPath), JsonOptions) ?? [];

    public void SaveLayers(IReadOnlyList<LocationLayer> layers) =>
        File.WriteAllText(ManifestPath, JsonSerializer.Serialize(layers, JsonOptions) + Environment.NewLine);

    public List<CalibrationAnchor> LoadAnchors(LocationLayer layer)
    {
        var path = AnchorPath(layer);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<List<CalibrationAnchor>>(File.ReadAllText(path), JsonOptions) ?? []
            : [];
    }

    public void SaveAnchors(LocationLayer layer, IReadOnlyList<CalibrationAnchor> anchors) =>
        File.WriteAllText(AnchorPath(layer), JsonSerializer.Serialize(anchors, JsonOptions) + Environment.NewLine);

    public CalibrationWorkspace LoadWorkspace(LocationLayer layer)
    {
        var path = WorkspacePath(layer);
        return File.Exists(path)
            ? JsonSerializer.Deserialize<CalibrationWorkspace>(File.ReadAllText(path), JsonOptions)
                ?? new CalibrationWorkspace { ImageFile = layer.FileName }
            : new CalibrationWorkspace { ImageFile = layer.FileName };
    }

    public void SaveWorkspace(LocationLayer layer, CalibrationWorkspace workspace) =>
        File.WriteAllText(WorkspacePath(layer), JsonSerializer.Serialize(workspace, JsonOptions) + Environment.NewLine);

    public void ClearWorkspace(LocationLayer layer)
    {
        foreach (var path in new[] { WorkspacePath(layer), AnchorPath(layer) })
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    public string ExportWorkspace(LocationLayer layer, CalibrationWorkspace workspace)
    {
        var directory = Path.Combine(LocationsDirectory, "CalibrationExports");
        Directory.CreateDirectory(directory);
        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var path = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(layer.FileName)}-{stamp}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(workspace, JsonOptions) + Environment.NewLine);
        return path;
    }

    public List<RoomPoint> LoadRooms()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(MapDirectory, "world-map.json")));
        var result = new List<RoomPoint>();
        foreach (var area in document.RootElement.GetProperty("areas").EnumerateArray())
        {
            var areaId = area.GetProperty("id").GetInt32();
            foreach (var room in area.GetProperty("rooms").EnumerateArray())
            {
                var coordinates = room.GetProperty("coordinates");
                string? vnum = null;
                if (room.TryGetProperty("userData", out var userData) &&
                    userData.TryGetProperty("vnum", out var rawVnum))
                {
                    vnum = rawVnum.ToString();
                }
                var exits = room.TryGetProperty("exits", out var rawExits)
                    ? rawExits.EnumerateArray().Select(exit => exit.GetProperty("exitId").GetInt32()).ToArray()
                    : [];
                result.Add(new RoomPoint(
                    room.GetProperty("id").GetInt32(), vnum,
                    room.TryGetProperty("name", out var name) ? name.GetString() : null,
                    areaId, coordinates[0].GetDouble(), coordinates[1].GetDouble(), coordinates[2].GetDouble(), exits));
            }
        }
        return result;
    }

    public static string FindMapDirectory(string? requestedRoot)
    {
        var current = new DirectoryInfo(Path.GetFullPath(requestedRoot ?? Environment.CurrentDirectory));
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "MudClient.App", "Assets", "Map");
            if (File.Exists(Path.Combine(candidate, "world-map.json")) &&
                File.Exists(Path.Combine(candidate, "Locations", "manifest.json")))
            {
                return candidate;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Nie znaleziono src/MudClient.App/Assets/Map. Uruchom narzędzie z repozytorium lub podaj katalog repo jako argument.");
    }

    private string AnchorPath(LocationLayer layer) =>
        Path.Combine(LocationsDirectory, Path.GetFileNameWithoutExtension(layer.FileName) + ".anchors.json");
    private string WorkspacePath(LocationLayer layer) =>
        Path.Combine(LocationsDirectory, Path.GetFileNameWithoutExtension(layer.FileName) + ".calibration.json");
}
