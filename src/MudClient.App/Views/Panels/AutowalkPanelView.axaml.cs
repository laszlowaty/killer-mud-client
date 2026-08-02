using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MudClient.App.Controls;
using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.App.Views.Panels;

public sealed partial class AutowalkPanelView : UserControl
{
    private MainWindowViewModel? _viewModel;
    private readonly AutomationTransferService _transferService = new();
    private CancellationTokenSource _transferCancellation = new();
    private static readonly FilePickerFileType JsonFileType = new("JSON")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };
    internal Func<Window, string, string, Task<bool>> ConfirmDeletionAsync { get; set; } =
        DeleteConfirmationDialog.ShowAsync;

    public AutowalkPanelView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => _viewModel = DataContext as MainWindowViewModel;
        AttachedToVisualTree += (_, _) =>
        {
            if (_transferCancellation.IsCancellationRequested)
            {
                _transferCancellation.Dispose();
                _transferCancellation = new CancellationTokenSource();
            }
        };
        DetachedFromVisualTree += (_, _) => _transferCancellation.Cancel();
    }

    private void GoToLocation_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is AutowalkLocation location &&
            _viewModel is not null)
        {
            _viewModel.GoToLocationCommand.Execute(location);
        }
    }

    private void GoToDeath_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is DeathMarkEntry entry &&
            _viewModel is not null)
        {
            _viewModel.GoToDeathCommand.Execute(entry);
        }
    }

    private void DeleteDeath_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is DeathMarkEntry entry &&
            _viewModel is not null)
        {
            _viewModel.DeleteDeathCommand.Execute(entry);
        }
    }

    private async void DeleteLocation_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is AutowalkLocation location &&
            _viewModel is not null)
        {
            if (TopLevel.GetTopLevel(this) is not Window owner)
            {
                return;
            }

            button.IsEnabled = false;
            try
            {
                if (await ConfirmDeletionAsync(owner, "cel autowalk", location.Name))
                {
                    _viewModel.DeleteLocationCommand.Execute(location);
                }
            }
            finally
            {
                button.IsEnabled = true;
            }
        }
    }

    private async void ExportAll_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storageProvider)
        {
            return;
        }

        var cancellationToken = _transferCancellation.Token;
        try
        {
            var package = _viewModel.CreateAutowalkTransferPackage();
            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Eksport celów autowalka",
                SuggestedFileName = "autowalk.json",
                FileTypeChoices = [JsonFileType],
                DefaultExtension = "json",
                ShowOverwritePrompt = true,
            });
            if (file is null)
            {
                return;
            }

            await using var stream = await file.OpenWriteAsync();
            stream.SetLength(0);
            await _transferService.WriteAsync(stream, package, cancellationToken);
            _viewModel.ReportAutomationTransfer("Wyeksportowano cele autowalka do JSON.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Zamknięcie panelu anuluje transfer; nie pokazujemy komunikatu dla oczekiwanego przerwania.
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            _viewModel.ReportAutomationTransfer(
                $"Nie udało się wyeksportować celów autowalka: {exception.Message}",
                isError: true);
        }
    }

    private async void Import_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storageProvider)
        {
            return;
        }

        var cancellationToken = _transferCancellation.Token;
        try
        {
            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import celów autowalka",
                AllowMultiple = false,
                FileTypeFilter = [JsonFileType],
            });
            var file = files.FirstOrDefault();
            if (file is null)
            {
                return;
            }

            await using var stream = await file.OpenReadAsync();
            var package = await _transferService.ReadAsync(stream, cancellationToken);
            if (package.Kind != FolderKind.Autowalk)
            {
                throw new JsonException("Wybrany plik nie zawiera celów autowalka.");
            }

            _viewModel.ImportAutomationTransferPackage(package);
            _viewModel.ReportAutomationTransfer("Zaimportowano cele autowalka z JSON.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Zamknięcie panelu anuluje transfer; nie pokazujemy komunikatu dla oczekiwanego przerwania.
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            _viewModel.ReportAutomationTransfer(
                $"Nie udało się zaimportować celów autowalka: {exception.Message}",
                isError: true);
        }
    }

}
