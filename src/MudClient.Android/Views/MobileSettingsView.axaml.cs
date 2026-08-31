using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MudClient.Android.Services;
using MudClient.App.Models;
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
    private CancellationTokenSource? _exportCancellation;
    private bool _importReady;

    public MobileSettingsView()
    {
        InitializeComponent();
        DetachedFromVisualTree += (_, _) => CancelTransfers();
    }

    private async void SelectGameSessionLogFolder_OnClick(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainWindowViewModel viewModel
            || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storageProvider)
        {
            ShowImportStatus("Nie udało się otworzyć wyboru folderu.");
            return;
        }

        try
        {
            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Folder zapisów sesji gry",
                AllowMultiple = false,
            });
            var folder = folders.FirstOrDefault();
            if (folder is null)
            {
                return;
            }

            var identifier = folder.Path.AbsoluteUri;
            AndroidGameSessionLogStorage.PersistFolderPermission(identifier);
            viewModel.SetGameSessionLogFolder(identifier, folder.Name);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException
            or Java.Lang.Exception)
        {
            ShowImportStatus($"Nie udało się wybrać folderu zapisów: {exception.Message}");
        }
    }

    private void AddFloatingButtonSet_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            ShowFloatingButtonStatus("Nie udało się utworzyć schematu.");
            return;
        }

        if (viewModel.AddFloatingButtonSet(FloatingButtonSetNameInput.Text) is null)
        {
            ShowFloatingButtonStatus("Podaj unikalną nazwę schematu.");
            return;
        }

        FloatingButtonSetNameInput.Text = string.Empty;
        FloatingButtonStatusText.IsVisible = false;
    }

    private void DeleteFloatingButtonSet_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            !viewModel.RemoveSelectedFloatingButtonSet())
        {
            ShowFloatingButtonStatus("Musi pozostać co najmniej jeden schemat.");
        }
    }

    private void AddFloatingButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            ShowFloatingButtonStatus("Nie udało się dodać przycisku.");
            return;
        }

        var button = viewModel.AddFloatingButton(
            FloatingButtonNameInput.Text,
            FloatingButtonCommandInput.Text);
        if (button is null)
        {
            ShowFloatingButtonStatus("Podaj nazwę przycisku i komendę.");
            return;
        }

        FloatingButtonNameInput.Text = string.Empty;
        FloatingButtonCommandInput.Text = string.Empty;
        FloatingButtonStatusText.IsVisible = false;
    }

    private void DeleteFloatingButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel
            && sender is Avalonia.Controls.Button { Tag: FloatingButtonDefinition button })
        {
            viewModel.RemoveFloatingButton(button);
        }
    }

    private void ShowFloatingButtonStatus(string message)
    {
        FloatingButtonStatusText.Text = message;
        FloatingButtonStatusText.IsVisible = true;
    }

    private async void ExportSettings_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            TopLevel.GetTopLevel(this)?.StorageProvider is not { } storageProvider)
        {
            ShowImportStatus("Nie udało się otworzyć wyboru pliku.");
            return;
        }

        IStorageFile? file;
        try
        {
            file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Eksport wszystkich ustawień",
                SuggestedFileName = $"KillerMudClient-Android-{DateTime.Now:yyyy-MM-dd}.zip",
                FileTypeChoices = [ZipFileType],
                DefaultExtension = "zip",
                ShowOverwritePrompt = true,
            });
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            ShowImportStatus($"Nie udało się wybrać pliku kopii: {exception.Message}");
            return;
        }

        if (file is null)
        {
            return;
        }

        _exportCancellation?.Cancel();
        _exportCancellation?.Dispose();
        _exportCancellation = new CancellationTokenSource();
        ExportSettingsButton.IsEnabled = false;
        SelectImportButton.IsEnabled = false;
        ApplyImportButton.IsEnabled = false;
        ShowImportStatus("Tworzenie pełnej kopii…");
        try
        {
            var service = new SettingsBackupService(viewModel.SettingsDirectory);
            await using var stream = await file.OpenWriteAsync();
            if (stream.CanSeek)
            {
                stream.SetLength(0);
            }

            await service.ExportAsync(
                stream,
                _exportCancellation.Token,
                file.Path.IsFile ? file.Path.LocalPath : null);
            ShowImportStatus("Utworzono pełną kopię ustawień.");
        }
        catch (OperationCanceledException) when (_exportCancellation.IsCancellationRequested)
        {
            ShowImportStatus("Eksport został anulowany.");
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            ShowImportStatus($"Nie udało się utworzyć kopii: {exception.Message}");
        }
        finally
        {
            _exportCancellation.Dispose();
            _exportCancellation = null;
            ExportSettingsButton.IsEnabled = true;
            SelectImportButton.IsEnabled = true;
            ApplyImportButton.IsEnabled = true;
        }
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
        ExportSettingsButton.IsEnabled = false;
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
            ExportSettingsButton.IsEnabled = true;
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

    private void CancelTransfers()
    {
        CancelImport();
        _exportCancellation?.Cancel();
    }

    private void ShowImportStatus(string message)
    {
        ImportStatusText.Text = message;
        ImportStatusText.IsVisible = true;
    }
}
