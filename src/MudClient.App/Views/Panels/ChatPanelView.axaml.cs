using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using MudClient.App.Controls;
using MudClient.App.ViewModels;

namespace MudClient.App.Views.Panels;

/// <summary>
/// Scrollable ANSI conversation view. History lives in the main view model so messages
/// are retained while the dockable widget is hidden or temporarily detached.
/// </summary>
public sealed partial class ChatPanelView : UserControl
{
    private readonly MudOutputView _chatOutput;
    private MainWindowViewModel? _viewModel;
    private bool _isViewModelSubscribed;

    public ChatPanelView()
    {
        InitializeComponent();
        _chatOutput = this.FindControl<MudOutputView>("ChatOutput")
            ?? throw new InvalidOperationException("ChatOutput not found.");

        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void OnDataContextChanged(object? sender, EventArgs eventArgs)
    {
        UnsubscribeFromViewModel();
        _viewModel = DataContext as MainWindowViewModel;

        if (this.IsAttachedToVisualTree())
        {
            SubscribeToViewModel();
        }
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs eventArgs) =>
        SubscribeToViewModel();

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs eventArgs) =>
        UnsubscribeFromViewModel();

    private void SubscribeToViewModel()
    {
        if (_viewModel is null || _isViewModelSubscribed)
        {
            return;
        }

        _chatOutput.Clear();
        foreach (var line in _viewModel.ChatHistory)
        {
            _chatOutput.AppendText(line);
        }

        _viewModel.ChatOutputReceived += OnChatOutputReceived;
        _isViewModelSubscribed = true;
    }

    private void UnsubscribeFromViewModel()
    {
        if (_viewModel is null || !_isViewModelSubscribed)
        {
            return;
        }

        _viewModel.ChatOutputReceived -= OnChatOutputReceived;
        _isViewModelSubscribed = false;
    }

    private void OnChatOutputReceived(string text) => _chatOutput.AppendText(text);
}
