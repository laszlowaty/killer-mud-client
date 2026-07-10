using System.Text.Json;

namespace MudClient.Core.Map;

public sealed class MapLoader
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<MapLoadResult> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new MapLoadException($"Plik mapy nie został znaleziony: {path}");
        }

        RawMapDocument? raw;

        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);

            raw = await JsonSerializer
                .DeserializeAsync<RawMapDocument>(stream, SerializerOptions, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException exception)
        {
            throw new MapLoadException($"Nieprawidłowy format JSON mapy: {exception.Message}", exception);
        }

        if (raw is null)
        {
            throw new MapLoadException("Plik mapy jest pusty lub nieprawidłowy.");
        }

        var warnings = new List<string>();
        var areas = new List<MapArea>();

        foreach (var rawArea in raw.Areas ?? [])
        {
            var rooms = new List<MapRoom>();

            foreach (var rawRoom in rawArea.Rooms ?? [])
            {
                if (!MapCoordinates.TryCreate(rawRoom.Coordinates, out var coordinates))
                {
                    warnings.Add(
                        $"Pominięto pokój {rawRoom.Id} w obszarze {rawArea.Id}: brak poprawnych współrzędnych.");
                    continue;
                }

                var exitOverrides = BuildExitCommandOverrides(rawRoom.UserData);

                var exits = new List<MapExit>();
                foreach (var rawExit in rawRoom.Exits ?? [])
                {
                    exits.Add(new MapExit
                    {
                        ExitId = rawExit.ExitId,
                        Name = exitOverrides.TryGetValue(rawExit.ExitId, out var cmd) ? cmd : rawExit.Name,
                        Door = rawExit.Door,
                    });
                }

                rooms.Add(new MapRoom
                {
                    Id = rawRoom.Id,
                    AreaId = rawArea.Id,
                    Name = rawRoom.Name,
                    Coordinates = coordinates,
                    Environment = rawRoom.Environment,
                    Weight = rawRoom.Weight,
                    Symbol = rawRoom.Symbol,
                    Exits = exits,
                    UserData = rawRoom.UserData,
                });
            }

            areas.Add(new MapArea
            {
                Id = rawArea.Id,
                Name = rawArea.Name,
                Rooms = rooms,
            });
        }

        var document = new MapDocument
        {
            AnonymousAreaName = raw.AnonymousAreaName,
            Areas = areas,
        };

        return new MapLoadResult
        {
            Document = document,
            Warnings = warnings,
        };
    }

    /// <summary>
    /// Old-style exit data is stored in userData keys (direction abbreviations
    /// like "n", "s", "w", "e", "ne", "u", "d") whose value is a serialised
    /// JSON object containing the fields "id" (target room id) and "command"
    /// (the actual text the player must send, e.g. "up" / "down").  The map
    /// generation pipeline stored the cardinal direction derived from the key
    /// ("west" for "w") instead of the real command, so we must fix it here.
    /// </summary>
    private static Dictionary<int, string> BuildExitCommandOverrides(
        IReadOnlyDictionary<string, JsonElement>? userData)
    {
        if (userData is null)
            return [];

        var overrides = new Dictionary<int, string>();

        foreach (var kv in userData)
        {
            if (kv.Value.ValueKind != JsonValueKind.String)
                continue;

            try
            {
                using var inner = JsonDocument.Parse(kv.Value.GetString()!);
                var root = inner.RootElement;

                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("id", out var idProp) &&
                    idProp.ValueKind == JsonValueKind.Number &&
                    idProp.TryGetInt32(out var id) &&
                    root.TryGetProperty("command", out var cmdProp) &&
                    cmdProp.ValueKind == JsonValueKind.String)
                {
                    var command = cmdProp.GetString()!;
                    if (!string.IsNullOrWhiteSpace(command))
                    {
                        overrides[id] = command;
                    }
                }
            }
            catch (JsonException)
            {
                // Malformed old-style exit entry — skip silently.
            }
        }

        return overrides;
    }
}
