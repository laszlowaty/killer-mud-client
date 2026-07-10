using System.Text.Json;
using Avalonia.Media.Imaging;
using MudClient.Core.Map;

namespace MudClient.App.Services;

public sealed class SectorTextureCache : IDisposable
{
    private readonly string _sectorDirectory;
    private readonly Dictionary<string, string> _manifest;
    private readonly Dictionary<string, Bitmap?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public SectorTextureCache(string sectorDirectory, string? manifestPath)
    {
        _sectorDirectory = sectorDirectory;
        _manifest = LoadManifest(manifestPath);
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

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var bitmap in _cache.Values)
            {
                bitmap?.Dispose();
            }

            _cache.Clear();
        }
    }
}
