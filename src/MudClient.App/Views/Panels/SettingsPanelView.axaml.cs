using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MudClient.App.Controls;
using MudClient.App.Models;
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
    private FloatingButtonDefinition? _editingFloatingButton;

    public SettingsPanelView()
    {
        InitializeComponent();
        DetachedFromVisualTree += (_, _) => CancelTransfer();
    }

    private async void SelectGameSessionLogFolder_OnClick(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainWindowViewModel viewModel
            || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storageProvider)
        {
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
            if (folder?.Path.IsFile == true)
            {
                viewModel.SetGameSessionLogFolder(folder.Path.LocalPath, folder.Path.LocalPath);
            }
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            ShowStatus($"Nie udało się wybrać folderu zapisów: {exception.Message}");
        }
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

    private void AddFloatingButtonSet_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var name = FloatingButtonSetNameInput.Text?.Trim() ?? string.Empty;
        if (viewModel.AddFloatingButtonSet(name) is not null)
        {
            FloatingButtonSetNameInput.Text = string.Empty;
            FloatingButtonStatusText.IsVisible = false;
        }
        else
        {
            FloatingButtonStatusText.Text = "Schemat o takiej nazwie już istnieje albo nazwa jest pusta.";
            FloatingButtonStatusText.IsVisible = true;
        }
    }

    private void DeleteFloatingButtonSet_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            viewModel.RemoveSelectedFloatingButtonSet();
        }
    }

    private void AddFloatingButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        var succeeded = _editingFloatingButton is not null
            ? viewModel.UpdateFloatingButton(
                _editingFloatingButton,
                FloatingButtonNameInput.Text,
                FloatingButtonCommandInput.Text)
            : viewModel.AddFloatingButton(
                FloatingButtonNameInput.Text,
                FloatingButtonCommandInput.Text) is not null;
        if (succeeded)
        {
            ResetFloatingButtonEditor();
            FloatingButtonStatusText.IsVisible = false;
        }
        else
        {
            FloatingButtonStatusText.Text = "Nazwa i komenda przycisku nie mogą być puste.";
            FloatingButtonStatusText.IsVisible = true;
        }
    }

    private void EditFloatingButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Control { Tag: FloatingButtonDefinition definition })
        {
            return;
        }

        _editingFloatingButton = definition;
        FloatingButtonNameInput.Text = definition.Name;
        FloatingButtonCommandInput.Text = definition.Command;
        FloatingButtonSubmitButton.Content = "Zapisz zmiany";
        FloatingButtonCancelEditButton.IsVisible = true;
        FloatingButtonStatusText.IsVisible = false;
        FloatingButtonNameInput.Focus();
    }

    private void CancelFloatingButtonEdit_OnClick(object? sender, RoutedEventArgs eventArgs) =>
        ResetFloatingButtonEditor();

    private void FloatingButtonSet_OnSelectionChanged(
        object? sender,
        SelectionChangedEventArgs eventArgs)
    {
        if (_editingFloatingButton is not null)
        {
            ResetFloatingButtonEditor();
        }
    }

    private void DeleteFloatingButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (DataContext is MainWindowViewModel viewModel
            && sender is Control { Tag: FloatingButtonDefinition definition })
        {
            viewModel.RemoveFloatingButton(definition);
            if (string.Equals(
                    _editingFloatingButton?.Id,
                    definition.Id,
                    StringComparison.Ordinal))
            {
                ResetFloatingButtonEditor();
            }
        }
    }

    private void ResetFloatingButtonEditor()
    {
        _editingFloatingButton = null;
        FloatingButtonNameInput.Text = string.Empty;
        FloatingButtonCommandInput.Text = string.Empty;
        FloatingButtonSubmitButton.Content = "Dodaj pływający przycisk";
        FloatingButtonCancelEditButton.IsVisible = false;
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
