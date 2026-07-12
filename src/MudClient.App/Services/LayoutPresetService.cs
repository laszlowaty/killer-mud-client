using System.IO;
using System.Text.Json;
using MudClient.App.Docking;

namespace MudClient.App.Services;

/// <summary>A named, user-saved dock layout.</summary>
public sealed class LayoutPreset
{
    public string Name { get; set; } = string.Empty;

    public DockLayoutSnapshot Snapshot { get; set; } = new();
}

/// <summary>
/// Persists user-named dock layouts as JSON so the current arrangement can be saved
/// under a name and restored later. The built-in "DEFAULT" layout is not stored here —
/// it is always regenerated from <see cref="MudDockFactory"/> so new panels are included.
/// Default location: %AppData%\KillerMudClient\layout-presets.json.
/// </summary>
public sealed class LayoutPresetService
{
    /// <summary>Reserved name of the always-available built-in layout.</summary>
    public const string DefaultName = "DEFAULT";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        // Panel leaves store Proportion = NaN (not applicable to them).
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private readonly string _path;

    public LayoutPresetService(string? directory = null)
    {
        var folder = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KillerMudClient");
        _path = Path.Combine(folder, "layout-presets.json");
    }

    public List<LayoutPreset> Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                return JsonSerializer.Deserialize<List<LayoutPreset>>(File.ReadAllText(_path), SerializerOptions)
                    ?? new List<LayoutPreset>();
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            // Corrupted or unreadable presets — start from an empty set.
        }

        return new List<LayoutPreset>();
    }

    public void Save(IEnumerable<LayoutPreset> presets)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(presets.ToList(), SerializerOptions));
    }
}
