using System.Text.Json;
using MudClient.App.Models;

namespace MudClient.App.Services;

/// <summary>
/// Persists the player's local map markers (Phase 1 — local only, not yet shared with anyone).
/// Mirrors <see cref="RareCatalogStore"/>'s atomic-write pattern; unlike it, there's no bundled
/// fallback since markers are entirely player-authored.
/// </summary>
public sealed class MapMarkerStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _path;

    public MapMarkerStore(string? path = null)
    {
        _path = path ?? System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KillerMudClient",
            "map-markers.json");
    }

    public string Path => _path;

    public MapMarkerDocument Load()
    {
        if (!File.Exists(_path))
        {
            return new MapMarkerDocument();
        }

        try
        {
            using var file = File.OpenRead(_path);
            return JsonSerializer.Deserialize<MapMarkerDocument>(file, SerializerOptions)
                ?? new MapMarkerDocument();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("Plik znaczników mapy ma nieprawidłowy format JSON.", exception);
        }
    }

    public async Task SaveAsync(MapMarkerDocument document, CancellationToken cancellationToken = default)
    {
        var directory = System.IO.Path.GetDirectoryName(_path)
            ?? throw new InvalidOperationException("Ścieżka pliku znaczników nie ma katalogu nadrzędnego.");
        Directory.CreateDirectory(directory);
        var temporaryPath = _path + ".tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                16 * 1024,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
