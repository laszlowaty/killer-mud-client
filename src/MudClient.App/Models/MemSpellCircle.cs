using MudClient.Core.Gmcp;

namespace MudClient.App.Models;

/// <summary>
/// Memorized spells of a single magic circle, populated from Char.MemSpell GMCP.
/// Spells are aggregated by name ("cure serious ×7"); slots still being
/// memorized (meming) are shown separately from finished (memed) ones.
/// </summary>
public sealed record MemSpellCircle
{
    public int Circle { get; }
    public int MemedCount { get; }
    public int MemingCount { get; }
    public string MemedDisplay { get; }
    public string MemingDisplay { get; }

    public string Header => $"Krąg {Circle}";

    /// <summary>E.g. "7" or "5 + 2…" when some slots are still being memorized.</summary>
    public string CountDisplay =>
        MemingCount > 0 ? $"{MemedCount} + {MemingCount}…" : $"{MemedCount}";

    public bool HasMemed => MemedDisplay.Length > 0;
    public bool HasMeming => MemingDisplay.Length > 0;

    private MemSpellCircle(int circle, int memedCount, int memingCount, string memedDisplay, string memingDisplay)
    {
        Circle = circle;
        MemedCount = memedCount;
        MemingCount = memingCount;
        MemedDisplay = memedDisplay;
        MemingDisplay = memingDisplay;
    }

    /// <summary>Groups a flat slot list into per-circle rows, ordered by circle.</summary>
    public static List<MemSpellCircle> FromCore(IReadOnlyList<MemorizedSpell> spells)
    {
        var result = new List<MemSpellCircle>();
        foreach (var circleGroup in spells.GroupBy(s => s.Circle).OrderBy(g => g.Key))
        {
            var memed = circleGroup.Where(s => s.Memed && !s.Meming).ToList();
            var meming = circleGroup.Where(s => s.Meming).ToList();
            result.Add(new MemSpellCircle(
                circle: circleGroup.Key,
                memedCount: memed.Count,
                memingCount: meming.Count,
                memedDisplay: Aggregate(memed),
                memingDisplay: Aggregate(meming)));
        }

        return result;
    }

    /// <summary>"armor, cure serious ×7" — names in slot (counter) order, duplicates collapsed.</summary>
    private static string Aggregate(IEnumerable<MemorizedSpell> spells)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        foreach (var spell in spells.OrderBy(s => s.Counter))
        {
            if (counts.TryGetValue(spell.Name, out var count))
            {
                counts[spell.Name] = count + 1;
            }
            else
            {
                counts[spell.Name] = 1;
                order.Add(spell.Name);
            }
        }

        return string.Join(", ", order.Select(name =>
            counts[name] > 1 ? $"{name} ×{counts[name]}" : name));
    }
}
