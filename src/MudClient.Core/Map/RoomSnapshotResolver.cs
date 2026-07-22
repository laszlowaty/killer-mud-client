using System.Text.Json;
using MudClient.Core.Gmcp;

namespace MudClient.Core.Map;

public sealed record RoomSnapshotExit(
    string Direction,
    string? Name,
    bool HasDoor,
    bool IsClosed)
{
    public string Command => string.IsNullOrWhiteSpace(Name) ? Direction : Name;
}

public sealed record RoomSnapshot(
    string Vnum,
    string? Name,
    string? Sector,
    IReadOnlyList<RoomSnapshotExit> Exits);

/// <summary>
/// Parses complete object-shaped GMCP Room.Info messages. Unlike the location
/// resolver, it publishes repeated snapshots for the same vnum so callers can
/// distinguish a failed movement from a successful one.
/// </summary>
public sealed class RoomSnapshotResolver
{
    public RoomSnapshot? Current { get; private set; }

    public event Action<RoomSnapshot>? SnapshotReceived;

    public void Process(GmcpMessage message)
    {
        if (!string.Equals(message.Package, "Room.Info", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(message.Json))
        {
            return;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(message.Json);
        }
        catch (JsonException)
        {
            return;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !TryReadVnum(root, out var vnum))
            {
                return;
            }

            var exits = new List<RoomSnapshotExit>();
            if (root.TryGetProperty("exits", out var exitsElement) && exitsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var exit in exitsElement.EnumerateArray())
                {
                    if (exit.ValueKind != JsonValueKind.Object ||
                        !exit.TryGetProperty("dir", out var directionElement) ||
                        directionElement.ValueKind != JsonValueKind.String ||
                        string.IsNullOrWhiteSpace(directionElement.GetString()))
                    {
                        continue;
                    }

                    exits.Add(new RoomSnapshotExit(
                        directionElement.GetString()!.Trim(),
                        ReadOptionalString(exit, "name"),
                        ReadBoolean(exit, "door"),
                        ReadBoolean(exit, "closed")));
                }
            }

            Current = new RoomSnapshot(
                vnum,
                ReadOptionalString(root, "name"),
                ReadOptionalString(root, "sector"),
                exits);
            SnapshotReceived?.Invoke(Current);
        }
    }

    private static bool TryReadVnum(JsonElement root, out string vnum)
    {
        foreach (var propertyName in new[] { "vnum", "num" })
        {
            if (!root.TryGetProperty(propertyName, out var value))
            {
                continue;
            }

            switch (value.ValueKind)
            {
                case JsonValueKind.String when !string.IsNullOrWhiteSpace(value.GetString()):
                    vnum = value.GetString()!.Trim();
                    return true;
                case JsonValueKind.Number:
                    vnum = value.ToString();
                    return true;
            }
        }

        vnum = string.Empty;
        return false;
    }

    private static string? ReadOptionalString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    private static bool ReadBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;
}
