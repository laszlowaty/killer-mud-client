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

                    return settings;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or JsonException)
        {
            // Corrupted or unreadable settings — fall back to defaults.
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        File.WriteAllText(_path, JsonSerializer.Serialize(settings, SerializerOptions));
    }
}
