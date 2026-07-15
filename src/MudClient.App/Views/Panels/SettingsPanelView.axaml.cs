using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MudClient.App.Controls;
using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.App.Views.Panels;

public sealed partial class SettingsPanelView : UserControl
{
    private static readonly FilePickerFileType ZipFileType = new("Archiwum ZIP")
    {
        Patterns = ["*.zip"],
        MimeTypes = ["application/zip"],
    };

    private CancellationTokenSource? _transferCancellation;

    public SettingsPanelView()
    {
        InitializeComponent();
        DetachedFromVisualTree += (_, _) => CancelTransfer();
    }

    private async void ExportSettings_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (!TryGetTransferContext(out var service, out var storageProvider, out _))
        {
            return;
        }

        var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Eksport wszystkich ustawień",
            SuggestedFileName = $"KillerMudClient-ustawienia-{DateTime.Now:yyyy-MM-dd}.zip",
            FileTypeChoices = [ZipFileType],
            DefaultExtension = "zip",
            ShowOverwritePrompt = true,
        });
        if (file is null)
        {
            return;
        }

        await RunTransferAsync(async cancellationToken =>
        {
            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            await service.ExportAsync(
                stream,
                cancellationToken,
                file.Path.IsFile ? file.Path.LocalPath : null);
            ShowStatus("Utworzono kopię całego katalogu ustawień.");
        }, "Nie udało się utworzyć kopii");
    }

    private async void ImportSettings_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (!TryGetTransferContext(out var service, out var storageProvider, out var owner))
        {
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import wszystkich ustawień",
            AllowMultiple = false,
            FileTypeFilter = [ZipFileType],
        });
        var file = files.FirstOrDefault();
        if (file is null || !await SettingsImportConfirmationDialog.ShowAsync(owner))
        {
            return;
        }

        await RunTransferAsync(async cancellationToken =>
        {
            await using var stream = await file.OpenReadAsync();
            await service.StageImportAsync(stream, cancellationToken);
            if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                throw new InvalidOperationException("Nie można automatycznie zamknąć aplikacji.");
            }

            ApplicationRestartService.StartReplacementProcess();
            ShowStatus("Import jest gotowy. Trwa ponowne uruchamianie aplikacji…");
            desktop.Shutdown();
        }, "Nie udało się przygotować importu");
    }

    private bool TryGetTransferContext(
        out SettingsBackupService service,
        out IStorageProvider storageProvider,
        out Window owner)
    {
        service = null!;
        storageProvider = null!;
        owner = null!;
        if (DataContext is not MainWindowViewModel viewModel ||
            TopLevel.GetTopLevel(this) is not Window topLevel ||
            topLevel.StorageProvider is not { } provider)
        {
            return false;
        }

        service = new SettingsBackupService(viewModel.SettingsDirectory);
        storageProvider = provider;
        owner = topLevel;
        return true;
    }

    private async Task RunTransferAsync(Func<CancellationToken, Task> operation, string errorPrefix)
    {
        CancelTransfer();
        _transferCancellation = new CancellationTokenSource();
        ExportSettingsButton.IsEnabled = false;
        ImportSettingsButton.IsEnabled = false;
        try
        {
            await operation(_transferCancellation.Token);
        }
        catch (OperationCanceledException) when (_transferCancellation.IsCancellationRequested)
        {
            ShowStatus("Operacja została anulowana.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        {
            ShowStatus($"{errorPrefix}: {exception.Message}");
        }
        finally
        {
            _transferCancellation.Dispose();
            _transferCancellation = null;
            ExportSettingsButton.IsEnabled = true;
            ImportSettingsButton.IsEnabled = true;
        }
    }

    private void CancelTransfer()
    {
        _transferCancellation?.Cancel();
    }

    private void ShowStatus(string message)
    {
        SettingsTransferStatusText.Text = message;
        SettingsTransferStatusText.IsVisible = true;
    }
}
