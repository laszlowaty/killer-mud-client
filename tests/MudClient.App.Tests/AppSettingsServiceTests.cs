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
        Assert.True(settings.ShowNumericCharacterStatRanges);
        Assert.True(settings.ShowNumericCombatDamage);
        Assert.True(settings.ShowTerminalVitalsBars);
        Assert.False(settings.ClearCommandInputAfterSend);
        Assert.False(settings.LordModeEnabled);
        Assert.Equal(AppSettings.DefaultTelnetColorScheme, settings.TelnetColorScheme);
        Assert.Equal(AppSettings.DefaultMobileControlsOpacity, settings.MobileControlsOpacity);
        Assert.Equal(
            AppSettings.DefaultMobileFloatingButtonScale,
            settings.MobileFloatingButtonScale);
        Assert.Equal(
            AppSettings.DefaultMobileMovementButtonScale,
            settings.MobileMovementButtonScale);
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

    [Theory]
    [InlineData(-1, AppSettings.MinMobileControlsOpacity)]
    [InlineData(2, AppSettings.MaxMobileControlsOpacity)]
    public void Load_MobileControlsOpacity_ClampsToSupportedRange(
        double rawOpacity,
        double expectedOpacity)
    {
        SaveRaw(new AppSettings { MobileControlsOpacity = rawOpacity });

        var settings = _service.Load();

        Assert.Equal(expectedOpacity, settings.MobileControlsOpacity);
    }

    [Theory]
    [InlineData(-1, AppSettings.MinMobileButtonScale)]
    [InlineData(2, AppSettings.MaxMobileButtonScale)]
    public void Load_MobileButtonScales_ClampToSupportedRange(
        double rawScale,
        double expectedScale)
    {
        SaveRaw(new AppSettings
        {
            MobileFloatingButtonScale = rawScale,
            MobileMovementButtonScale = rawScale,
        });

        var settings = _service.Load();

        Assert.Equal(expectedScale, settings.MobileFloatingButtonScale);
        Assert.Equal(expectedScale, settings.MobileMovementButtonScale);
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
            ShowNumericCharacterStatRanges = false,
            ShowNumericCombatDamage = false,
            ShowTerminalVitalsBars = false,
            ClearCommandInputAfterSend = true,
            AutoAssistEnabled = true,
            AutoAssistExcludedMobNames = ["Wielki smok", "Ork"],
            AutoAssistFollowUpCommands = "wesprzyj;czar 'ochrona'",
            AutowalkUseRefreshes = true,
            AutowalkUseRecuperate = true,
            GroupOrdersEnabled = true,
            ShowGroupMembersAsNumbers = true,
            LordModeEnabled = true,
            TelnetColorScheme = "Colorblind",
            FloatingButtons =
            [
                new FloatingButtonDefinition
                {
                    Id = "heal",
                    Name = "Leczenie",
                    Command = "quaff red",
                    X = 0.25,
                    Y = 0.75,
                },
            ],
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
        Assert.False(loaded.ShowNumericCharacterStatRanges);
        Assert.False(loaded.ShowNumericCombatDamage);
        Assert.False(loaded.ShowTerminalVitalsBars);
        Assert.True(loaded.ClearCommandInputAfterSend);
        Assert.True(loaded.AutoAssistEnabled);
        Assert.Equal(["Wielki smok", "Ork"], loaded.AutoAssistExcludedMobNames);
        Assert.Equal("wesprzyj;czar 'ochrona'", loaded.AutoAssistFollowUpCommands);
        Assert.True(loaded.AutowalkUseRefreshes);
        Assert.True(loaded.AutowalkUseRecuperate);
        Assert.True(loaded.GroupOrdersEnabled);
        Assert.True(loaded.ShowGroupMembersAsNumbers);
        Assert.True(loaded.LordModeEnabled);
        Assert.Equal("Colorblind", loaded.TelnetColorScheme);
        var floatingButton = Assert.Single(loaded.FloatingButtons);
        Assert.Equal("heal", floatingButton.Id);
        Assert.Equal("Leczenie", floatingButton.Name);
        Assert.Equal("quaff red", floatingButton.Command);
        Assert.Equal(0.25, floatingButton.X);
        Assert.Equal(0.75, floatingButton.Y);
    }

    [Fact]
    public void Load_FloatingButtons_NormalizesInvalidEntriesAndPositions()
    {
        SaveRaw(new AppSettings
        {
            FloatingButtons =
            [
                new FloatingButtonDefinition
                {
                    Id = "duplicate",
                    Name = "  Atak  ",
                    Command = "  kill ork  ",
                    X = -2,
                    Y = 3,
                },
                new FloatingButtonDefinition
                {
                    Id = "duplicate",
                    Name = "Obrona",
                    Command = "rescue tank",
                },
                new FloatingButtonDefinition
                {
                    Name = "Bez komendy",
                    Command = " ",
                },
            ],
        });

        var buttons = _service.Load().FloatingButtons;

        Assert.Equal(2, buttons.Count);
        Assert.Equal("Atak", buttons[0].Name);
        Assert.Equal("kill ork", buttons[0].Command);
        Assert.Equal(0, buttons[0].X);
        Assert.Equal(1, buttons[0].Y);
        Assert.NotEqual(buttons[0].Id, buttons[1].Id);
    }

    [Fact]
    public void Load_LegacyFloatingButtons_MigratesToDefaultSet()
    {
        SaveRaw(new AppSettings
        {
            FloatingButtons =
            [
                new FloatingButtonDefinition
                {
                    Name = "Atak",
                    Command = "kill ork",
                },
            ],
        });

        var settings = _service.Load();

        var set = Assert.Single(settings.FloatingButtonSets);
        Assert.Equal(AppSettings.DefaultFloatingButtonSetName, set.Name);
        Assert.Equal(set.Id, settings.ActiveFloatingButtonSetId);
        Assert.Same(set.Buttons, settings.FloatingButtons);
        Assert.Equal("Atak", Assert.Single(set.Buttons).Name);
    }

    [Fact]
    public void Load_FloatingButtonSets_NormalizesSetsAndSelectsValidActiveSet()
    {
        SaveRaw(new AppSettings
        {
            ActiveFloatingButtonSetId = "missing",
            FloatingButtonSets =
            [
                new FloatingButtonSetDefinition
                {
                    Id = "combat",
                    Name = "  Walka  ",
                    Buttons =
                    [
                        new FloatingButtonDefinition
                        {
                            Name = "Atak",
                            Command = "kill ork",
                        },
                    ],
                },
                new FloatingButtonSetDefinition
                {
                    Id = "combat",
                    Name = "Podróż",
                },
                new FloatingButtonSetDefinition { Name = " " },
            ],
        });

        var settings = _service.Load();

        Assert.Equal(2, settings.FloatingButtonSets.Count);
        Assert.Equal("Walka", settings.FloatingButtonSets[0].Name);
        Assert.NotEqual(
            settings.FloatingButtonSets[0].Id,
            settings.FloatingButtonSets[1].Id);
        Assert.Equal(settings.FloatingButtonSets[0].Id, settings.ActiveFloatingButtonSetId);
        Assert.Same(settings.FloatingButtonSets[0].Buttons, settings.FloatingButtons);
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
