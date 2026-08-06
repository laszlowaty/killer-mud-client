using System.Text.Json;

namespace MudClient.Core.Gmcp;

/// <summary>A single skill's on/off-cooldown state from Char.Skills.Timeout GMCP.</summary>
public sealed record SkillTimeoutEntry(string Name, bool Timeout);

/// <summary>
/// Translates Char.Skills.Timeout GMCP messages — a snapshot of skills currently tracked for
/// cooldown, keyed by skill name (e.g. { "holy prayer": { "timeout": true } }) — into typed
/// updates. Malformed or unknown messages are ignored.
/// </summary>
public sealed class SkillTimeoutResolver
{
    public event Action<IReadOnlyList<SkillTimeoutEntry>>? TimeoutsChanged;

    public void Process(GmcpMessage message)
    {
        if (!string.Equals(message.Package, "Char.Skills.Timeout", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(message.Json))
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
            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            var entries = new List<SkillTimeoutEntry>();
            foreach (var property in root.EnumerateObject())
            {
                var name = property.Name.Trim();
                if (name.Length == 0 || property.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!property.Value.TryGetProperty("timeout", out var timeoutValue))
                {
                    continue;
                }

                entries.Add(new SkillTimeoutEntry(name, ReadBool(timeoutValue)));
            }

            TimeoutsChanged?.Invoke(entries);
        }
    }

    /// <summary>
    /// The server has been observed sending "timeout" as both a real JSON boolean and as the
    /// string "true"/"false" — accept either.
    /// </summary>
    private static bool ReadBool(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.String => bool.TryParse(value.GetString(), out var parsed) && parsed,
        _ => false,
    };
}
