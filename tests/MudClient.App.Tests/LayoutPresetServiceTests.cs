using MudClient.App.Docking;
using MudClient.App.Services;

namespace MudClient.App.Tests;

public sealed class LayoutPresetServiceTests : IDisposable
{
    private readonly string _tempDir = Directory.CreateTempSubdirectory("layout-preset-tests-").FullName;

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDir, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void Load_CorruptedPrimary_RecoversPreviousCompletePresets()
    {
        var service = new LayoutPresetService(_tempDir);
        service.Save([new LayoutPreset { Name = "pierwszy", Snapshot = new DockLayoutSnapshot() }]);
        service.Save([new LayoutPreset { Name = "drugi", Snapshot = new DockLayoutSnapshot() }]);
        File.WriteAllText(Path.Combine(_tempDir, "layout-presets.json"), "{ urwany zapis");

        var loaded = service.Load();

        Assert.Equal("pierwszy", Assert.Single(loaded).Name);
    }
}
