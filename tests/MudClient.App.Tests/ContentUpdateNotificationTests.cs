using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.App.Tests;

public sealed class ContentUpdateNotificationTests : IAsyncDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "KillerMudClient-ContentNotificationTests", Guid.NewGuid().ToString("N"));
    private MainWindowViewModel? _viewModel;

    [Fact]
    public async Task StartUpdateCheck_OffersContentPackageWithoutReplacingApplication()
    {
        var available = new ContentUpdateAvailability(
            "2026.07.18.1",
            [new ContentComponentUpdate(
                "killeropedia",
                "2026.07.18.1",
                new Uri("https://example.test/killeropedia.zip"),
                2 * 1024 * 1024,
                new string('a', 64))]);
        var contentService = new RecordingContentUpdateService(available);
        _viewModel = new MainWindowViewModel(
            settingsService: new AppSettingsService(_directory),
            updateCheckService: new NoApplicationUpdateService(),
            contentUpdateService: contentService);

        _viewModel.StartUpdateCheck();
        Assert.NotNull(_viewModel.ActiveContentUpdateCheck);
        await _viewModel.ActiveContentUpdateCheck;

        Assert.True(_viewModel.IsContentUpdateAvailable);
        Assert.Contains("Killeropedia", _viewModel.ContentUpdateDescription);
        Assert.Contains("2026.07.18.1", _viewModel.ContentUpdateDescription);
        Assert.Contains("2 MB", _viewModel.ContentUpdateDescription);
        Assert.True(_viewModel.InstallContentUpdateCommand.CanExecute(null));
    }

    public async ValueTask DisposeAsync()
    {
        if (_viewModel is not null)
        {
            await _viewModel.DisposeAsync();
        }

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class RecordingContentUpdateService(ContentUpdateAvailability update)
        : IContentUpdateService
    {
        public Task<ContentUpdateAvailability?> CheckForUpdateAsync(
            CancellationToken cancellationToken = default) => Task.FromResult<ContentUpdateAvailability?>(update);

        public Task<ContentInstallResult> InstallAsync(
            ContentUpdateAvailability selectedUpdate,
            IProgress<ContentUpdateProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ContentInstallResult(
                selectedUpdate.Release,
                selectedUpdate.Components.Select(component => component.Name).ToArray()));
        }
    }

    private sealed class NoApplicationUpdateService : IUpdateCheckService
    {
        public Task<AvailableUpdate?> CheckForUpdateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<AvailableUpdate?>(null);
    }
}
