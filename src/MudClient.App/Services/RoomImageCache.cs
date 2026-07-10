using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace MudClient.App.Services;

/// <summary>
/// Loads per-room images from a directory where each file is named after the
/// room's vnum (e.g. <c>12345.png</c>). The directory is scanned once at
/// construction. Map-sized icons are decoded downscaled on a background thread
/// and kept in a bounded LRU cache so the render loop never blocks on disk I/O;
/// full-size images for the details panel use a small separate LRU cache.
/// All public members except the constructor must be called from the UI thread.
/// </summary>
public sealed class RoomImageCache : IDisposable
{
    private const int MapIconDecodeWidth = 128;
    private const int MapIconCacheCapacity = 600;
    private const int FullImageCacheCapacity = 4;

    private static readonly string[] SupportedExtensions = [".png", ".webp", ".jpg", ".jpeg"];

    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly LruBitmapCache _mapIcons = new(MapIconCacheCapacity);
    private readonly LruBitmapCache _fullImages = new(FullImageCacheCapacity);
    private readonly HashSet<string> _pendingMapIcons = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public RoomImageCache(string roomDirectory)
    {
        if (!Directory.Exists(roomDirectory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(roomDirectory))
        {
            if (SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            {
                _files[Path.GetFileNameWithoutExtension(path)] = path;
            }
        }
    }

    /// <summary>Raised on the UI thread when a background-decoded map icon becomes available.</summary>
    public event Action? MapIconLoaded;

    public int ImageCount => _files.Count;

    public bool HasImage(string? vnum) => vnum is not null && _files.ContainsKey(vnum);

    /// <summary>
    /// Returns the downscaled map icon for the room, or null when the room has
    /// no image or the image is still decoding. Callers should fall back to the
    /// sector texture on null; <see cref="MapIconLoaded"/> fires once the icon
    /// is ready.
    /// </summary>
    public Bitmap? GetMapIcon(string? vnum)
    {
        if (_disposed || vnum is null || !_files.TryGetValue(vnum, out var path))
        {
            return null;
        }

        if (_mapIcons.TryGet(vnum, out var cached))
        {
            return cached;
        }

        if (_pendingMapIcons.Add(vnum))
        {
            Task.Run(() => DecodeMapIcon(vnum, path));
        }

        return null;
    }

    /// <summary>
    /// Returns the full-resolution image for the details panel, decoding it
    /// synchronously on first use (a single file, so the cost is negligible).
    /// </summary>
    public Bitmap? GetFullImage(string? vnum)
    {
        if (_disposed || vnum is null || !_files.TryGetValue(vnum, out var path))
        {
            return null;
        }

        if (_fullImages.TryGet(vnum, out var cached))
        {
            return cached;
        }

        var bitmap = DecodeSafe(path, decodeWidth: null);
        _fullImages.Add(vnum, bitmap);
        return bitmap;
    }

    private void DecodeMapIcon(string vnum, string path)
    {
        var bitmap = DecodeSafe(path, MapIconDecodeWidth);

        Dispatcher.UIThread.Post(() =>
        {
            _pendingMapIcons.Remove(vnum);

            if (_disposed)
            {
                bitmap?.Dispose();
                return;
            }

            // Failed decodes are cached as null so a broken file is not retried
            // on every frame; the room simply keeps its sector texture.
            _mapIcons.Add(vnum, bitmap);

            if (bitmap is not null)
            {
                MapIconLoaded?.Invoke();
            }
        });
    }

    private static Bitmap? DecodeSafe(string path, int? decodeWidth)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return decodeWidth is { } width
                ? Bitmap.DecodeToWidth(stream, width)
                : new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _mapIcons.Dispose();
        _fullImages.Dispose();
    }

    private sealed class LruBitmapCache(int capacity) : IDisposable
    {
        private readonly Dictionary<string, LinkedListNode<(string Key, Bitmap? Bitmap)>> _map =
            new(StringComparer.OrdinalIgnoreCase);

        private readonly LinkedList<(string Key, Bitmap? Bitmap)> _order = new();

        public bool TryGet(string key, out Bitmap? bitmap)
        {
            if (_map.TryGetValue(key, out var node))
            {
                _order.Remove(node);
                _order.AddFirst(node);
                bitmap = node.Value.Bitmap;
                return true;
            }

            bitmap = null;
            return false;
        }

        public void Add(string key, Bitmap? bitmap)
        {
            if (_map.Remove(key, out var existing))
            {
                _order.Remove(existing);
                existing.Value.Bitmap?.Dispose();
            }

            _map[key] = _order.AddFirst((key, bitmap));

            while (_map.Count > capacity)
            {
                var last = _order.Last!;
                _order.RemoveLast();
                _map.Remove(last.Value.Key);
                last.Value.Bitmap?.Dispose();
            }
        }

        public void Dispose()
        {
            foreach (var (_, bitmap) in _order)
            {
                bitmap?.Dispose();
            }

            _map.Clear();
            _order.Clear();
        }
    }
}
