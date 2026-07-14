using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;

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

public sealed record RoomPoint(int Id, string? Vnum, string? Name, string? Sector, int AreaId, double X, double Y, double Z, int[] Exits)
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

public sealed class MapImageElement
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public required string AssetFile { get; set; }
    public double ImageX { get; set; }
    public double ImageY { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double Rotation { get; set; }
    public double Opacity { get; set; } = 1;
    public int ZIndex { get; set; }

    public MapImageElement Clone() => new()
    {
        Id = Id,
        AssetFile = AssetFile,
        ImageX = ImageX,
        ImageY = ImageY,
        Width = Width,
        Height = Height,
        Rotation = Rotation,
        Opacity = Opacity,
        ZIndex = ZIndex,
    };

    public override string ToString() => $"{Path.GetFileNameWithoutExtension(AssetFile)}  [{ImageX:0}, {ImageY:0}]";
}

public sealed record MapEditorAsset(string File, string Name, string Category)
{
    public override string ToString() => Name;
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
    public List<MapImageElement> ImageElements { get; set; } = [];
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
    private static readonly string[] DefaultEditorAssets =
    [
        "Sectors/03_droga.png",
        "Sectors/12_plac.png",
        "Sectors/20_las.png",
        "Sectors/25_park.png",
        "Sectors/29_ruiny.png",
        "Sectors/30_miasto.png",
        "Sectors/32_jezioro.png",
        "Sectors/36_rzeka.png",
        "Sectors/40_gory.png",
        "Sectors/46_trawa.png",
    ];
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

    public IReadOnlyList<MapEditorAsset> LoadEditorAssets()
    {
        var result = new List<MapEditorAsset>();
        var editorDirectory = Path.Combine(MapDirectory, "EditorAssets");
        if (Directory.Exists(editorDirectory))
        {
            foreach (var path in Directory.EnumerateFiles(editorDirectory, "*.png", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(MapDirectory, path).Replace('\\', '/');
                var category = Path.GetDirectoryName(Path.GetRelativePath(editorDirectory, path)) ?? "Własne";
                result.Add(new MapEditorAsset(relative, FriendlyAssetName(path), category));
            }
        }

        foreach (var relative in DefaultEditorAssets)
        {
            if (File.Exists(ResolveEditorAssetPath(relative)))
                result.Add(new MapEditorAsset(relative, FriendlyAssetName(relative), "Domyślne tekstury"));
        }

        return result
            .DistinctBy(asset => asset.File, StringComparer.OrdinalIgnoreCase)
            .OrderBy(asset => asset.Category, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(asset => asset.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public string ResolveEditorAssetPath(string relativePath)
    {
        var root = Path.GetFullPath(MapDirectory) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(MapDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Asset edytora wychodzi poza katalog mapy: {relativePath}");
        return path;
    }

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

    public static string CompositePathForExport(string jsonPath) => Path.Combine(
        Path.GetDirectoryName(jsonPath) ?? string.Empty,
        Path.GetFileNameWithoutExtension(jsonPath) + "-composite.png");

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
                string? sector = null;
                if (room.TryGetProperty("userData", out var sectorData) &&
                    sectorData.TryGetProperty("sector", out var rawSector))
                {
                    sector = rawSector.GetString();
                }
                result.Add(new RoomPoint(
                    room.GetProperty("id").GetInt32(), vnum,
                    room.TryGetProperty("name", out var name) ? name.GetString() : null,
                    sector,
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

    private static string FriendlyAssetName(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var separator = name.IndexOf('_');
        if (separator >= 0 && name[..separator].All(char.IsDigit)) name = name[(separator + 1)..];
        name = name.Replace('_', ' ').Replace('-', ' ');
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
    }
}
