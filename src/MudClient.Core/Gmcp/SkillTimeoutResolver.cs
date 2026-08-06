using System.Text.Json;

namespace MudClient.Core.Gmcp;

/// <summary>A single skill's on/off-cooldown state from Skills.Timeout GMCP.</summary>
public sealed record SkillTimeoutEntry(string Name, bool Timeout);

/// <summary>
/// Translates Skills.Timeout GMCP messages — a snapshot of skills currently tracked for
/// cooldown, keyed by skill name (e.g. { "torment": { "timeout": true } }) — into typed
/// updates. Malformed or unknown messages are ignored.
/// </summary>
public sealed class SkillTimeoutResolver
{
    public event Action<IReadOnlyList<SkillTimeoutEntry>>? TimeoutsChanged;

    public void Process(GmcpMessage message)
    {
        if (!string.Equals(message.Package, "Skills.Timeout", StringComparison.OrdinalIgnoreCase))
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

                var timeout = property.Value.TryGetProperty("timeout", out var timeoutValue)
                              && timeoutValue.ValueKind == JsonValueKind.True;

                entries.Add(new SkillTimeoutEntry(name, timeout));
            }

            TimeoutsChanged?.Invoke(entries);
        }
    }
}
