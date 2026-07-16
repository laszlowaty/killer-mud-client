using System.Text.Json;

namespace MudClient.Core.Gmcp;

/// <summary>Fields from Char.Vitals; null when the message did not include them.</summary>
public sealed record CharacterVitalsUpdate(
    int? Hp,
    int? MaxHp,
    int? Mv,
    int? MaxMv,
    int? Mem,
    string? Name,
    string? Sex,
    int? Level,
    string? Position);

/// <summary>Fields from Char.Condition. Flags holds the raw boolean entries (e.g. "hungry" → true).</summary>
public sealed record CharacterConditionUpdate(
    string? Position,
    IReadOnlyDictionary<string, bool> Flags);

/// <summary>Someone visible in the room (player or NPC). Enemy is null unless they fight.</summary>
public sealed record RoomPerson(string Name, bool IsFighting, string? Enemy);

/// <summary>A single member of the group from Char.Group GMCP.</summary>
public sealed record CharacterGroupMember(
    string Name,
    string? Position,
    string HpText,
    int? HpScale,
    string MvText,
    int? MvScale,
    int? Mem,
    bool IsNpc,
    string? Room,
    bool IsLeader);

/// <summary>Full group state from Char.Group GMCP.</summary>
public sealed record CharacterGroupUpdate(
    string? Leader,
    IReadOnlyList<CharacterGroupMember> Members);

/// <summary>A single memorized spell slot from Char.MemSpell GMCP.</summary>
public sealed record MemorizedSpell(
    int Counter,
    int Circle,
    string Name,
    bool Memed,
    bool Meming);

/// <summary>A single affect from Char.Affects GMCP.</summary>
public sealed record CharacterAffect(
    string Name,
    string Description,
    bool Negative,
    bool Ending,
    string? ExtraValue);

/// <summary>
/// Translates Char.Vitals / Char.Condition / Room.People / Char.Group / Char.MemSpell GMCP
/// messages into typed updates. Malformed or unknown messages are ignored.
/// </summary>
public sealed class CharacterStateResolver
{
    public event Action<CharacterVitalsUpdate>? VitalsChanged;

    public event Action<CharacterConditionUpdate>? ConditionChanged;

    public event Action<IReadOnlyList<RoomPerson>>? PeopleChanged;

    public event Action<CharacterGroupUpdate>? GroupChanged;

    public event Action<IReadOnlyList<CharacterAffect>>? AffectsChanged;

    public event Action<IReadOnlyList<MemorizedSpell>>? MemSpellsChanged;

    public void Process(GmcpMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Json))
        {
            return;
        }

        var isVitals = string.Equals(message.Package, "Char.Vitals", StringComparison.OrdinalIgnoreCase);
        var isCondition = string.Equals(message.Package, "Char.Condition", StringComparison.OrdinalIgnoreCase);
        var isGroup = string.Equals(message.Package, "Char.Group", StringComparison.OrdinalIgnoreCase);
        var isAffects = string.Equals(message.Package, "Char.Affects", StringComparison.OrdinalIgnoreCase);
        var isMemSpells = string.Equals(message.Package, "Char.MemSpell", StringComparison.OrdinalIgnoreCase);
        // Room.Info carries the same people array on this server; its object-shaped
        // variant (room metadata) is handled by GmcpLocationResolver instead.
        var isPeople = string.Equals(message.Package, "Room.People", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(message.Package, "Room.Info", StringComparison.OrdinalIgnoreCase);
        if (!isVitals && !isCondition && !isPeople && !isGroup && !isAffects && !isMemSpells)
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

            if (isPeople)
            {
                if (root.ValueKind == JsonValueKind.Array)
                {
                    PeopleChanged?.Invoke(ParsePeople(root));
                }

                return;
            }

            if (isAffects)
            {
                if (root.ValueKind == JsonValueKind.Array)
                {
                    AffectsChanged?.Invoke(ParseAffects(root));
                }

                return;
            }

            if (isMemSpells)
            {
                if (root.ValueKind == JsonValueKind.Array)
                {
                    MemSpellsChanged?.Invoke(ParseMemSpells(root));
                }

                return;
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (isGroup)
            {
                ParseGroup(root);
                return;
            }

            if (isVitals)
            {
                VitalsChanged?.Invoke(new CharacterVitalsUpdate(
                    Hp: GetInt(root, "hp"),
                    MaxHp: GetInt(root, "max_hp"),
                    Mv: GetInt(root, "mv"),
                    MaxMv: GetInt(root, "max_mv"),
                    Mem: GetInt(root, "mem"),
                    Name: GetString(root, "name"),
                    Sex: GetString(root, "sex"),
                    Level: GetInt(root, "level"),
                    Position: NormalizePosition(GetString(root, "pos"))));
            }
            else
            {
                var flags = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in root.EnumerateObject())
                {
                    if (property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    {
                        flags[property.Name] = property.Value.GetBoolean();
                    }
                }

                ConditionChanged?.Invoke(new CharacterConditionUpdate(
                    Position: NormalizePosition(GetString(root, "position")),
                    Flags: flags));
            }
        }
    }

    private static List<RoomPerson> ParsePeople(JsonElement array)
    {
        var people = new List<RoomPerson>();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = GetString(element, "name")?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var isFighting = element.TryGetProperty("is_fighting", out var fighting)
                             && fighting.ValueKind == JsonValueKind.True;

            // The server pads "enemy" with spaces when there is none.
            var enemy = GetString(element, "enemy")?.Trim();
            people.Add(new RoomPerson(
                name,
                isFighting,
                string.IsNullOrEmpty(enemy) ? null : enemy));
        }

        return people;
    }

    private static List<MemorizedSpell> ParseMemSpells(JsonElement array)
    {
        var spells = new List<MemorizedSpell>();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = GetString(element, "name")?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var memed = element.TryGetProperty("memed", out var memedProp)
                        && memedProp.ValueKind == JsonValueKind.True;
            var meming = element.TryGetProperty("meming", out var memingProp)
                         && memingProp.ValueKind == JsonValueKind.True;

            spells.Add(new MemorizedSpell(
                Counter: GetInt(element, "counter") ?? 0,
                Circle: GetInt(element, "circle") ?? 0,
                Name: name,
                Memed: memed,
                Meming: meming));
        }

        return spells;
    }

    private static List<CharacterAffect> ParseAffects(JsonElement array)
    {
        var affects = new List<CharacterAffect>();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = GetString(element, "name")?.Trim();
            var description = GetString(element, "desc") ?? string.Empty;

            // Some affects come with an empty "name" and carry the real label in "desc"
            // (e.g. curses/wounds) — fall back to desc so they aren't silently dropped.
            if (string.IsNullOrEmpty(name))
            {
                name = description.Trim();
            }

            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var negative = element.TryGetProperty("negative", out var negProp)
                           && negProp.ValueKind == JsonValueKind.True;

            var ending = element.TryGetProperty("ending", out var endProp)
                         && (endProp.ValueKind == JsonValueKind.True
                             || endProp.ValueKind == JsonValueKind.String
                             && string.Equals(endProp.GetString(), "!", StringComparison.Ordinal));

            var extraValue = ParseExtraValue(element, "extraValue");

            affects.Add(new CharacterAffect(name, description, negative, ending, extraValue));
        }

        return affects;
    }

    /// <summary>
    /// Converts extraValue to a displayable string:
    ///   string/number/bool → their string representation;
    ///   JSON null / missing / object / array → null.
    /// </summary>
    private static string? ParseExtraValue(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null,
        };
    }

    /// <summary>Maps "POS_STANDING" and "standing" alike to "standing".</summary>
    private static string? NormalizePosition(string? position)
    {
        if (string.IsNullOrWhiteSpace(position))
        {
            return null;
        }

        var normalized = position.Trim();
        if (normalized.StartsWith("POS_", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[4..];
        }

        return normalized.ToLowerInvariant();
    }

    private static int? GetInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.Number
        && value.TryGetInt32(out var intValue)
            ? intValue
            : null;

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>
    /// Reads a room value that may be a JSON string ("6017") or a JSON number (6017).
    /// Returns null for any other value kind or if the property is missing.
    /// </summary>
    private static string? GetRoomString(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }

    private void ParseGroup(JsonElement root)
    {
        // A valid group message must have a "members" array. Anything else
        // (missing field or non-array value) is malformed and silently ignored.
        if (!root.TryGetProperty("members", out var membersElement)
            || membersElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var leader = GetString(root, "leader")?.Trim();
        if (string.IsNullOrEmpty(leader))
        {
            leader = null;
        }

        var members = new List<CharacterGroupMember>();
        foreach (var element in membersElement.EnumerateArray())
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var name = GetString(element, "name")?.Trim();
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            var hpText = GetString(element, "hp") ?? string.Empty;
            var mvText = GetString(element, "mv") ?? string.Empty;
            var position = NormalizePosition(GetString(element, "pos"));
            var mem = GetInt(element, "mem");
            var room = GetRoomString(element, "room")?.Trim();
            var isNpc = element.TryGetProperty("is_npc", out var npcProp)
                        && npcProp.ValueKind == JsonValueKind.True;

            members.Add(new CharacterGroupMember(
                Name: name,
                Position: position,
                HpText: hpText,
                HpScale: MapHpText(hpText),
                MvText: mvText,
                MvScale: MapMvText(mvText),
                Mem: mem,
                IsNpc: isNpc,
                Room: room,
                IsLeader: leader != null
                          && string.Equals(name, leader, StringComparison.OrdinalIgnoreCase)));
        }

        GroupChanged?.Invoke(new CharacterGroupUpdate(leader, members));
    }

    // ========================================================================
    // HP / MV Polish-text → numeric scale mapping
    // ========================================================================

    /// <summary>
    /// Strips Polish diacritics and lowercases so both "żadnych śladów" and
    /// "zadnych sladow" map to the same normalized key.
    /// </summary>
    private static string NormalizeDiacritics(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var result = text.Trim();
        result = result.Replace('ą', 'a')
                       .Replace('ć', 'c')
                       .Replace('ę', 'e')
                       .Replace('ł', 'l')
                       .Replace('ń', 'n')
                       .Replace('ó', 'o')
                       .Replace('ś', 's')
                       .Replace('ź', 'z')
                       .Replace('ż', 'z')
                       .Replace('Ą', 'A')
                       .Replace('Ć', 'C')
                       .Replace('Ę', 'E')
                       .Replace('Ł', 'L')
                       .Replace('Ń', 'N')
                       .Replace('Ó', 'O')
                       .Replace('Ś', 'S')
                       .Replace('Ź', 'Z')
                       .Replace('Ż', 'Z');
        return result.ToLowerInvariant();
    }

    // HP levels 0..7 (0 = worst, 7 = best)
    private static readonly IReadOnlyDictionary<string, int> HpScaleMap =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["umiera"] = 0,
            ["unieruchomiony"] = 0,
            ["ledwo stoi"] = 1,
            ["ogromne rany"] = 2,
            ["ogromne uszkodzenia"] = 2,
            ["ciezkie rany"] = 3,
            ["ciezkie uszkodzenia"] = 3,
            ["srednie rany"] = 4,
            ["srednie uszkodzenia"] = 4,
            ["lekkie rany"] = 5,
            ["lekkie uszkodzenia"] = 5,
            ["zadrapania"] = 6,
            ["zadnych sladow"] = 7,
        };

    private static int? MapHpText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var key = NormalizeDiacritics(text);
        return HpScaleMap.TryGetValue(key, out var scale) ? scale : null;
    }

    // MV levels 0..4 (0 = worst, 4 = best)
    private static readonly IReadOnlyDictionary<string, int> MvScaleMap =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["zameczony"] = 0,
            ["zameczona"] = 0,
            ["bardzo zmeczony"] = 1,
            ["bardzo zmeczona"] = 1,
            ["zmeczony"] = 2,
            ["zmeczona"] = 2,
            ["lekko zmeczony"] = 3,
            ["lekko zmeczona"] = 3,
            ["wypoczety"] = 4,
            ["wypoczeta"] = 4,
        };

    private static int? MapMvText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var key = NormalizeDiacritics(text);
        return MvScaleMap.TryGetValue(key, out var scale) ? scale : null;
    }
}
