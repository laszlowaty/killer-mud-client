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
    private Dock.Avalonia.Controls.DockControl? _mainDock;
    internal Func<Window, string, string, Task<bool>> ConfirmDeletionAsync { get; set; } =
        DeleteConfirmationDialog.ShowAsync;

    public Exception? DeferredSettingsImportError { get; init; }

    public MainWindow()
    {
        InitializeComponent();

        Opened += OnOpened;
        Activated += OnWindowActivated;
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
        DataContextChanged += (_, _) =>
        {
            _viewModel = DataContext as MainWindowViewModel;
            // Pinned edge tabs use fixed proportions of the live dock area: one third of its
            // width at the sides and half its height at the top/bottom. The view supplies the
            // dimensions because the UI-agnostic factory cannot see the rendered DockControl.
            _viewModel?.ConfigurePinnedPreviewSize(GetPinnedPreviewSize);
        };

        // Safety net: when a dock drag ends (drop or cancel, anywhere in the window),
        // reclaim any panel the drag pipeline dropped on the floor so it reappears in
        // the "Panele" restore menu instead of vanishing.
        var mainDock = this.FindControl<Dock.Avalonia.Controls.DockControl>("MainDock");
        _mainDock = mainDock;
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
            if (DeferredSettingsImportError is not null)
            {
                _viewModel.ReportSettingsImportError(DeferredSettingsImportError);
            }
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

    private async void DeleteProfile_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { DataContext: string profileName } button || _viewModel is null)
        {
            return;
        }

        button.IsEnabled = false;
        try
        {
            if (await ConfirmDeletionAsync(this, "profil", profileName))
            {
                _viewModel.DeleteProfileCommand.Execute(profileName);
            }
        }
        finally
        {
            button.IsEnabled = true;
        }
    }

    /// <summary>
    /// The fixed preview size for the given edge: one third of the live dock width for a side
    /// (Left/Right) tab and half its height for a top/bottom tab. Falls back to the window client
    /// size before the dock has been laid out.
    /// </summary>
    private double GetPinnedPreviewSize(Dock.Model.Core.Alignment edge)
    {
        var width = _mainDock?.Bounds.Width ?? 0;
        var height = _mainDock?.Bounds.Height ?? 0;
        if (width <= 0 || height <= 0)
        {
            width = ClientSize.Width;
            height = ClientSize.Height;
        }

        var side = edge is Dock.Model.Core.Alignment.Left or Dock.Model.Core.Alignment.Right;
        return side ? width / 3.0 : height / 2.0;
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

    private void SelectProfileField_OnKeyDown(object? sender, KeyEventArgs eventArgs)
        => ExecuteOnEnter(eventArgs, _viewModel?.SelectProfileCommand);

    private void CreateProfileField_OnKeyDown(object? sender, KeyEventArgs eventArgs)
        => ExecuteOnEnter(eventArgs, _viewModel?.CreateProfileCommand);

    private static void ExecuteOnEnter(KeyEventArgs eventArgs, System.Windows.Input.ICommand? command)
    {
        if (eventArgs.Key is not (Key.Enter or Key.Return) || command is null)
        {
            return;
        }

        eventArgs.Handled = true;
        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
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
