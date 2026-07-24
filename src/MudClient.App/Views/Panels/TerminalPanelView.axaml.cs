using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MudClient.App.Controls;
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
    private readonly TextBox _searchBox;
    private readonly DispatcherTimer _timerCountdownRefresh;
    private MainWindowViewModel? _viewModel;
    private bool _isViewModelSubscribed;
    private int _historyIndex = -1;
    private volatile bool _shouldSelectAllOnNextInput;

    public TerminalPanelView()
    {
        InitializeComponent();
        _mudOutput = this.FindControl<MudOutputView>("MudOutput")
            ?? throw new InvalidOperationException("MudOutput not found.");
        _commandBox = this.FindControl<TextBox>("CommandBox")
            ?? throw new InvalidOperationException("CommandBox not found.");
        _searchBox = this.FindControl<TextBox>("SearchBox")
            ?? throw new InvalidOperationException("SearchBox not found.");
        _commandBox.AddHandler(
            InputElement.KeyDownEvent,
            CommandBox_OnPreviewKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
        _timerCountdownRefresh = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100),
        };
        _timerCountdownRefresh.Tick += RefreshTimerCountdowns;

        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;

        _mudOutput.AppendText(
            "[96mMudClient Starter[0m\n" +
            "Aplikacja łączy się z domyślnym hostem po wyborze profilu.\n" +
            "Możesz zmienić host/port i połączyć się ponownie ręcznie.\n" +
            "Przykładowy alias: [93ml[0m -> [93mlook[0m\n\n");
    }

    /// <summary>Checks whether <paramref name="box"/> is this terminal's command box.</summary>
    public bool IsCommandBox(TextBox? box) => ReferenceEquals(box, _commandBox);

    /// <summary>
    /// Marks that the next text input should select all text in the command box
    /// (e.g., because window focus just returned from another application while
    /// the command box still held focus).
    /// </summary>
    public void MarkForSelectAllOnNextInput() => _shouldSelectAllOnNextInput = true;

    /// <summary>
    /// Clears the select-all-on-next-input flag without performing any selection.
    /// Called when text input arrives for a non-terminal TextBox (host/port/profile),
    /// preventing a stale mark from hijacking the command box later.
    /// </summary>
    public void ClearSelectAllOnNextInput() => _shouldSelectAllOnNextInput = false;

    /// <summary>
    /// If previously marked, selects all text in the command box and clears the mark.
    /// Called on the first keystroke after window reactivation when the command box
    /// already has focus.
    /// </summary>
    public void PrepareCommandBoxForFirstInput()
    {
        if (!_shouldSelectAllOnNextInput)
            return;

        _shouldSelectAllOnNextInput = false;
        _commandBox.SelectAll();
    }

    public bool OwnsControl(Visual visual) =>
        ReferenceEquals(visual, _commandBox);

    private void OnDataContextChanged(object? sender, EventArgs eventArgs)
    {
        UnsubscribeFromViewModel();
        _viewModel = DataContext as MainWindowViewModel;
        RefreshTimerCountdowns(this, EventArgs.Empty);

        if (this.IsAttachedToVisualTree())
        {
            SubscribeToViewModel();
        }
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs eventArgs)
    {
        Current = this;
        SubscribeToViewModel();
        RefreshTimerCountdowns(this, EventArgs.Empty);
        _timerCountdownRefresh.Start();
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs eventArgs)
    {
        UnsubscribeFromViewModel();
        _timerCountdownRefresh.Stop();
        if (ReferenceEquals(Current, this))
        {
            Current = null;
        }
    }

    private void RefreshTimerCountdowns(object? sender, EventArgs eventArgs)
    {
        if (_viewModel is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var timer in _viewModel.Timers)
        {
            timer.RefreshCountdown(now);
        }
    }

    private void SubscribeToViewModel()
    {
        if (_viewModel is null || _isViewModelSubscribed)
        {
            return;
        }

        _viewModel.OutputReceived += OnOutputReceived;
        _viewModel.ProfileActivated += OnProfileActivated;
        _isViewModelSubscribed = true;
    }

    private void UnsubscribeFromViewModel()
    {
        if (_viewModel is null || !_isViewModelSubscribed)
        {
            return;
        }

        _viewModel.OutputReceived -= OnOutputReceived;
        _viewModel.ProfileActivated -= OnProfileActivated;
        _isViewModelSubscribed = false;
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
        SelectAllCommandText();
    }

    /// <summary>Selects the command text without changing the currently focused control.</summary>
    public void SelectAllCommandText()
    {
        _commandBox.SelectAll();
        _shouldSelectAllOnNextInput = false;
    }

    /// <summary>Redirects raw typed text into the command box (called by the window).</summary>
    public void RedirectTextInput(TextInputEventArgs e)
    {
        _commandBox.Focus();
        _commandBox.SelectAll();
        _shouldSelectAllOnNextInput = false;

        var redirectArgs = new TextInputEventArgs { Text = e.Text };
        redirectArgs.RoutedEvent = InputElement.TextInputEvent;
        _commandBox.RaiseEvent(redirectArgs);
    }

    private async void CommandBox_OnPreviewKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.C
            && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
            && _mudOutput.HasSelection)
        {
            // Output selection keeps focus in the command box so typing can continue.
            // Give that selection priority; without one, leave Ctrl+C to the TextBox.
            eventArgs.Handled = true;
            await _mudOutput.CopySelectionToClipboardAsync();
            return;
        }

        // Must intercept Enter in the tunnel phase: the TextBox's own bubble-phase
        // handling (AcceptsReturn="True") would otherwise insert a newline first.
        if (eventArgs.Key == Key.Enter
            && !eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift)
            && _viewModel is not null
            && _viewModel.SendCommandCommand.CanExecute(null))
        {
            _viewModel.SendCommandCommand.Execute(null);
            HandlePostSend();
            eventArgs.Handled = true;
        }
    }

    private void CommandBox_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (_viewModel is null)
        {
            return;
        }

        switch (eventArgs.Key)
        {
            case Key.Up:
                if (IsCaretOnFirstLine())
                {
                    NavigateHistory(+1);
                    eventArgs.Handled = true;
                }
                break;

            case Key.Down:
                if (IsCaretOnLastLine())
                {
                    NavigateHistory(-1);
                    eventArgs.Handled = true;
                }
                break;
        }
    }

    private bool IsCaretOnFirstLine()
    {
        var text = _commandBox.Text ?? string.Empty;
        var caretIndex = _commandBox.CaretIndex;
        return text.LastIndexOf('\n', Math.Max(0, Math.Min(caretIndex, text.Length) - 1)) < 0;
    }

    private bool IsCaretOnLastLine()
    {
        var text = _commandBox.Text ?? string.Empty;
        var caretIndex = _commandBox.CaretIndex;
        return text.IndexOf('\n', Math.Min(caretIndex, text.Length)) < 0;
    }

    private void SearchBox_OnTextChanged(object? sender, TextChangedEventArgs eventArgs)
    {
        if (sender is TextBox searchBox)
        {
            _mudOutput.UpdateSearch(searchBox.Text ?? string.Empty);
        }
    }

    private void SearchBox_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (sender is not TextBox searchBox)
        {
            return;
        }

        if (eventArgs.Key == Key.Escape)
        {
            searchBox.Clear();
            eventArgs.Handled = true;
            return;
        }

        if (eventArgs.Key != Key.Enter)
        {
            return;
        }

        ExecuteSearch(newer: eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift));
        eventArgs.Handled = true;
    }

    private void SearchPrevButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        ExecuteSearch(newer: false);
    }

    private void SearchNextButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        ExecuteSearch(newer: true);
    }

    private void ExecuteSearch(bool newer)
    {
        _mudOutput.Search(_searchBox.Text ?? string.Empty, newer: newer);
        _searchBox.Focus();
        _searchBox.CaretIndex = _searchBox.Text?.Length ?? 0;
    }

    private void HandlePostSend()
    {
        if (_viewModel?.ClearCommandInputAfterSend == true)
        {
            _viewModel.CommandText = string.Empty;
            _commandBox.Clear();
            _commandBox.Focus();
            _shouldSelectAllOnNextInput = false;
        }
        else
        {
            FocusCommandBoxAndSelectAll();
        }

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

}
