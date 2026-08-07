using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using MudClient.App.Controls;
using MudClient.App.Models;
using MudClient.App.ViewModels;

namespace MudClient.Android.Views;

public sealed partial class MobileAutomationView : UserControl
{
    private MainWindowViewModel? _viewModel;

    internal Func<Window, string, string, Task<bool>> ConfirmDeletionAsync { get; set; } =
        DeleteConfirmationDialog.ShowAsync;

    public MobileAutomationView()
    {
        ConfirmDeleteFolderCommand = new AsyncRelayCommand<FolderNode>(ConfirmDeleteFolderAsync);
        InitializeComponent();
        DataContextChanged += (_, _) => _viewModel = DataContext as MainWindowViewModel;
    }

    public IAsyncRelayCommand<FolderNode> ConfirmDeleteFolderCommand { get; }

    private async Task ConfirmDeleteFolderAsync(FolderNode? folder)
    {
        if (folder is null ||
            _viewModel is null ||
            TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var itemType = folder.Kind switch
        {
            FolderKind.Timers => "folder timerów",
            FolderKind.Aliases => "folder aliasów",
            FolderKind.Triggers => "folder triggerów",
            FolderKind.Scripts => "folder skryptów",
            _ => "folder",
        };

        if (await ConfirmDeletionAsync(owner, itemType, folder.Name) &&
            _viewModel.DeleteFolderCommand.CanExecute(folder))
        {
            _viewModel.DeleteFolderCommand.Execute(folder);
        }
    }
}
