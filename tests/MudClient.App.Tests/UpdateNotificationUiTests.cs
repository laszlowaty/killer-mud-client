using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.App.ViewModels;
using MudClient.App.Views;

namespace MudClient.App.Tests;

[Collection(AvaloniaUiCollection.Name)]
public sealed class UpdateNotificationUiTests : IAsyncDisposable
{
    private readonly string _tempDirectory = Path.Combine(
        Path.GetTempPath(), "KillerMudClient-UpdateNotificationUiTests", Guid.NewGuid().ToString("N"));
    private MainWindow? _window;
    private MainWindowViewModel? _viewModel;

    [AvaloniaFact]
    public void MainWindow_ShowsDiscordButtonInTopBar()
    {
        _viewModel = new MainWindowViewModel(
            settingsService: new AppSettingsService(_tempDirectory));
        _window = new MainWindow { DataContext = _viewModel };
        _window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        _window.UpdateLayout();

        Assert.Contains(
            _window.GetVisualDescendants().OfType<Button>(),
            button => button.IsEffectivelyVisible && button.Content?.ToString() == "discord");
    }

    [AvaloniaFact]
    public async Task AvailableUpdate_ShowsPersistentBannerWithActions()
    {
        var update = new AvailableUpdate(
            "999.0.0",
            false,
            new Uri("https://example.test/release"),
            new Uri("https://example.test/changelog"));
        _viewModel = new MainWindowViewModel(
            settingsService: new AppSettingsService(_tempDirectory),
            updateCheckService: new StubUpdateCheckService(update));
        _window = new MainWindow { DataContext = _viewModel };
        _window.Show();

        _viewModel.StartUpdateCheck();
        Assert.NotNull(_viewModel.ActiveUpdateCheck);
        await _viewModel.ActiveUpdateCheck;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        _window.UpdateLayout();

        var visibleTexts = _window.GetVisualDescendants()
            .OfType<TextBlock>()
            .Where(text => text.IsEffectivelyVisible)
            .Select(text => text.Text)
            .ToList();
        var visibleButtons = _window.GetVisualDescendants()
            .OfType<Button>()
            .Where(button => button.IsEffectivelyVisible)
            .Select(button => button.Content?.ToString())
            .ToList();

        Assert.Contains("Dostępna jest wersja 999.0.0.", visibleTexts);
        Assert.Contains("Pobierz", visibleButtons);
        Assert.Contains("Lista zmian", visibleButtons);
    }

    public async ValueTask DisposeAsync()
    {
        if (_window is not null)
        {
            _window.DataContext = null;
            _window.Close();
        }

        Dispatcher.UIThread.RunJobs();
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
}
