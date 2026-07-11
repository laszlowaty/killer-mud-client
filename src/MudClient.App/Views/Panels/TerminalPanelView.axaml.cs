using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MudClient.App.Controls;
using MudClient.App.Models;
using MudClient.App.ViewModels;

namespace MudClient.App.Views.Panels;

public sealed partial class TerminalPanelView : UserControl
{
    /// <summary>
    /// Last-attached terminal panel. The main window uses this to redirect
    /// clicks / raw text input to the command box no matter which dockable
    /// panel currently has focus.
    /// </summary>
    public static TerminalPanelView? Current { get; private set; }

    private readonly MudOutputView _mudOutput;
    private readonly TextBox _commandBox;
    private MainWindowViewModel? _viewModel;
    private int _historyIndex = -1;

    public TerminalPanelView()
    {
        InitializeComponent();
        _mudOutput = this.FindControl<MudOutputView>("MudOutput")
            ?? throw new InvalidOperationException("MudOutput not found.");
        _commandBox = this.FindControl<TextBox>("CommandBox")
            ?? throw new InvalidOperationException("CommandBox not found.");

        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += (_, _) => Current = this;

        _mudOutput.AppendText(
            "[96mMudClient Starter[0m\n" +
            "Aplikacja łączy się z domyślnym hostem po wyborze profilu.\n" +
            "Możesz zmienić host/port i połączyć się ponownie ręcznie.\n" +
            "Przykładowy alias: [93ml[0m -> [93mlook[0m\n\n");
    }

    public bool OwnsControl(Visual visual) =>
        ReferenceEquals(visual, _commandBox) || visual.FindAncestorOfType<MudOutputView>(includeSelf: true) is { } mo && ReferenceEquals(mo, _mudOutput);

    private void OnDataContextChanged(object? sender, EventArgs eventArgs)
    {
        if (_viewModel is not null)
        {
            _viewModel.OutputReceived -= OnOutputReceived;
            _viewModel.ProfileActivated -= OnProfileActivated;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.OutputReceived += OnOutputReceived;
            _viewModel.ProfileActivated += OnProfileActivated;
        }
    }

    private async void OnProfileActivated(string profileName)
    {
        if (_viewModel is null)
        {
            return;
        }

        // Auto-connect once the user has chosen a profile.
        if (_viewModel.ConnectCommand.CanExecute(null))
        {
            await _viewModel.ConnectCommand.ExecuteAsync(null);
        }
    }

    private void OnOutputReceived(string text)
    {
        _mudOutput.AppendText(text);
    }

    public void FocusCommandBoxAndSelectAll()
    {
        _commandBox.Focus();
        _commandBox.SelectAll();
    }

    /// <summary>Redirects raw typed text into the command box (called by the window).</summary>
    public void RedirectTextInput(TextInputEventArgs e)
    {
        _commandBox.Focus();
        _commandBox.CaretIndex = _commandBox.Text?.Length ?? 0;

        var redirectArgs = new TextInputEventArgs { Text = e.Text };
        redirectArgs.RoutedEvent = InputElement.TextInputEvent;
        _commandBox.RaiseEvent(redirectArgs);
    }

    private void CommandBox_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (_viewModel is null)
        {
            return;
        }

        switch (eventArgs.Key)
        {
            case Key.Enter:
                if (_viewModel.SendCommandCommand.CanExecute(null))
                {
                    _viewModel.SendCommandCommand.Execute(null);
                    HandlePostSend();
                    eventArgs.Handled = true;
                }
                break;

            case Key.Up:
                NavigateHistory(+1);
                eventArgs.Handled = true;
                break;

            case Key.Down:
                NavigateHistory(-1);
                eventArgs.Handled = true;
                break;
        }
    }

    private void HandlePostSend()
    {
        FocusCommandBoxAndSelectAll();
        _historyIndex = -1;
    }

    private void SendButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        HandlePostSend();
    }

    private void NavigateHistory(int direction)
    {
        if (_viewModel is null || _viewModel.CommandHistory.Count == 0)
        {
            return;
        }

        if (direction > 0)
        {
            _historyIndex = Math.Min(_historyIndex + 1, _viewModel.CommandHistory.Count - 1);
        }
        else
        {
            if (_historyIndex < 0)
            {
                return;
            }

            _historyIndex = Math.Max(_historyIndex - 1, -1);
        }

        if (_historyIndex < 0)
        {
            _commandBox.Text = string.Empty;
        }
        else
        {
            _commandBox.Text = _viewModel.CommandHistory[_historyIndex];
        }

        _commandBox.CaretIndex = _commandBox.Text?.Length ?? 0;
    }

    private void QuickCommandChip_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is QuickCommand qc &&
            _viewModel is not null)
        {
            _viewModel.QuickCommandExecuteCommand.Execute(qc.Command);
        }
    }

    private void LogFilter_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is ToggleButton clicked)
        {
            if (clicked.Parent is StackPanel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (child is ToggleButton other && !ReferenceEquals(other, clicked))
                    {
                        other.IsChecked = false;
                    }
                }
            }

            clicked.IsChecked = true;

            if (_viewModel is not null && clicked.Tag is string tag)
            {
                var index = tag switch
                {
                    "all" => 0,
                    "combat" => 1,
                    "chat" => 2,
                    "system" => 3,
                    _ => 0,
                };
                _viewModel.SelectedLogTab = index;
            }
        }
    }
}
