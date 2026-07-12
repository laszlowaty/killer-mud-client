using System.Text.Json;
using Avalonia.Media.Imaging;
using MudClient.Core.Map;

namespace MudClient.App.Services;

public sealed class SectorTextureCache : IDisposable
{
    private const string BackgroundTextureKey = "_world_background";

    private readonly string _sectorDirectory;
    private readonly Dictionary<string, string> _manifest;
    private readonly Dictionary<string, Bitmap?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(int AreaId, double Z), BackdropManifestEntry> _backdropManifest;
    private readonly Dictionary<(int AreaId, double Z), IReadOnlyList<LocationBackdropManifestEntry>> _locationBackdropManifest;
    private readonly Dictionary<(int AreaId, double Z), (Bitmap? Terrain, Bitmap? Rooms)> _backdropCache = [];
    private readonly Dictionary<string, Bitmap?> _locationBackdropCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<(int AreaId, double Z)> _backdropLru = [];
    private readonly object _lock = new();

    public SectorTextureCache(string sectorDirectory, string? manifestPath)
    {
        _sectorDirectory = sectorDirectory;
        _manifest = LoadManifest(manifestPath);
        _backdropManifest = LoadBackdropManifest(Path.Combine(sectorDirectory, "..", "Backdrops", "manifest.json"));
        _locationBackdropManifest = LoadLocationBackdropManifest(
            Path.Combine(sectorDirectory, "..", "Locations", "manifest.json"));
    }

    public Bitmap? GetTexture(string sectorName)
    {
        if (string.IsNullOrWhiteSpace(sectorName))
        {
            return GetDefaultTexture();
        }

        lock (_lock)
        {
            if (_cache.TryGetValue(sectorName, out var cached))
            {
                return cached ?? GetDefaultTexture();
            }

            var bitmap = TryLoad(sectorName);
            _cache[sectorName] = bitmap;
            return bitmap ?? GetDefaultTexture();
        }
    }

    public Bitmap? GetBackgroundTexture()
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(BackgroundTextureKey, out var cached))
            {
                return cached;
            }

            var path = Path.Combine(_sectorDirectory, "world-background.png");
            var bitmap = LoadBitmapSafe(path);
            _cache[BackgroundTextureKey] = bitmap;
            return bitmap;
        }
    }

    public WorldBackdrop? GetWorldBackdrop(int areaId, double z)
    {
        var key = (areaId, z);
        lock (_lock)
        {
            if (!_backdropManifest.TryGetValue(key, out var entry))
            {
                return null;
            }

            if (!_backdropCache.TryGetValue(key, out var bitmaps))
            {
                while (_backdropCache.Count >= 2 && _backdropLru.First is { } oldest)
                {
                    if (_backdropCache.Remove(oldest.Value, out var evicted))
                    {
                        evicted.Terrain?.Dispose();
                        evicted.Rooms?.Dispose();
                    }

                    _backdropLru.RemoveFirst();
                }

                var directory = Path.GetFullPath(Path.Combine(_sectorDirectory, "..", "Backdrops"));
                bitmaps = (
                    LoadBitmapSafe(Path.Combine(directory, entry.FileName)),
                    LoadBitmapSafe(Path.Combine(directory, entry.OverviewFileName)));
                _backdropCache[key] = bitmaps;
            }

            _backdropLru.Remove(key);
            _backdropLru.AddLast(key);

            return bitmaps.Terrain is null || bitmaps.Rooms is null
                ? null
                : new WorldBackdrop(bitmaps.Terrain, bitmaps.Rooms, entry.MinX, entry.MinY, entry.MaxX, entry.MaxY, entry.PixelsPerUnit);
        }
    }

    public IReadOnlyList<LocationBackdrop> GetLocationBackdrops(int areaId, double z)
    {
        lock (_lock)
        {
            if (!_locationBackdropManifest.TryGetValue((areaId, z), out var entries))
            {
                return [];
            }

            var directory = Path.GetFullPath(Path.Combine(_sectorDirectory, "..", "Locations"));
            var result = new List<LocationBackdrop>(entries.Count);
            foreach (var entry in entries)
            {
                if (!_locationBackdropCache.TryGetValue(entry.FileName, out var bitmap))
                {
                    bitmap = LoadBitmapSafe(Path.Combine(directory, entry.FileName));
                    _locationBackdropCache[entry.FileName] = bitmap;
                }

                if (bitmap is not null)
                {
                    result.Add(new LocationBackdrop(
                        bitmap, entry.MinX, entry.MinY, entry.MaxX, entry.MaxY,
                        Math.Clamp(entry.Opacity, 0, 1), Math.Clamp(entry.EdgeFade, 0, 0.49)));
                }
            }

            return result;
        }
    }

    private Bitmap? GetDefaultTexture()
    {
        const string defaultKey = "_default";

        lock (_lock)
        {
            if (_cache.TryGetValue(defaultKey, out var cached))
            {
                return cached;
            }

            var path = Path.Combine(_sectorDirectory, "_default.png");
            var bitmap = LoadBitmapSafe(path);
            _cache[defaultKey] = bitmap;
            return bitmap;
        }
    }

    private Bitmap? TryLoad(string sectorName)
    {
        if (_manifest.TryGetValue(sectorName, out var manifestFile))
        {
            var manifestPath = Path.Combine(_sectorDirectory, manifestFile);
            var bitmap = LoadBitmapSafe(manifestPath);
            if (bitmap is not null)
            {
                return bitmap;
            }
        }

        var fileName = SectorNameNormalizer.ToFileName(sectorName);
        return LoadBitmapSafe(Path.Combine(_sectorDirectory, fileName));
    }

    private static Bitmap? LoadBitmapSafe(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static Dictionary<string, string> LoadManifest(string? manifestPath)
    {
        var manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (manifestPath is null || !File.Exists(manifestPath))
        {
            return manifest;
        }

        try
        {
            var json = File.ReadAllText(manifestPath);
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            if (parsed is not null)
            {
                foreach (var (key, value) in parsed)
                {
                    manifest[key] = value;
                }
            }
        }
        catch (JsonException)
        {
            // Manifest is optional; fall back to automatic name normalization.
        }

        return manifest;
    }

    private static Dictionary<(int AreaId, double Z), BackdropManifestEntry> LoadBackdropManifest(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<BackdropManifestEntry>>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return entries?.ToDictionary(entry => (entry.AreaId, entry.Z)) ?? [];
        }
        catch (JsonException)
        {
            // Generated backdrops are optional; the vector renderer remains the fallback.
            return [];
        }
    }

    private static Dictionary<(int AreaId, double Z), IReadOnlyList<LocationBackdropManifestEntry>>
        LoadLocationBackdropManifest(string path)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<LocationBackdropManifestEntry>>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            return entries
                .GroupBy(entry => (entry.AreaId, entry.Z))
                .ToDictionary(group => group.Key, group => (IReadOnlyList<LocationBackdropManifestEntry>)group.ToList());
        }
        catch (JsonException)
        {
            // Location artwork is optional; malformed metadata must not prevent the map from loading.
            return [];
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var bitmap in _cache.Values)
            {
                bitmap?.Dispose();
            }

            _cache.Clear();

            foreach (var bitmaps in _backdropCache.Values)
            {
                bitmaps.Terrain?.Dispose();
                bitmaps.Rooms?.Dispose();
            }

            _backdropCache.Clear();
            _backdropLru.Clear();

            foreach (var bitmap in _locationBackdropCache.Values)
            {
                bitmap?.Dispose();
            }

            _locationBackdropCache.Clear();
        }
    }

    private sealed class BackdropManifestEntry
    {
        public int AreaId { get; init; }
        public double Z { get; init; }
        public double MinX { get; init; }
        public double MinY { get; init; }
        public double MaxX { get; init; }
        public double MaxY { get; init; }
        public int PixelsPerUnit { get; init; }
        public required string FileName { get; init; }
        public required string OverviewFileName { get; init; }
    }

    private sealed class LocationBackdropManifestEntry
    {
        public int AreaId { get; init; }
        public double Z { get; init; }
        public double MinX { get; init; }
        public double MinY { get; init; }
        public double MaxX { get; init; }
        public double MaxY { get; init; }
        public double Opacity { get; init; } = 0.72;
        public double EdgeFade { get; init; }
        public required string FileName { get; init; }
    }
}

public sealed record WorldBackdrop(
    Bitmap Terrain,
    Bitmap Rooms,
    double MinX,
    double MinY,
    double MaxX,
    double MaxY,
    int PixelsPerUnit);

public sealed record LocationBackdrop(
    Bitmap Image,
    double MinX,
    double MinY,
    double MaxX,
    double MaxY,
    double Opacity,
    double EdgeFade);
