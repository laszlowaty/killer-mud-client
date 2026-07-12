using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MudClient.App.Controls;
using MudClient.App.ViewModels;
using MudClient.App.Views.Panels;

namespace MudClient.App.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        Opened += OnOpened;
        Activated += OnWindowActivated;
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
        DataContextChanged += (_, _) => _viewModel = DataContext as MainWindowViewModel;

        // Safety net: when a dock drag ends (drop or cancel, anywhere in the window),
        // reclaim any panel the drag pipeline dropped on the floor so it reappears in
        // the "Panele" restore menu instead of vanishing.
        var mainDock = this.FindControl<Dock.Avalonia.Controls.DockControl>("MainDock");
        if (mainDock is not null)
        {
            mainDock.PropertyChanged += (_, args) =>
            {
                if (args.Property == Dock.Avalonia.Controls.DockControl.IsDraggingDockProperty
                    && args.NewValue is false)
                {
                    // Let the drag pipeline finish its bookkeeping first.
                    Dispatcher.UIThread.Post(() => _viewModel?.ReclaimLostPanels(), DispatcherPriority.Background);
                }
            };
        }

        // Intercept text input in the tunneling phase so we can redirect
        // printable characters to the command box when focus is outside
        // any text-editing control.
        AddHandler(InputElement.TextInputEvent, OnPreviewTextInput, RoutingStrategies.Tunnel);
    }

    private async void OnOpened(object? sender, EventArgs eventArgs)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            // Auto-connect happens after the user picks a profile
            // (MainWindowViewModel.ActivateProfile).
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

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        // When the window becomes active while the command box still holds focus,
        // mark the terminal so that the first keystroke replaces the existing
        // command text instead of appending to it. If no TextBox or a non-terminal
        // TextBox holds focus, do not set the mark — a stale flag would otherwise
        // hijack the command box after the user typed elsewhere.
        var terminal = TerminalPanelView.Current;
        if (terminal is not null &&
            FocusManager?.GetFocusedElement() is TextBox focusedBox &&
            terminal.IsCommandBox(focusedBox))
        {
            terminal.MarkForSelectAllOnNextInput();
        }
    }

    private void Close_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        Close();
    }

    private void Window_OnPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        // Clicking anywhere on the window should drop focus straight into the command line,
        // unless the click is meant for another interactive control.
        var terminal = TerminalPanelView.Current;
        if (terminal is null || eventArgs.Source is not Visual visual)
        {
            return;
        }

        if (terminal.OwnsControl(visual) || visual.FindAncestorOfType<TextBox>(includeSelf: true) is not null)
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
            visual.FindAncestorOfType<ProgressBar>(includeSelf: true) is not null)
        {
            return;
        }

        terminal.FocusCommandBoxAndSelectAll();
    }

    private void OnPreviewTextInput(object? sender, TextInputEventArgs e)
    {
        var focused = FocusManager?.GetFocusedElement();
        var terminal = TerminalPanelView.Current;

        if (focused is TextBox focusedTextBox)
        {
            // When the terminal's command box has focus and window focus just returned,
            // select all text so the first typed character replaces existing input.
            if (terminal is not null && terminal.IsCommandBox(focusedTextBox))
            {
                terminal.PrepareCommandBoxForFirstInput();
            }
            else if (terminal is not null)
            {
                // Text input arrived for a non-terminal TextBox (host/port/profile, etc.).
                // Clear any pending select-all mark so it does not hijack the command box
                // when the user later clicks into it and types.
                terminal.ClearSelectAllOnNextInput();
            }

            return;
        }

        // No TextBox has focus – redirect printable characters to the terminal command box.
        if (terminal is null)
        {
            return;
        }

        e.Handled = true;
        terminal.RedirectTextInput(e);
    }

    private void ProfileList_OnDoubleTapped(object? sender, TappedEventArgs eventArgs)
    {
        if (_viewModel is null)
        {
            return;
        }

        // Only react to double-clicks on an actual item, not on empty list space.
        if (eventArgs.Source is Visual source &&
            source.FindAncestorOfType<ListBoxItem>(includeSelf: true) is { DataContext: string profileName })
        {
            _viewModel.SelectedProfileName = profileName;
            if (_viewModel.SelectProfileCommand.CanExecute(null))
            {
                _viewModel.SelectProfileCommand.Execute(null);
            }
        }
    }

    protected override void OnClosed(EventArgs eventArgs)
    {
        if (_viewModel is not null)
        {
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
