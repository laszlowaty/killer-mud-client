using System.Text.Json;
using MudClient.Core.Gmcp;

namespace MudClient.Core.Map;

/// <summary>One exit of the current room as reported by GMCP Room.Info.</summary>
public sealed record RoomExitInfo(string Dir, string? Name, bool HasDoor, bool IsClosed);

/// <summary>
/// Tracks the exits of the current room from object-shaped GMCP Room.Info
/// messages (the array-shaped Room.Info variant carries people and is
/// handled by CharacterStateResolver).
/// </summary>
public sealed class RoomExitsResolver
{
    public IReadOnlyList<RoomExitInfo> CurrentExits { get; private set; } = [];

    public event Action<IReadOnlyList<RoomExitInfo>>? ExitsChanged;

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
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("exits", out var exits) ||
                exits.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            var parsed = new List<RoomExitInfo>();
            foreach (var exit in exits.EnumerateArray())
            {
                if (exit.ValueKind != JsonValueKind.Object ||
                    !exit.TryGetProperty("dir", out var dir) ||
                    dir.ValueKind != JsonValueKind.String ||
                    string.IsNullOrWhiteSpace(dir.GetString()))
                {
                    continue;
                }

                var name = exit.TryGetProperty("name", out var nameElement) &&
                           nameElement.ValueKind == JsonValueKind.String
                    ? nameElement.GetString()
                    : null;

                parsed.Add(new RoomExitInfo(
                    dir.GetString()!,
                    string.IsNullOrWhiteSpace(name) ? null : name,
                    HasDoor: ReadBool(exit, "door"),
                    IsClosed: ReadBool(exit, "closed")));
            }

            CurrentExits = parsed;
            ExitsChanged?.Invoke(parsed);
        }
    }

    private static bool ReadBool(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
}
