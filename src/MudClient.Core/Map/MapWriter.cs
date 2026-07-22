using System.Text.Json;
using System.Text.Json.Nodes;

namespace MudClient.Core.Map;

/// <summary>
/// Saves the runtime map model while retaining Mudlet fields not consumed by
/// MapLoader (labels, colors, custom lines, stub exits and map metadata).
/// </summary>
public sealed class MapWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
    };

    public async Task SaveAsync(
        MapDocument document,
        string path,
        CancellationToken cancellationToken = default,
        string? baselinePath = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new ArgumentException("Ścieżka mapy nie ma katalogu nadrzędnego.", nameof(path));
        Directory.CreateDirectory(directory);

        var root = await LoadBaselineAsync(baselinePath, cancellationToken).ConfigureAwait(false)
                   ?? new JsonObject();
        PatchDocument(root, document, cancellationToken);

        var temporaryPath = fullPath + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             bufferSize: 64 * 1024,
                             useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, root, SerializerOptions, cancellationToken)
                    .ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static async Task<JsonObject?> LoadBaselineAsync(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 64 * 1024,
            useAsync: true);
        return await JsonNode.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false)
               as JsonObject;
    }

    private static void PatchDocument(JsonObject root, MapDocument document, CancellationToken cancellationToken)
    {
        root["anonymousAreaName"] = document.AnonymousAreaName;
        root["areaCount"] = document.Areas.Count;
        root["roomCount"] = document.Areas.Sum(area => area.Rooms.Count);
        root["labelCount"] = document.Areas.Sum(area => area.Labels.Count);

        var baselineAreas = IndexObjects(root["areas"] as JsonArray, "id");
        var areas = new JsonArray();
        foreach (var area in document.Areas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var areaNode = baselineAreas.GetValueOrDefault(area.Id)?.DeepClone() as JsonObject ?? new JsonObject();
            areaNode["id"] = area.Id;
            areaNode["name"] = area.Name;
            areaNode["roomCount"] = area.Rooms.Count;

            var baselineLabels = IndexObjects(areaNode["labels"] as JsonArray, "id");
            var labels = new JsonArray();
            foreach (var label in area.Labels)
            {
                var isNew = !baselineLabels.TryGetValue(label.Id, out var baselineLabel);
                var labelNode = baselineLabel?.DeepClone() as JsonObject ?? new JsonObject();
                labelNode["id"] = label.Id;
                labelNode["text"] = label.Text;
                labelNode["coordinates"] = new JsonArray(
                    label.Coordinates.X,
                    label.Coordinates.Y,
                    label.Coordinates.Z);
                labelNode["showOnTop"] = label.ShowOnTop;
                if (label.FontSize.HasValue)
                {
                    labelNode["fontSize"] = label.FontSize.Value;
                }

                if (isNew)
                {
                    labelNode["scaledels"] = true;
                    labelNode["colors"] = new JsonArray(
                        new JsonObject { ["color24RGB"] = new JsonArray(230, 230, 230) },
                        new JsonObject { ["color24RGB"] = new JsonArray(0, 0, 0) });
                    labelNode["image"] = new JsonArray();
                    var width = Math.Max(2, label.Text.Length * (label.FontSize ?? 18) / 18.0);
                    labelNode["size"] = new JsonArray(width, Math.Max(1.5, (label.FontSize ?? 18) / 9.0));
                }

                labels.Add(labelNode);
            }

            areaNode["labels"] = labels;

            var baselineRooms = IndexObjects(areaNode["rooms"] as JsonArray, "id");
            var rooms = new JsonArray();
            foreach (var room in area.Rooms)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var roomNode = baselineRooms.GetValueOrDefault(room.Id)?.DeepClone() as JsonObject ?? new JsonObject();
                PatchRoom(roomNode, room);
                rooms.Add(roomNode);
            }

            areaNode["rooms"] = rooms;
            areas.Add(areaNode);
        }

        root["areas"] = areas;
    }

    private static void PatchRoom(JsonObject node, MapRoom room)
    {
        node["id"] = room.Id;
        node["name"] = room.Name;
        node["coordinates"] = new JsonArray(room.Coordinates.X, room.Coordinates.Y, room.Coordinates.Z);

        if (room.Environment.HasValue)
        {
            node["environment"] = room.Environment.Value;
        }

        if (room.Weight.HasValue)
        {
            node["weight"] = room.Weight.Value;
        }

        if (room.Symbol is not null)
        {
            node["symbol"] = room.Symbol;
        }

        var baselineExits = (node["exits"] as JsonArray)?
            .OfType<JsonObject>()
            .Where(item => item["exitId"]?.GetValue<int?>() is not null)
            .GroupBy(item => item["exitId"]!.GetValue<int>())
            .ToDictionary(group => group.Key, group => new Queue<JsonObject>(group));
        var exits = new JsonArray();
        foreach (var exit in room.Exits)
        {
            JsonObject exitNode;
            if (baselineExits is not null && baselineExits.TryGetValue(exit.ExitId, out var matches) && matches.Count > 0)
            {
                exitNode = (JsonObject)matches.Dequeue().DeepClone();
            }
            else
            {
                exitNode = new JsonObject
                {
                    ["exitId"] = exit.ExitId,
                    ["name"] = exit.Name,
                    ["door"] = exit.Door,
                };
            }

            exits.Add(exitNode);
        }

        node["exits"] = exits;
        var userData = new JsonObject();
        if (room.UserData is not null)
        {
            foreach (var pair in room.UserData)
            {
                userData[pair.Key] = JsonNode.Parse(pair.Value.GetRawText());
            }
        }

        node["userData"] = userData;
    }

    private static Dictionary<int, JsonObject> IndexObjects(JsonArray? array, string idProperty)
    {
        var result = new Dictionary<int, JsonObject>();
        if (array is null)
        {
            return result;
        }

        foreach (var item in array.OfType<JsonObject>())
        {
            if (item[idProperty]?.GetValue<int?>() is { } id)
            {
                result[id] = item;
            }
        }

        return result;
    }
}
