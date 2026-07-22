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
                        Name = ResolveExitCommand(rawExit, exitOverrides),
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
                Labels = (rawArea.Labels ?? [])
                    .Where(label => MapCoordinates.TryCreate(label.Coordinates, out _))
                    .Select(label =>
                    {
                        MapCoordinates.TryCreate(label.Coordinates, out var coordinates);
                        return new MapLabel
                        {
                            Id = label.Id,
                            AreaId = rawArea.Id,
                            Text = label.Text ?? string.Empty,
                            Coordinates = coordinates,
                            FontSize = label.FontSize,
                            ShowOnTop = label.ShowOnTop,
                        };
                    })
                    .ToArray(),
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
    /// JSON object containing the fields "id" (an old target identifier) and "command"
    /// (the actual text the player must send, e.g. "up" / "down").  The map
    /// generation pipeline stored the cardinal direction derived from the key
    /// ("west" for "w") instead of the real command, so we must fix it here.
    /// Some exports renumber rooms without updating the embedded id, therefore
    /// the direction key is the primary match and the id is only a fallback.
    /// </summary>
    private static ExitCommandOverrides BuildExitCommandOverrides(
        IReadOnlyDictionary<string, JsonElement>? userData)
    {
        if (userData is null)
            return new ExitCommandOverrides(
                new Dictionary<string, string>(),
                new Dictionary<int, string>());

        var byDirection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var byTargetId = new Dictionary<int, string>();

        foreach (var kv in userData)
        {
            if (kv.Value.ValueKind != JsonValueKind.String)
                continue;

            try
            {
                using var inner = JsonDocument.Parse(kv.Value.GetString()!);
                var root = inner.RootElement;

                if (root.ValueKind == JsonValueKind.Object &&
                    root.TryGetProperty("command", out var cmdProp) &&
                    cmdProp.ValueKind == JsonValueKind.String)
                {
                    var command = cmdProp.GetString()!;
                    if (string.IsNullOrWhiteSpace(command))
                        continue;

                    byDirection[NormalizeDirection(kv.Key)] = command;

                    if (root.TryGetProperty("id", out var idProp) &&
                        TryReadInt32(idProp, out var id))
                    {
                        byTargetId[id] = command;
                    }
                }
            }
            catch (JsonException)
            {
                // Optional legacy metadata can be malformed; retain the regular
                // exported exit name instead of rejecting the whole map.
            }
        }

        return new ExitCommandOverrides(byDirection, byTargetId);
    }

    private static string? ResolveExitCommand(RawMapExit rawExit, ExitCommandOverrides overrides)
    {
        if (!string.IsNullOrWhiteSpace(rawExit.Name) &&
            overrides.ByDirection.TryGetValue(NormalizeDirection(rawExit.Name), out var directionCommand))
        {
            return directionCommand;
        }

        return overrides.ByTargetId.GetValueOrDefault(rawExit.ExitId) ?? rawExit.Name;
    }

    private static bool TryReadInt32(JsonElement element, out int value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.TryGetInt32(out value);
            case JsonValueKind.String:
                return int.TryParse(element.GetString(), out value);
            default:
                value = default;
                return false;
        }
    }

    private static string NormalizeDirection(string direction) =>
        direction.Trim().ToLowerInvariant() switch
        {
            "n" => "north",
            "ne" => "northeast",
            "e" => "east",
            "se" => "southeast",
            "s" => "south",
            "sw" => "southwest",
            "w" => "west",
            "nw" => "northwest",
            "u" => "up",
            "d" => "down",
            _ => direction.Trim().ToLowerInvariant(),
        };

    private sealed record ExitCommandOverrides(
        IReadOnlyDictionary<string, string> ByDirection,
        IReadOnlyDictionary<int, string> ByTargetId);
}
