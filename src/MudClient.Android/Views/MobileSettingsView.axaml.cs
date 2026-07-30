using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MudClient.Android.Services;
using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.Android.Views;

public sealed partial class MobileSettingsView : UserControl
{
    private static readonly FilePickerFileType ZipFileType = new("Archiwum ZIP")
    {
        Patterns = ["*.zip"],
        MimeTypes = ["application/zip"],
    };

    private CancellationTokenSource? _importCancellation;
    private bool _importReady;

    public MobileSettingsView()
    {
        InitializeComponent();
        DetachedFromVisualTree += (_, _) => CancelImport();
    }

    private async void SelectImport_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            TopLevel.GetTopLevel(this)?.StorageProvider is not { } storageProvider)
        {
            ShowImportStatus("Nie udało się otworzyć wyboru pliku.");
            return;
        }

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import ustawień KillerMudClient",
            AllowMultiple = false,
            FileTypeFilter = [ZipFileType],
        });
        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        CancelImport();
        _importCancellation = new CancellationTokenSource();
        _importReady = false;
        ApplyImportButton.IsVisible = false;
        SelectImportButton.IsEnabled = false;
        ShowImportStatus("Sprawdzanie kopii…");

        try
        {
            var service = new SettingsBackupService(viewModel.SettingsDirectory);
            await using var stream = await file.OpenReadAsync();
            await service.StageImportAsync(stream, _importCancellation.Token);

            _importReady = true;
            ApplyImportButton.IsVisible = true;
            ShowImportStatus(
                "Kopia jest poprawna. Zastosowanie importu zastąpi obecne dane aplikacji.");
        }
        catch (OperationCanceledException) when (_importCancellation.IsCancellationRequested)
        {
            ShowImportStatus("Import został anulowany.");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidDataException)
        {
            ShowImportStatus($"Nie udało się przygotować importu: {exception.Message}");
        }
        finally
        {
            _importCancellation.Dispose();
            _importCancellation = null;
            SelectImportButton.IsEnabled = true;
        }
    }

    private void ApplyImport_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (!_importReady)
        {
            return;
        }

        ApplyImportButton.IsEnabled = false;
        SelectImportButton.IsEnabled = false;
        ShowImportStatus("Ponowne uruchamianie i zastosowanie importu…");

        try
        {
            AndroidApplicationRestartService.ScheduleRestartAndExit(
                global::Android.App.Application.Context);
        }
        catch (Exception exception) when (exception is InvalidOperationException)
        {
            ApplyImportButton.IsEnabled = true;
            SelectImportButton.IsEnabled = true;
            ShowImportStatus(
                $"Nie udało się uruchomić aplikacji ponownie: {exception.Message}");
        }
    }

    private void CancelImport()
    {
        _importCancellation?.Cancel();
    }

    private void ShowImportStatus(string message)
    {
        ImportStatusText.Text = message;
        ImportStatusText.IsVisible = true;
    }
}
