using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using MudClient.App.Controls;
using MudClient.App.ViewModels;

namespace MudClient.App.Views.Panels;

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

        _viewModel.ChatLineReceived += OnChatLineReceived;
        _isViewModelSubscribed = true;
    }

    private void UnsubscribeFromViewModel()
    {
        if (_viewModel is null || !_isViewModelSubscribed)
        {
            return;
        }

        _viewModel.ChatLineReceived -= OnChatLineReceived;
        _isViewModelSubscribed = false;
    }

    private void OnChatLineReceived(string line) => _chatOutput.AppendText(line + "\n");
}
