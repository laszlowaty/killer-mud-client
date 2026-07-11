using System.IO;
using System.Text.Json;
using MudClient.App.Docking;

namespace MudClient.App.Services;

/// <summary>
/// Persists the panel layout (dock/tool arrangement) as JSON.
/// Default location: %AppData%\KillerMudClient\dock-layout.json.
/// </summary>
public sealed class DockLayoutService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        // Panel leaves store Proportion = NaN (not applicable to them).
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
    };

    private readonly string _path;

    public DockLayoutService(string? directory = null)
    {
        var folder = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KillerMudClient");
        _path = Path.Combine(folder, "dock-layout.json");
    }

    public DockLayoutSnapshot? Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                return JsonSerializer.Deserialize<DockLayoutSnapshot>(File.ReadAllText(_path), SerializerOptions);
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            // Corrupted or unreadable layout — fall back to the default.
        }

        return null;
    }

    public void Save(DockLayoutSnapshot snapshot)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(snapshot, SerializerOptions));
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch (IOException)
        {
            // Best-effort; a stale file will simply be overwritten next save.
        }
    }
}
