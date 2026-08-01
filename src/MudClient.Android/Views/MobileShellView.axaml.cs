using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Android.Util;
using MudClient.App.Models;
using MudClient.App.ViewModels;
using MudClient.Android.Services;

namespace MudClient.Android.Views;

public sealed partial class MobileShellView : UserControl
{
    private readonly MobileSessionHost _sessionHost;
    private readonly CancellationTokenSource _initializationCancellation = new();
    private bool _initializationStarted;
    private bool _mapInitializationStarted;
    private bool _movementPadDragging;
    private bool _isImeVisible;
    private bool _restoreMapAfterIme;
    private int _imeBottomInsetPixels;
    private double _viewportHeightWithoutIme;
    private Point _movementPadDragStart;
    private Vector _movementPadTranslationAtDragStart;
    private MainWindowViewModel? _viewModel;

    public MobileShellView()
        : this(new MobileSessionHost(global::Android.App.Application.Context))
    {
    }

    public MobileShellView(MobileSessionHost sessionHost)
    {
        _sessionHost = sessionHost;
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
        SizeChanged += OnViewportSizeChanged;
    }

    private async void OnAttachedToVisualTree(
        object? sender,
        Avalonia.VisualTreeAttachmentEventArgs eventArgs)
    {
        if (_initializationStarted)
        {
            return;
        }

        _initializationStarted = true;
        try
        {
            _viewModel = await _sessionHost.GetViewModelAsync(
                _initializationCancellation.Token);
            DataContext = _viewModel;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateMovementPadVisibility();
            var loadingOverlay = this.FindControl<Grid>("LoadingOverlay");
            if (loadingOverlay is not null)
            {
                loadingOverlay.IsVisible = false;
            }

            StartMapInitializationWhenConnected();
        }
        catch (OperationCanceledException) when (_initializationCancellation.IsCancellationRequested)
        {
            // Activity recreation detached this view while the shared host kept initializing.
        }
        catch (Exception exception)
        {
            Log.Error("KillerMudClient", exception.ToString());
            var loadingOverlay = this.FindControl<Grid>("LoadingOverlay");
            if (loadingOverlay is not null)
            {
                loadingOverlay.IsVisible = true;
            }

            var loadingText = this.FindControl<TextBlock>("LoadingText");
            if (loadingText is not null)
            {
                loadingText.Text = $"Nie udało się uruchomić aplikacji:\n{exception.Message}";
            }
        }
    }

    private void OnDetachedFromVisualTree(
        object? sender,
        Avalonia.VisualTreeAttachmentEventArgs eventArgs)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _initializationCancellation.Cancel();
    }

    private void OnViewModelPropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(MainWindowViewModel.IsProfileSelected))
        {
            StartMapInitializationWhenConnected();
            UpdateMovementPadVisibility();
        }
    }

    public void SetImeState(bool isVisible, int bottomInsetPixels)
    {
        var wasVisible = _isImeVisible;
        _isImeVisible = isVisible;
        _imeBottomInsetPixels = Math.Max(0, bottomInsetPixels);

        var mapToggle = this.FindControl<Avalonia.Controls.Primitives.ToggleButton>("MapToggle");
        if (mapToggle is not null)
        {
            if (isVisible && !wasVisible)
            {
                _restoreMapAfterIme = mapToggle.IsChecked == true;
                mapToggle.IsChecked = false;
            }
            else if (!isVisible && wasVisible)
            {
                mapToggle.IsChecked = _restoreMapAfterIme;
                _restoreMapAfterIme = false;
            }

            mapToggle.IsEnabled = !isVisible;
        }

        UpdateMovementPadVisibility();
        Dispatcher.UIThread.Post(
            ApplyImeInsetFallback,
            DispatcherPriority.Background);
    }

    private void UpdateMovementPadVisibility()
    {
        var movementPad = this.FindControl<Border>("MovementPad");
        if (movementPad is not null)
        {
            movementPad.IsVisible = !_isImeVisible
                                    && _viewModel?.IsProfileSelected == true;
        }
    }

    private void ApplyImeInsetFallback()
    {
        var viewport = this.FindControl<Border>("ImeViewport");
        var root = this.FindControl<Grid>("MobileRoot");
        var terminal = this.FindControl<MudClient.App.Views.Panels.TerminalPanelView>(
            "MobileTerminal");
        if (viewport is null
            || root is null
            || terminal is null
            || viewport.Bounds.Height <= 0)
        {
            return;
        }

        if (!_isImeVisible)
        {
            root.Height = double.NaN;
            terminal.Height = double.NaN;
            return;
        }

        if (_imeBottomInsetPixels <= 0 || _viewportHeightWithoutIme <= 0)
        {
            root.Height = double.NaN;
            terminal.Height = double.NaN;
            return;
        }

        var density = global::Android.App.Application.Context
                          .Resources?.DisplayMetrics?.Density
                      ?? 1;
        var imeInset = _imeBottomInsetPixels / Math.Max(1, density);
        var missingInset = ViewportInsetCalculator.CalculateMissingBottomInset(
            _viewportHeightWithoutIme,
            viewport.Bounds.Height,
            imeInset);

        // adjustResize normally supplies the whole reduction. Android 15+
        // edge-to-edge can instead expose only IME insets. An explicit height
        // is required here because the Android TopLevel can retain the old
        // DesiredSize and otherwise arrange the Auto command row below its clip.
        // Shrinking the terminal keeps its Auto-sized command row in normal layout,
        // directly above the IME, and reduces the output row by the command row too.
        var availableHeight = Math.Max(0, viewport.Bounds.Height - missingInset);
        root.Height = availableHeight;
        terminal.Height = availableHeight;
    }

    private void OnViewportSizeChanged(object? sender, SizeChangedEventArgs eventArgs)
    {
        if (!_isImeVisible && eventArgs.NewSize.Height > 0)
        {
            _viewportHeightWithoutIme = eventArgs.NewSize.Height;
            return;
        }

        if (_isImeVisible)
        {
            Dispatcher.UIThread.Post(
                ApplyImeInsetFallback,
                DispatcherPriority.Background);
        }
    }

    private async void StartMapInitializationWhenConnected()
    {
        if (_mapInitializationStarted || _viewModel?.IsProfileSelected != true)
        {
            return;
        }

        _mapInitializationStarted = true;
        try
        {
            await _sessionHost.EnsureViewModelInitializedAsync(
                _initializationCancellation.Token);
        }
        catch (OperationCanceledException) when (_initializationCancellation.IsCancellationRequested)
        {
            // Activity recreation detached this view while initialization continued.
        }
        catch (Exception exception)
        {
            Log.Error("KillerMudClient", exception.ToString());
        }
    }

    private void MovementPadDragHandle_OnPointerPressed(
        object? sender,
        PointerPressedEventArgs eventArgs)
    {
        if (sender is not Control handle)
        {
            return;
        }

        var transform = this.FindControl<Border>("MovementPad")?.RenderTransform
            as TranslateTransform;
        if (transform is null)
        {
            return;
        }

        _movementPadDragging = true;
        _movementPadDragStart = eventArgs.GetPosition(this);
        _movementPadTranslationAtDragStart = new Vector(transform.X, transform.Y);
        eventArgs.Pointer.Capture(handle);
        eventArgs.Handled = true;
    }

    private void MovementPadDragHandle_OnPointerMoved(
        object? sender,
        PointerEventArgs eventArgs)
    {
        if (!_movementPadDragging)
        {
            return;
        }

        var currentPosition = eventArgs.GetPosition(this);
        var dragDelta = currentPosition - _movementPadDragStart;
        var requestedTranslation = _movementPadTranslationAtDragStart + dragDelta;
        SetMovementPadTranslation(requestedTranslation);
        eventArgs.Handled = true;
    }

    private void MovementPadDragHandle_OnPointerReleased(
        object? sender,
        PointerReleasedEventArgs eventArgs)
    {
        EndMovementPadDrag(eventArgs.Pointer);
        eventArgs.Handled = true;
    }

    private void MovementPadDragHandle_OnPointerCaptureLost(
        object? sender,
        PointerCaptureLostEventArgs eventArgs)
    {
        _movementPadDragging = false;
    }

    private void MovementPad_OnSizeChanged(object? sender, SizeChangedEventArgs eventArgs)
    {
        var transform = this.FindControl<Border>("MovementPad")?.RenderTransform
            as TranslateTransform;
        if (transform is not null)
        {
            SetMovementPadTranslation(new Vector(transform.X, transform.Y));
        }
    }

    private void SetMovementPadTranslation(Vector requestedTranslation)
    {
        var pad = this.FindControl<Border>("MovementPad");
        var transform = pad?.RenderTransform as TranslateTransform;
        if (pad is null || transform is null || Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        const double edgeMargin = 8;
        var minX = edgeMargin - pad.Bounds.X;
        var maxX = Bounds.Width - edgeMargin - pad.Bounds.Right;
        var minY = edgeMargin - pad.Bounds.Y;
        var maxY = Bounds.Height - edgeMargin - pad.Bounds.Bottom;

        transform.X = ViewportPositionCalculator.ClampOrCenter(
            requestedTranslation.X,
            minX,
            maxX);
        transform.Y = ViewportPositionCalculator.ClampOrCenter(
            requestedTranslation.Y,
            minY,
            maxY);
    }

    private void EndMovementPadDrag(IPointer pointer)
    {
        _movementPadDragging = false;
        pointer.Capture(null);
    }

    private void OpenSettings_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        OpenToolOverlay("Ustawienia", "MobileSettingsPanel");
    }

    private async void SwitchProfile_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        var viewModel = _viewModel;
        this.FindControl<Avalonia.Controls.Button>("MobileMenuButton")?.Flyout?.Hide();
        HideToolOverlay();

        if (viewModel is null || viewModel.IsBusy)
        {
            return;
        }

        try
        {
            if (viewModel.IsConnected && viewModel.DisconnectCommand.CanExecute(null))
            {
                await viewModel.DisconnectCommand.ExecuteAsync(null);
            }

            if (viewModel.SwitchProfileCommand.CanExecute(null))
            {
                viewModel.SwitchProfileCommand.Execute(null);
            }
        }
        catch (Exception exception)
        {
            Log.Error("KillerMudClient", $"Nie udało się zmienić profilu: {exception}");
        }
    }

    private void OpenAutomation_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        OpenToolOverlay("Automaty", "MobileAutomationPanel");
    }

    private void OpenAutowalk_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        OpenToolOverlay("Autowalk", "MobileAutowalkPanel");
    }

    private void CloseToolOverlay_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        HideToolOverlay();
    }

    public bool TryNavigateBackToTerminal()
    {
        var overlay = this.FindControl<Border>("ToolOverlay");
        if (overlay?.IsVisible == true)
        {
            HideToolOverlay();
            return true;
        }

        var menuFlyout = this.FindControl<Avalonia.Controls.Button>("MobileMenuButton")?.Flyout;
        if (menuFlyout?.IsOpen == true)
        {
            menuFlyout.Hide();
            return true;
        }

        return false;
    }

    private void OpenToolOverlay(string title, string activePanelName)
    {
        foreach (var panelName in new[]
                 {
                     "MobileSettingsPanel",
                     "MobileAutomationPanel",
                     "MobileAutowalkPanel",
                 })
        {
            var panel = this.FindControl<Control>(panelName);
            if (panel is not null)
            {
                panel.IsVisible = panelName == activePanelName;
            }
        }

        var titleBlock = this.FindControl<TextBlock>("ToolOverlayTitle");
        if (titleBlock is not null)
        {
            titleBlock.Text = title;
        }

        var overlay = this.FindControl<Border>("ToolOverlay");
        if (overlay is not null)
        {
            overlay.IsVisible = true;
        }

        this.FindControl<Avalonia.Controls.Button>("MobileMenuButton")?.Flyout?.Hide();
    }

    private void HideToolOverlay()
    {
        var overlay = this.FindControl<Border>("ToolOverlay");
        if (overlay is not null)
        {
            overlay.IsVisible = false;
        }
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
