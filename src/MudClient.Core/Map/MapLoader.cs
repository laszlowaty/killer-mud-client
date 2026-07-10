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

                var exits = new List<MapExit>();
                foreach (var rawExit in rawRoom.Exits ?? [])
                {
                    exits.Add(new MapExit
                    {
                        ExitId = rawExit.ExitId,
                        Name = rawExit.Name,
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
}
