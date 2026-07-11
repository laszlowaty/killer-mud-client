using Avalonia.Controls;
using Avalonia.Interactivity;
using MudClient.App.Models;
using MudClient.App.ViewModels;

namespace MudClient.App.Views.Panels;

public sealed partial class NotesPanelView : UserControl
{
    private MainWindowViewModel? _viewModel;

    public NotesPanelView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => _viewModel = DataContext as MainWindowViewModel;
    }

    private void EditNote_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is NoteEntry note &&
            _viewModel is not null)
        {
            _viewModel.EditNoteCommand.Execute(note);
        }
    }

    private void DeleteNote_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is NoteEntry note &&
            _viewModel is not null)
        {
            _viewModel.DeleteNoteCommand.Execute(note);
        }
    }
}
