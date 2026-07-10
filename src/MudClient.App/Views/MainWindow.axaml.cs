using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MudClient.App.Controls;
using MudClient.App.Models;
using MudClient.App.ViewModels;

namespace MudClient.App.Views;

public partial class MainWindow : Window
{
    private readonly MudOutputView _mudOutput;
    private readonly TextBox _commandBox;
    private MainWindowViewModel? _viewModel;
    private int _historyIndex = -1;

    public MainWindow()
    {
        InitializeComponent();
        _mudOutput = this.FindControl<MudOutputView>("MudOutput")
            ?? throw new InvalidOperationException("MudOutput not found.");
        _commandBox = this.FindControl<TextBox>("CommandBox")
            ?? throw new InvalidOperationException("CommandBox not found.");

        DataContextChanged += OnDataContextChanged;
        Opened += OnOpened;
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;

        // Intercept text input in the tunneling phase so we can redirect
        // printable characters to the command box when focus is outside
        // any text-editing control.
        AddHandler(InputElement.TextInputEvent, OnPreviewTextInput, RoutingStrategies.Tunnel);

        _mudOutput.AppendText(
            "\u001b[96mMudClient Starter\u001b[0m\n" +
            "Wpisz host i port, a następnie kliknij Połącz.\n" +
            "Przykładowy alias: \u001b[93ml\u001b[0m -> \u001b[93mlook\u001b[0m\n\n");
    }

    private void OnDataContextChanged(object? sender, EventArgs eventArgs)
    {
        if (_viewModel is not null)
        {
            _viewModel.OutputReceived -= OnOutputReceived;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.OutputReceived += OnOutputReceived;
        }
    }

    private void OnOutputReceived(string text)
    {
        _mudOutput.AppendText(text);
    }

    private async void OnOpened(object? sender, EventArgs eventArgs)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception exception)
        {
            _viewModel.ReportStartupError(exception);
        }
    }

    private void OnDispatcherUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs eventArgs)
    {
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.ReportStartupError(eventArgs.Exception);
        eventArgs.Handled = true;
    }

    private void Close_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        Close();
    }

    private void Window_OnPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        // Clicking anywhere on the window should drop focus straight into the command line,
        // unless the click is meant for another interactive control.
        if (eventArgs.Source is not Visual visual)
        {
            return;
        }

        if (ReferenceEquals(visual, _commandBox) || visual.FindAncestorOfType<TextBox>(includeSelf: true) is not null)
        {
            return;
        }

        if (visual.FindAncestorOfType<Button>(includeSelf: true) is not null ||
            visual.FindAncestorOfType<ListBox>(includeSelf: true) is not null ||
            visual.FindAncestorOfType<TabControl>(includeSelf: true) is not null ||
            visual.FindAncestorOfType<GridSplitter>(includeSelf: true) is not null ||
            visual.FindAncestorOfType<ScrollBar>(includeSelf: true) is not null ||
            visual.FindAncestorOfType<ComboBox>(includeSelf: true) is not null ||
            visual.FindAncestorOfType<ToggleButton>(includeSelf: true) is not null ||
            visual.FindAncestorOfType<WorldMapControl>(includeSelf: true) is not null ||
            visual.FindAncestorOfType<SelectableTextBlock>(includeSelf: true) is not null ||
            visual.FindAncestorOfType<ProgressBar>(includeSelf: true) is not null)
        {
            return;
        }

        _commandBox.Focus();
        _commandBox.CaretIndex = _commandBox.Text?.Length ?? 0;
    }

    private void OnPreviewTextInput(object? sender, TextInputEventArgs e)
    {
        if (FocusManager?.GetFocusedElement() is TextBox)
        {
            return;
        }

        e.Handled = true;

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
                NavigateHistory(-1);
                eventArgs.Handled = true;
                break;

            case Key.Down:
                NavigateHistory(1);
                eventArgs.Handled = true;
                break;
        }
    }

    /// <summary>
    /// Shared post-send behavior: focus the command box, select all text,
    /// and reset history navigation so the next keypress replaces the
    /// selected text instead of appending.
    /// </summary>
    private void HandlePostSend()
    {
        _commandBox.Focus();
        _commandBox.SelectAll();
        _historyIndex = -1;
    }

    private void SendButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        // The Command binding already executed the send; we only perform
        // the post-send UI work here (focus + select-all + history reset).
        HandlePostSend();
    }

    private void NavigateHistory(int direction)
    {
        if (_viewModel is null || _viewModel.CommandHistory.Count == 0)
        {
            return;
        }

        // If no history session active, start from the beginning.
        if (_historyIndex < 0 && direction < 0)
        {
            _historyIndex = 0;
        }
        else if (_historyIndex < 0 && direction > 0)
        {
            // Already at "new" position — nothing older to go down to.
            return;
        }
        else
        {
            _historyIndex += direction;
        }

        _historyIndex = Math.Clamp(_historyIndex, -1, _viewModel.CommandHistory.Count - 1);

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

    // ========================================================================
    // New event handlers for DataTemplate buttons
    // ========================================================================

    private void QuickCommandChip_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button button &&
            button.DataContext is QuickCommand qc &&
            _viewModel is not null)
        {
            _viewModel.QuickCommandExecuteCommand.Execute(qc.Command);
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

    private void LogFilter_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is ToggleButton clicked)
        {
            // De-toggle all other filter buttons in the same panel.
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

            // Ensure the clicked one stays checked (toggle-group behavior).
            clicked.IsChecked = true;

            // Update the VM's selected log tab.
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

    protected override void OnClosed(EventArgs eventArgs)
    {
        if (_viewModel is not null)
        {
            _viewModel.OutputReceived -= OnOutputReceived;
            _ = _viewModel.DisposeAsync();
        }

        Dispatcher.UIThread.UnhandledException -= OnDispatcherUnhandledException;

        base.OnClosed(eventArgs);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
