using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using MudClient.App.Controls;
using MudClient.App.Models;
using MudClient.App.Services;
using MudClient.App.ViewModels;

namespace MudClient.App.Views.Panels;

public sealed partial class AutomationPanelView : UserControl
{
    private MainWindowViewModel? _viewModel;
    private readonly AutomationTransferService _transferService = new();
    private static readonly FilePickerFileType JsonFileType = new("JSON")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };

    public AutomationPanelView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => _viewModel = DataContext as MainWindowViewModel;
    }

    private void EditTimer_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is TimerEntry timer &&
            _viewModel is not null)
        {
            _viewModel.EditTimerCommand.Execute(timer);
        }
    }

    private void ToggleTimer_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is ToggleButton button &&
            button.DataContext is TimerEntry timer &&
            _viewModel is not null)
        {
            _viewModel.ToggleTimerCommand.Execute(timer);
        }
    }

    private void DeleteTimer_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is TimerEntry timer &&
            _viewModel is not null)
        {
            _viewModel.DeleteTimerCommand.Execute(timer);
        }
    }

    private void ToggleRule_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is ToggleButton button &&
            button.DataContext is AutomationRuleEntry rule &&
            _viewModel is not null)
        {
            _viewModel.ToggleRuleCommand.Execute(rule);
        }
    }

    private void DeleteRule_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is AutomationRuleEntry rule &&
            _viewModel is not null)
        {
            _viewModel.DeleteRuleCommand.Execute(rule);
        }
    }

    private void EditRule_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is AutomationRuleEntry rule &&
            _viewModel is not null)
        {
            _viewModel.EditRuleCommand.Execute(rule);
        }
    }

    private async void ExportItem_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Control { DataContext: { } item })
        {
            await ExportAsync(item);
        }
    }

    private async void FolderTree_OnFolderExportRequested(object? sender, FolderExportRequestedEventArgs eventArgs) =>
        await ExportAsync(eventArgs.Folder);

    private async Task ExportAsync(object selection)
    {
        if (_viewModel is null || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storageProvider)
        {
            return;
        }

        try
        {
            var package = _viewModel.CreateAutomationTransferPackage(selection);
            var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Eksport automatyzacji",
                SuggestedFileName = GetSuggestedFileName(selection) + ".json",
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
            await _transferService.WriteAsync(stream, package);
            _viewModel.ReportAutomationTransfer("Wyeksportowano dane automatyzacji do JSON.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            _viewModel.ReportAutomationTransfer($"Nie udało się wyeksportować: {exception.Message}", isError: true);
        }
    }

    private async void Import_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (_viewModel is null ||
            sender is not Control { Tag: FolderKind expectedKind } ||
            TopLevel.GetTopLevel(this)?.StorageProvider is not { } storageProvider)
        {
            return;
        }

        try
        {
            var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Import automatyzacji",
                AllowMultiple = false,
                FileTypeFilter = [JsonFileType],
            });
            var file = files.FirstOrDefault();
            if (file is null)
            {
                return;
            }

            await using var stream = await file.OpenReadAsync();
            var package = await _transferService.ReadAsync(stream);
            if (package.Kind != expectedKind)
            {
                throw new JsonException($"Wybrany plik zawiera {package.Kind}, a nie {expectedKind}.");
            }

            _viewModel.ImportAutomationTransferPackage(package);
            _viewModel.ReportAutomationTransfer("Zaimportowano dane automatyzacji z JSON.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException)
        {
            _viewModel.ReportAutomationTransfer($"Nie udało się zaimportować: {exception.Message}", isError: true);
        }
    }

    private static string GetSuggestedFileName(object selection)
    {
        var raw = selection switch
        {
            FolderNode folder => folder.Name,
            TimerEntry timer => timer.Name,
            AutomationRuleEntry rule => rule.Name,
            _ => "automatyzacja",
        };
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(raw.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "automatyzacja" : sanitized;
    }
}
