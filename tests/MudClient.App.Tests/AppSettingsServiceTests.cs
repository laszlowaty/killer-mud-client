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
        Assert.Equal(AppSettings.DefaultWidgetFontFamily, settings.WidgetFontFamily);
        Assert.Equal(AppSettings.DefaultWidgetFontSize, settings.WidgetFontSize);
        Assert.True(settings.OutputWordWrap);
        Assert.True(settings.ShowTerminalVitalsBars);
        Assert.False(settings.ClearCommandInputAfterSend);
        Assert.False(settings.LordModeEnabled);
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

    [Fact]
    public void Load_AutoAssistExclusions_TrimsAndRemovesEmptyDuplicates()
    {
        SaveRaw(new AppSettings
        {
            AutoAssistExcludedMobNames = ["  Wielki smok  ", "", "wielki SMOK", "Ork"],
        });

        var settings = _service.Load();

        Assert.Equal(["Wielki smok", "Ork"], settings.AutoAssistExcludedMobNames);
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
            OutputFontBold = true,
            WidgetFontFamily = "Verdana",
            WidgetFontSize = 15,
            WidgetFontBold = true,
            OutputWordWrap = false,
            ShowTerminalVitalsBars = false,
            ClearCommandInputAfterSend = true,
            AutoAssistEnabled = true,
            AutoAssistExcludedMobNames = ["Wielki smok", "Ork"],
            AutoAssistFollowUpCommands = "wesprzyj;czar 'ochrona'",
            GroupOrdersEnabled = true,
            ShowGroupMembersAsNumbers = true,
            LordModeEnabled = true,
            TelnetColorScheme = "Colorblind",
        };

        _service.Save(original);
        var loaded = _service.Load();

        Assert.Equal("|", loaded.CommandStackingSeparator);
        Assert.Equal("Arial", loaded.OutputFontFamily);
        Assert.Equal(16, loaded.OutputFontSize);
        Assert.True(loaded.OutputFontBold);
        Assert.Equal("Verdana", loaded.WidgetFontFamily);
        Assert.Equal(15, loaded.WidgetFontSize);
        Assert.True(loaded.WidgetFontBold);
        Assert.False(loaded.OutputWordWrap);
        Assert.False(loaded.ShowTerminalVitalsBars);
        Assert.True(loaded.ClearCommandInputAfterSend);
        Assert.True(loaded.AutoAssistEnabled);
        Assert.Equal(["Wielki smok", "Ork"], loaded.AutoAssistExcludedMobNames);
        Assert.Equal("wesprzyj;czar 'ochrona'", loaded.AutoAssistFollowUpCommands);
        Assert.True(loaded.GroupOrdersEnabled);
        Assert.True(loaded.ShowGroupMembersAsNumbers);
        Assert.True(loaded.LordModeEnabled);
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

    [Fact]
    public void Load_InvalidWidgetFont_NormalizesToDefaultsAndRange()
    {
        SaveRaw(new AppSettings { WidgetFontFamily = "  ", WidgetFontSize = 100 });

        var settings = _service.Load();

        Assert.Equal(AppSettings.DefaultWidgetFontFamily, settings.WidgetFontFamily);
        Assert.Equal(AppSettings.MaxWidgetFontSize, settings.WidgetFontSize);
    }

    [Fact]
    public void Load_OutOfRangeOverlayOpacity_ClampsToLimits()
    {
        SaveRaw(new AppSettings { TerminalOverlayOpacity = 5 });

        var settings = _service.Load();

        Assert.Equal(AppSettings.MaxTerminalOverlayOpacity, settings.TerminalOverlayOpacity);
    }

    [Fact]
    public void Load_WhitespaceOverlayPanelId_IsDropped()
    {
        SaveRaw(new AppSettings
        {
            TerminalOverlays = [new TerminalOverlayEntry { PanelId = "   " }],
        });

        var settings = _service.Load();

        Assert.Empty(settings.TerminalOverlays);
    }

    [Fact]
    public void Load_DuplicateOverlayPanelIds_KeepsFirstOnly()
    {
        SaveRaw(new AppSettings
        {
            TerminalOverlays =
            [
                new TerminalOverlayEntry { PanelId = "Notes", HeightWeight = 2 },
                new TerminalOverlayEntry { PanelId = "Notes", HeightWeight = 3 },
            ],
        });

        var settings = _service.Load();

        var overlay = Assert.Single(settings.TerminalOverlays);
        Assert.Equal("Notes", overlay.PanelId);
        Assert.Equal(2, overlay.HeightWeight, precision: 6);
    }

    [Fact]
    public void Load_OutOfRangeOverlayHeightWeight_ClampsToLimits()
    {
        SaveRaw(new AppSettings
        {
            TerminalOverlays = [new TerminalOverlayEntry { PanelId = "Notes", HeightWeight = 50 }],
        });

        var settings = _service.Load();

        var overlay = Assert.Single(settings.TerminalOverlays);
        Assert.Equal(AppSettings.MaxTerminalOverlayHeightWeight, overlay.HeightWeight);
    }

    [Fact]
    public void Load_OutOfRangeColumnWidthFraction_ClampsToLimits()
    {
        SaveRaw(new AppSettings { TerminalOverlayColumnWidthFraction = 5 });

        var settings = _service.Load();

        Assert.Equal(AppSettings.MaxTerminalOverlayColumnWidthFraction, settings.TerminalOverlayColumnWidthFraction);
    }

    [Fact]
    public void SaveAndLoad_OverlaySettings_RoundTrip()
    {
        var original = new AppSettings
        {
            TerminalOverlays =
            [
                new TerminalOverlayEntry { PanelId = "Notes", HeightWeight = 1.5 },
                new TerminalOverlayEntry { PanelId = "Group", HeightWeight = 0.5 },
            ],
            TerminalOverlayOpacity = 0.6,
            TerminalOverlayColumnWidthFraction = 0.3,
        };

        _service.Save(original);
        var loaded = _service.Load();

        Assert.Equal(2, loaded.TerminalOverlays.Count);
        Assert.Equal("Notes", loaded.TerminalOverlays[0].PanelId);
        Assert.Equal(1.5, loaded.TerminalOverlays[0].HeightWeight, precision: 6);
        Assert.Equal("Group", loaded.TerminalOverlays[1].PanelId);
        Assert.Equal(0.5, loaded.TerminalOverlays[1].HeightWeight, precision: 6);
        Assert.Equal(0.6, loaded.TerminalOverlayOpacity, precision: 6);
        Assert.Equal(0.3, loaded.TerminalOverlayColumnWidthFraction, precision: 6);
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
