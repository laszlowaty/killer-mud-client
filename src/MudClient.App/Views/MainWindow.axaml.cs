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
        Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;
        DataContextChanged += (_, _) => _viewModel = DataContext as MainWindowViewModel;

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
        if (FocusManager?.GetFocusedElement() is TextBox)
        {
            return;
        }

        var terminal = TerminalPanelView.Current;
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
