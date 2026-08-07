using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using MudClient.App.Models;
using MudClient.App.ViewModels;

namespace MudClient.Android.Views;

public sealed partial class MobileAutomationView : UserControl
{
    private MainWindowViewModel? _viewModel;

    public MobileAutomationView()
    {
        ConfirmDeleteFolderCommand = new RelayCommand<FolderNode>(ConfirmDeleteFolder);
        InitializeComponent();
        DataContextChanged += (_, _) => _viewModel = DataContext as MainWindowViewModel;
    }

    public IRelayCommand<FolderNode> ConfirmDeleteFolderCommand { get; }

    private void ConfirmDeleteFolder(FolderNode? folder)
    {
        if (folder is null || _viewModel is null)
        {
            return;
        }

        if (_viewModel.DeleteFolderCommand.CanExecute(folder))
        {
            _viewModel.DeleteFolderCommand.Execute(folder);
        }
    }
}
