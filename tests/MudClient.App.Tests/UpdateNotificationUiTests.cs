using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
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
    public void MainWindow_KeepsDiscordInOverflowMenu()
    {
        _viewModel = new MainWindowViewModel(
            settingsService: new AppSettingsService(_tempDirectory));
        _window = new MainWindow { DataContext = _viewModel };
        _window.Show();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        _window.UpdateLayout();

        var topBar = _window.FindControl<Border>("MainTopBar")!;
        var killeropedia = _window.FindControl<Button>("TopKilleropediaButton")!;
        Assert.InRange(topBar.Bounds.Height, 1, 44);
        Assert.True(killeropedia.IsEffectivelyVisible);
        Assert.Equal(HorizontalAlignment.Center, killeropedia.HorizontalAlignment);
        Assert.Equal("Killeropedia", killeropedia.Content);

        Assert.DoesNotContain(
            _window.GetVisualDescendants().OfType<Button>(),
            button => button.IsEffectivelyVisible && button.Content?.ToString() == "Discord");

        var moreActions = _window.FindControl<Button>("MoreActionsButton")!;
        moreActions.Flyout!.ShowAt(moreActions);
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        _window.UpdateLayout();

        Assert.Contains(
            _window.GetVisualDescendants().OfType<Button>(),
            button => button.IsEffectivelyVisible && button.Content?.ToString() == "Discord");
    }

    [AvaloniaFact]
    public void StandardSections_UseASingleSeparatorInsteadOfABox()
    {
        var section = new Border
        {
            Classes = { "mud-section" },
            Child = new TextBlock { Text = "Sekcja" },
        };
        _window = new MainWindow { Content = section };
        _window.Show();
        _window.UpdateLayout();

        Assert.Equal(new Thickness(0, 0, 0, 1), section.BorderThickness);
        Assert.Equal(new CornerRadius(0), section.CornerRadius);
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

        var updateButton = _window.FindControl<Button>("ApplicationUpdateButton")!;
        Assert.True(updateButton.IsEffectivelyVisible);
        updateButton.Flyout!.ShowAt(updateButton);
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

    [AvaloniaFact]
    public async Task AvailableContentUpdates_ShowVersionsAndUpdateAction()
    {
        var contentUpdate = new ContentUpdateAvailability(
            "2026.07.18.2",
            [
                new ContentComponentUpdate(
                    "map",
                    "2026.07.18.1",
                    new Uri("https://example.test/map.zip"),
                    1024,
                    new string('a', 64)),
                new ContentComponentUpdate(
                    "killeropedia",
                    "2026.07.18.2",
                    new Uri("https://example.test/killeropedia.zip"),
                    2048,
                    new string('b', 64)),
            ]);
        _viewModel = new MainWindowViewModel(
            settingsService: new AppSettingsService(_tempDirectory),
            updateCheckService: new NoApplicationUpdateService(),
            contentUpdateService: new StubContentUpdateService(contentUpdate));
        _window = new MainWindow { DataContext = _viewModel };
        _window.Show();

        _viewModel.StartUpdateCheck();
        Assert.NotNull(_viewModel.ActiveContentUpdateCheck);
        await _viewModel.ActiveContentUpdateCheck;
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        _window.UpdateLayout();

        var updateButton = _window.FindControl<Button>("ContentUpdateButton")!;
        Assert.True(updateButton.IsEffectivelyVisible);
        updateButton.Flyout!.ShowAt(updateButton);
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

        Assert.Contains(
            "mapa 2026.07.18.1 i Killeropedia 2026.07.18.2 · 3 KB",
            visibleTexts);
        Assert.Contains("Aktualizuj", visibleButtons);
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

    private sealed class NoApplicationUpdateService : IUpdateCheckService
    {
        public Task<AvailableUpdate?> CheckForUpdateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<AvailableUpdate?>(null);
    }

    private sealed class StubContentUpdateService(ContentUpdateAvailability update) : IContentUpdateService
    {
        public Task<ContentUpdateAvailability?> CheckForUpdateAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ContentUpdateAvailability?>(update);

        public Task<ContentInstallResult> InstallAsync(
            ContentUpdateAvailability selectedUpdate,
            IProgress<ContentUpdateProgress>? progress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ContentInstallResult(
                selectedUpdate.Release,
                selectedUpdate.Components.Select(component => component.Name).ToArray()));
    }
}
