using System.IO;
using System.Text.Json;
using MudClient.App.Models;
using MudClient.App.Controls;

namespace MudClient.App.Services;

/// <summary>
/// Stores application-wide settings as a single JSON file.
/// Default location: %AppData%\KillerMudClient\settings.json.
/// </summary>
public sealed class AppSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private readonly string _path;

    public AppSettingsService(string? directory = null)
    {
        var folder = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "KillerMudClient");
        _path = Path.Combine(folder, "settings.json");
        DirectoryPath = folder;
    }

    public string DirectoryPath { get; }

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), SerializerOptions);
                if (settings is not null)
                {
                    settings.OutputFontSize = Math.Clamp(
                        settings.OutputFontSize, AppSettings.MinOutputFontSize, AppSettings.MaxOutputFontSize);
                    if (string.IsNullOrWhiteSpace(settings.OutputFontFamily))
                    {
                        settings.OutputFontFamily = AppSettings.DefaultOutputFontFamily;
                    }

                    settings.WidgetFontSize = Math.Clamp(
                        settings.WidgetFontSize, AppSettings.MinWidgetFontSize, AppSettings.MaxWidgetFontSize);
                    if (string.IsNullOrWhiteSpace(settings.WidgetFontFamily))
                    {
                        settings.WidgetFontFamily = AppSettings.DefaultWidgetFontFamily;
                    }

                    if (!AnsiColorPalette.IsKnown(settings.TelnetColorScheme))
                    {
                        settings.TelnetColorScheme = AppSettings.DefaultTelnetColorScheme;
                    }

                    // null means the property is missing from an older/corrupt settings file — use default.
                    if (settings.CommandStackingSeparator is null)
                    {
                        settings.CommandStackingSeparator = AppSettings.DefaultCommandStackingSeparator;
                    }
                    else
                    {
                        // Trim whitespace to be consistent with the UI setter in MainWindowViewModel,
                        // but preserve an explicitly-saved empty string (disables command stacking).
                        settings.CommandStackingSeparator = settings.CommandStackingSeparator.Trim();
                    }

                    settings.AutoAssistExcludedMobNames = settings.AutoAssistExcludedMobNames?
                        .Where(name => !string.IsNullOrWhiteSpace(name))
                        .Select(name => name.Trim())
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList() ?? [];
                    settings.AutoAssistFollowUpCommands ??= string.Empty;
                    NormalizeFloatingButtonSets(settings);

                    return settings;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            // Corrupted or unreadable settings — fall back to defaults.
        }

        var defaults = new AppSettings();
        NormalizeFloatingButtonSets(defaults);
        return defaults;
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, SerializerOptions));
    }

    private static List<FloatingButtonDefinition> NormalizeFloatingButtons(
        IEnumerable<FloatingButtonDefinition>? buttons)
    {
        var normalized = new List<FloatingButtonDefinition>();
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var button in buttons ?? [])
        {
            var name = button.Name?.Trim() ?? string.Empty;
            var command = button.Command?.Trim() ?? string.Empty;
            if (name.Length == 0 || command.Length == 0)
            {
                continue;
            }

            var id = button.Id?.Trim() ?? string.Empty;
            if (id.Length == 0 || !ids.Add(id))
            {
                do
                {
                    id = Guid.NewGuid().ToString("N");
                }
                while (!ids.Add(id));
            }

            normalized.Add(new FloatingButtonDefinition
            {
                Id = id,
                Name = name,
                Command = command,
                X = Math.Clamp(
                    double.IsFinite(button.X) ? button.X : 0.5,
                    0,
                    1),
                Y = Math.Clamp(
                    double.IsFinite(button.Y) ? button.Y : 0.55,
                    0,
                    1),
            });
        }

        return normalized;
    }

    private static void NormalizeFloatingButtonSets(AppSettings settings)
    {
        var normalizedSets = new List<FloatingButtonSetDefinition>();
        var setIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var set in settings.FloatingButtonSets ?? [])
        {
            var name = set.Name?.Trim() ?? string.Empty;
            if (name.Length == 0)
            {
                continue;
            }

            var id = set.Id?.Trim() ?? string.Empty;
            if (id.Length == 0 || !setIds.Add(id))
            {
                do
                {
                    id = Guid.NewGuid().ToString("N");
                }
                while (!setIds.Add(id));
            }

            normalizedSets.Add(new FloatingButtonSetDefinition
            {
                Id = id,
                Name = name,
                Buttons = NormalizeFloatingButtons(set.Buttons),
            });
        }

        if (normalizedSets.Count == 0)
        {
            normalizedSets.Add(new FloatingButtonSetDefinition
            {
                Name = AppSettings.DefaultFloatingButtonSetName,
                Buttons = NormalizeFloatingButtons(settings.FloatingButtons),
            });
        }

        settings.FloatingButtonSets = normalizedSets;
        var activeSet = normalizedSets.FirstOrDefault(set =>
                string.Equals(set.Id, settings.ActiveFloatingButtonSetId, StringComparison.Ordinal))
            ?? normalizedSets[0];
        settings.ActiveFloatingButtonSetId = activeSet.Id;
        settings.FloatingButtons = activeSet.Buttons;
    }
}
