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
        return DurableJsonFile.TryRead<DockLayoutSnapshot>(_path, SerializerOptions, out var snapshot)
            ? snapshot
            : null;
    }

    public void Save(DockLayoutSnapshot snapshot)
    {
        DurableJsonFile.Write(_path, snapshot, SerializerOptions);
    }

    public void Delete()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }

            var backupPath = _path + DurableJsonFile.BackupSuffix;
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }
        }
        catch (IOException)
        {
            // Best-effort; a stale file will simply be overwritten next save.
        }
    }
}
