using System.IO;
using System.Text.Json;
using MudClient.App.Models;
using MudClient.App.Services;

namespace MudClient.App.Tests;

public sealed class AppSettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly AppSettingsService _service;

    public AppSettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "KillerMudClient_Test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _service = new AppSettingsService(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    // ====================================================================
    // Load — file does not exist → returns defaults
    // ====================================================================

    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        var settings = _service.Load();

        Assert.Equal(";", settings.CommandStackingSeparator);
        Assert.Equal("Consolas", settings.OutputFontFamily);
        Assert.Equal(14, settings.OutputFontSize);
        Assert.True(settings.OutputWordWrap);
        Assert.Equal(AppSettings.DefaultTelnetColorScheme, settings.TelnetColorScheme);
    }

    // ====================================================================
    // Load — file with null separator → normalized to default
    // ====================================================================

    [Fact]
    public void Load_NullSeparator_NormalizesToDefault()
    {
        var raw = new AppSettings { CommandStackingSeparator = null! };
        SaveRaw(raw);

        var settings = _service.Load();

        Assert.Equal(";", settings.CommandStackingSeparator);
    }

    // ====================================================================
    // Load — file with empty separator → preserved as empty
    // ====================================================================

    [Fact]
    public void Load_EmptySeparator_StaysEmpty()
    {
        var raw = new AppSettings { CommandStackingSeparator = "" };
        SaveRaw(raw);

        var settings = _service.Load();

        Assert.Equal("", settings.CommandStackingSeparator);
    }

    // ====================================================================
    // Load — file with whitespace separator → trimmed to empty
    // ====================================================================

    [Fact]
    public void Load_WhitespaceSeparator_TrimsToEmpty()
    {
        var raw = new AppSettings { CommandStackingSeparator = "  " };
        SaveRaw(raw);

        var settings = _service.Load();

        Assert.Equal("", settings.CommandStackingSeparator);
    }

    // ====================================================================
    // Load — preserves custom separator
    // ====================================================================

    [Fact]
    public void Load_CustomSeparator_Preserved()
    {
        var raw = new AppSettings { CommandStackingSeparator = "|" };
        SaveRaw(raw);

        var settings = _service.Load();

        Assert.Equal("|", settings.CommandStackingSeparator);
    }

    [Fact]
    public void Load_UnknownColorScheme_NormalizesToDefault()
    {
        SaveRaw(new AppSettings { TelnetColorScheme = "nieistniejący" });

        var settings = _service.Load();

        Assert.Equal(AppSettings.DefaultTelnetColorScheme, settings.TelnetColorScheme);
    }

    // ====================================================================
    // Save then Load round-trip
    // ====================================================================

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        var original = new AppSettings
        {
            CommandStackingSeparator = "|",
            OutputFontFamily = "Arial",
            OutputFontSize = 16,
            OutputWordWrap = false,
            AutoAssistEnabled = true,
            GroupOrdersEnabled = true,
            TelnetColorScheme = "Colorblind",
        };

        _service.Save(original);
        var loaded = _service.Load();

        Assert.Equal("|", loaded.CommandStackingSeparator);
        Assert.Equal("Arial", loaded.OutputFontFamily);
        Assert.Equal(16, loaded.OutputFontSize);
        Assert.False(loaded.OutputWordWrap);
        Assert.True(loaded.AutoAssistEnabled);
        Assert.True(loaded.GroupOrdersEnabled);
        Assert.Equal("Colorblind", loaded.TelnetColorScheme);
    }

    // ====================================================================
    // Load — corrupted JSON → returns defaults
    // ====================================================================

    [Fact]
    public void Load_CorruptedJson_ReturnsDefaults()
    {
        File.WriteAllText(Path.Combine(_tempDir, "settings.json"), "not valid json");

        var settings = _service.Load();

        Assert.Equal(";", settings.CommandStackingSeparator);
        Assert.Equal("Consolas", settings.OutputFontFamily);
        Assert.Equal(14, settings.OutputFontSize);
    }

    // ====================================================================
    // Helpers
    // ====================================================================

    private void SaveRaw(AppSettings settings)
    {
        var path = Path.Combine(_tempDir, "settings.json");
        var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }
}
