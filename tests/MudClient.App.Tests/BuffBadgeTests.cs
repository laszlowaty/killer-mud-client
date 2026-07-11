using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.App.Tests;

public sealed class BuffBadgeTests : IAsyncDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), "MudClientTests", Guid.NewGuid().ToString("N"));

    private readonly MainWindowViewModel _vm;

    public BuffBadgeTests()
    {
        Directory.CreateDirectory(_tempDir);
        _vm = new MainWindowViewModel(
            profileService: new ProfileService(_tempDir),
            settingsService: new AppSettingsService(_tempDir));
    }

    public async ValueTask DisposeAsync()
    {
        await _vm.DisposeAsync();
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void BuffsBadge_ReflectsRequiredBuffCounts()
    {
        Assert.Equal("0", _vm.BuffsBadge);
        Assert.False(_vm.BuffsAlert);

        _vm.NewBuffName = "armor";
        _vm.AddBuffCommand.Execute(null);

        // No Char.Affects received yet, so the buff counts as missing.
        Assert.Equal("0/1", _vm.BuffsBadge);
        Assert.True(_vm.BuffsAlert);
    }
}
