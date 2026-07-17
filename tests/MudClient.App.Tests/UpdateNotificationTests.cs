using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.App.Tests;

public sealed class UpdateNotificationTests : IAsyncDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(), "KillerMudClient-UpdateNotificationTests", Guid.NewGuid().ToString("N"));
    private MainWindowViewModel? _viewModel;

    [Fact]
    public async Task StartUpdateCheck_ShowsNotificationAndCommandsOpenBothLinks()
    {
        var update = new AvailableUpdate(
            "999.0.0-beta.3",
            true,
            new Uri("https://example.test/release"),
            new Uri("https://example.test/changelog"));
        var links = new RecordingExternalLinkService();
        _viewModel = new MainWindowViewModel(
            settingsService: new AppSettingsService(_tempDirectory),
            updateCheckService: new StubUpdateCheckService(update),
            externalLinkService: links);

        _viewModel.StartUpdateCheck();
        Assert.NotNull(_viewModel.ActiveUpdateCheck);
        await _viewModel.ActiveUpdateCheck;

        Assert.True(_viewModel.IsUpdateAvailable);
        Assert.Contains("999.0.0-beta.3 (beta)", _viewModel.UpdateNotificationText);

        _viewModel.OpenUpdateReleaseCommand.Execute(null);
        _viewModel.OpenChangelogCommand.Execute(null);

        Assert.Equal([update.ReleasePageUri, update.ChangelogUri], links.OpenedUris);

        _viewModel.DismissUpdateCommand.Execute(null);
        Assert.False(_viewModel.IsUpdateAvailable);
    }

    [Fact]
    public void OpenDiscordCommand_OpensKillerMudInvite()
    {
        var links = new RecordingExternalLinkService();
        _viewModel = new MainWindowViewModel(
            settingsService: new AppSettingsService(_tempDirectory),
            externalLinkService: links);

        _viewModel.OpenDiscordCommand.Execute(null);

        Assert.Equal(
            new Uri("https://discord.gg/6NRnxZeMTC"),
            Assert.Single(links.OpenedUris));
    }

    public async ValueTask DisposeAsync()
    {
        if (_viewModel is not null)
        {
            await _viewModel.DisposeAsync();
        }

        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private sealed class StubUpdateCheckService(AvailableUpdate update) : IUpdateCheckService
    {
        public Task<AvailableUpdate?> CheckForUpdateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<AvailableUpdate?>(update);
    }

    private sealed class RecordingExternalLinkService : IExternalLinkService
    {
        public List<Uri> OpenedUris { get; } = [];

        public void Open(Uri uri) => OpenedUris.Add(uri);
    }
}
