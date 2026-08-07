using System.ComponentModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Animation;
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
    private bool _floatingButtonDragging;
    private bool _isImeVisible;
    private bool _restoreMapAfterIme;
    private bool _restorePanelFullscreenAfterIme;
    private int _imeBottomInsetPixels;
    private double _viewportHeightWithoutIme;
    private Point _movementPadDragStart;
    private Vector _movementPadTranslationAtDragStart;
    private CancellationTokenSource? _floatingButtonHoldCancellation;
    private Border? _pressedFloatingButton;
    private IPointer? _pressedFloatingPointer;
    private Point _floatingButtonPressPosition;
    private Point _floatingButtonPositionAtPress;
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
            _viewModel.FloatingButtons.CollectionChanged += OnFloatingButtonsChanged;
            RebuildFloatingButtons();
            ApplyMovementButtonScale();
            ApplyMobileControlsOpacity();
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
            _viewModel.FloatingButtons.CollectionChanged -= OnFloatingButtonsChanged;
        }

        ResetFloatingButtonGesture(releaseCapture: true);
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
            UpdateFloatingButtonVisibility();
        }

        if (eventArgs.PropertyName == nameof(MainWindowViewModel.MobileControlsOpacity))
        {
            ApplyMobileControlsOpacity();
        }

        if (eventArgs.PropertyName == nameof(MainWindowViewModel.MobileFloatingButtonScale))
        {
            RebuildFloatingButtons();
        }

        if (eventArgs.PropertyName == nameof(MainWindowViewModel.MobileMovementButtonScale))
        {
            ApplyMovementButtonScale();
        }
    }

    private void OnFloatingButtonsChanged(
        object? sender,
        NotifyCollectionChangedEventArgs eventArgs) =>
        RebuildFloatingButtons();

    public void SetImeState(bool isVisible, int bottomInsetPixels)
    {
        var wasVisible = _isImeVisible;
        _isImeVisible = isVisible;
        _imeBottomInsetPixels = Math.Max(0, bottomInsetPixels);

        var mapToggle = this.FindControl<Avalonia.Controls.Primitives.ToggleButton>("MapToggle");
        var fullscreenToggle =
            this.FindControl<Avalonia.Controls.Primitives.ToggleButton>(
                "PanelFullscreenToggle");
        if (mapToggle is not null)
        {
            if (isVisible && !wasVisible)
            {
                _restoreMapAfterIme = mapToggle.IsChecked == true;
                _restorePanelFullscreenAfterIme =
                    fullscreenToggle?.IsChecked == true;
                SetPanelFullscreen(false);
                mapToggle.IsChecked = false;
            }
            else if (!isVisible && wasVisible)
            {
                mapToggle.IsChecked = _restoreMapAfterIme;
                if (_restoreMapAfterIme
                    && _restorePanelFullscreenAfterIme)
                {
                    SetPanelFullscreen(true);
                }

                _restoreMapAfterIme = false;
                _restorePanelFullscreenAfterIme = false;
            }

            mapToggle.IsEnabled = !isVisible;
        }

        UpdateMovementPadVisibility();
        UpdateFloatingButtonVisibility();
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

    private void UpdateFloatingButtonVisibility()
    {
        var layer = this.FindControl<Canvas>("FloatingButtonLayer");
        if (layer is not null)
        {
            layer.IsVisible = !_isImeVisible
                              && _viewModel?.IsProfileSelected == true
                              && _viewModel.FloatingButtons.Count > 0;
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
        // MobileRoot is top-aligned so the explicitly shorter viewport consumes
        // the space above the IME instead of being centered and leaving a blank
        // strip where the collapsed map used to be.
        // Shrinking the terminal keeps its Auto-sized command row in normal layout,
        // directly above the IME, and reduces the output row by the command row too.
        var availableHeight = Math.Max(0, viewport.Bounds.Height - missingInset);
        root.Height = availableHeight;
        terminal.Height = availableHeight;
    }

    private void ImeViewport_OnSizeChanged(object? sender, SizeChangedEventArgs eventArgs)
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

    private void RebuildFloatingButtons()
    {
        var layer = this.FindControl<Canvas>("FloatingButtonLayer");
        if (layer is null || _viewModel is null)
        {
            return;
        }

        ResetFloatingButtonGesture(releaseCapture: true);
        layer.Children.Clear();
        var buttonScale = _viewModel.MobileFloatingButtonScale;
        foreach (var definition in _viewModel.FloatingButtons)
        {
            var scale = new ScaleTransform(1, 1)
            {
                Transitions =
                [
                    new DoubleTransition
                    {
                        Property = ScaleTransform.ScaleXProperty,
                        Duration = TimeSpan.FromMilliseconds(90),
                    },
                    new DoubleTransition
                    {
                        Property = ScaleTransform.ScaleYProperty,
                        Duration = TimeSpan.FromMilliseconds(90),
                    },
                ],
            };
            var button = new Border
            {
                Tag = definition,
                Width = Math.Clamp(50 + (definition.Name.Length * 7), 72, 160)
                    * buttonScale,
                Height = 48 * buttonScale,
                Padding = new Thickness(10 * buttonScale, 6 * buttonScale),
                CornerRadius = new CornerRadius(24 * buttonScale),
                Background = new SolidColorBrush(Color.Parse("#FF30373D")),
                BorderBrush = new SolidColorBrush(Color.Parse("#FFC9A84C")),
                BorderThickness = new Thickness(1),
                Opacity = _viewModel.MobileControlsOpacity,
                RenderTransform = scale,
                RenderTransformOrigin = RelativePoint.Center,
                Transitions =
                [
                    new DoubleTransition
                    {
                        Property = OpacityProperty,
                        Duration = TimeSpan.FromMilliseconds(90),
                    },
                ],
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = new TextBlock
                {
                    Text = definition.Name,
                    Foreground = Brushes.White,
                    FontSize = 14 * buttonScale,
                    FontWeight = FontWeight.SemiBold,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                },
            };

            button.PointerPressed += FloatingButton_OnPointerPressed;
            button.PointerMoved += FloatingButton_OnPointerMoved;
            button.PointerReleased += FloatingButton_OnPointerReleased;
            button.PointerCaptureLost += FloatingButton_OnPointerCaptureLost;
            layer.Children.Add(button);
        }

        UpdateFloatingButtonVisibility();
        Dispatcher.UIThread.Post(PositionFloatingButtons, DispatcherPriority.Loaded);
    }

    private void ApplyMovementButtonScale()
    {
        var grid = this.FindControl<Grid>("MovementPadGrid");
        var pad = this.FindControl<Border>("MovementPad");
        if (grid is null || pad is null || _viewModel is null)
        {
            return;
        }

        var scale = _viewModel.MobileMovementButtonScale;
        pad.Padding = new Thickness(7 * scale);
        pad.CornerRadius = new CornerRadius(14 * scale);
        grid.RowSpacing = 4 * scale;
        grid.ColumnSpacing = 4 * scale;

        grid.RowDefinitions[0].Height = new GridLength(30 * scale);
        for (var row = 1; row < grid.RowDefinitions.Count; row++)
        {
            grid.RowDefinitions[row].Height = new GridLength(54 * scale);
        }

        foreach (var column in grid.ColumnDefinitions)
        {
            column.Width = new GridLength(58 * scale);
        }

        var dragHandle = this.FindControl<Border>("MovementPadDragHandle");
        if (dragHandle is not null)
        {
            dragHandle.Width = 54 * scale;
            dragHandle.Height = 26 * scale;
            dragHandle.CornerRadius = new CornerRadius(8 * scale);
            if (dragHandle.Child is TextBlock dragIcon)
            {
                dragIcon.FontSize = 17 * scale;
            }
        }

        foreach (var button in grid.Children.OfType<Avalonia.Controls.Button>())
        {
            var size = 54 * scale;
            button.Width = size;
            button.Height = size;
            button.MinWidth = size;
            button.MinHeight = size;
            button.Padding = new Thickness(3 * scale);
            button.FontSize = (Grid.GetRow(button) == 4 ? 14 : 15) * scale;
            if (button.Content is TextBlock label)
            {
                label.MaxWidth = 46 * scale;
            }
        }

        var center = this.FindControl<Border>("MovementPadCenter");
        if (center is not null)
        {
            center.Width = 20 * scale;
            center.Height = 20 * scale;
            center.CornerRadius = new CornerRadius(10 * scale);
        }
    }

    private void FloatingButtonLayer_OnSizeChanged(
        object? sender,
        SizeChangedEventArgs eventArgs) =>
        PositionFloatingButtons();

    private void PositionFloatingButtons()
    {
        var layer = this.FindControl<Canvas>("FloatingButtonLayer");
        if (layer is null || layer.Bounds.Width <= 0 || layer.Bounds.Height <= 0)
        {
            return;
        }

        const double edgeMargin = 8;
        foreach (var control in layer.Children.OfType<Border>())
        {
            if (control.Tag is not FloatingButtonDefinition definition)
            {
                continue;
            }

            var maximumLeft = Math.Max(
                edgeMargin,
                layer.Bounds.Width - control.Width - edgeMargin);
            var maximumTop = Math.Max(
                edgeMargin,
                layer.Bounds.Height - control.Height - edgeMargin);
            Canvas.SetLeft(
                control,
                edgeMargin + (definition.X * (maximumLeft - edgeMargin)));
            Canvas.SetTop(
                control,
                edgeMargin + (definition.Y * (maximumTop - edgeMargin)));
        }
    }

    private void FloatingButton_OnPointerPressed(
        object? sender,
        PointerPressedEventArgs eventArgs)
    {
        if (sender is not Border button
            || !eventArgs.GetCurrentPoint(button).Properties.IsLeftButtonPressed)
        {
            return;
        }

        ResetFloatingButtonGesture(releaseCapture: true);
        _pressedFloatingButton = button;
        _pressedFloatingPointer = eventArgs.Pointer;
        _floatingButtonPressPosition = eventArgs.GetPosition(this);
        _floatingButtonPositionAtPress = new Point(
            Canvas.GetLeft(button),
            Canvas.GetTop(button));
        _floatingButtonHoldCancellation = new CancellationTokenSource();
        SetFloatingButtonPressVisual(button, isPressed: true);
        eventArgs.Pointer.Capture(button);
        _ = BeginFloatingButtonDragAfterHoldAsync(
            button,
            eventArgs.Pointer,
            _floatingButtonHoldCancellation.Token);
        eventArgs.Handled = true;
    }

    private async Task BeginFloatingButtonDragAfterHoldAsync(
        Border button,
        IPointer pointer,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(450), cancellationToken);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!cancellationToken.IsCancellationRequested
                    && ReferenceEquals(button, _pressedFloatingButton)
                    && ReferenceEquals(pointer, _pressedFloatingPointer))
                {
                    _floatingButtonDragging = true;
                    button.Opacity = 0.96;
                    if (button.RenderTransform is ScaleTransform scale)
                    {
                        scale.ScaleX = 1.03;
                        scale.ScaleY = 1.03;
                    }
                }
            });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Releasing a short tap intentionally cancels long-press recognition.
        }
    }

    private void FloatingButton_OnPointerMoved(
        object? sender,
        PointerEventArgs eventArgs)
    {
        if (!_floatingButtonDragging
            || sender is not Border button
            || !ReferenceEquals(button, _pressedFloatingButton))
        {
            return;
        }

        var delta = eventArgs.GetPosition(this) - _floatingButtonPressPosition;
        SetFloatingButtonPosition(
            button,
            _floatingButtonPositionAtPress.X + delta.X,
            _floatingButtonPositionAtPress.Y + delta.Y);
        eventArgs.Handled = true;
    }

    private async void FloatingButton_OnPointerReleased(
        object? sender,
        PointerReleasedEventArgs eventArgs)
    {
        if (sender is not Border button
            || !ReferenceEquals(button, _pressedFloatingButton))
        {
            return;
        }

        var wasDragging = _floatingButtonDragging;
        var definition = button.Tag as FloatingButtonDefinition;
        if (wasDragging && definition is not null)
        {
            PersistFloatingButtonPosition(button, definition);
        }

        ResetFloatingButtonGesture(releaseCapture: true);
        eventArgs.Handled = true;

        if (!wasDragging
            && definition is not null
            && _viewModel?.SendFloatingCommand.CanExecute(definition.Command) == true)
        {
            try
            {
                await _viewModel.SendFloatingCommand.ExecuteAsync(definition.Command);
            }
            catch (OperationCanceledException)
            {
                // The command can be cancelled when the shared mobile session is shutting down.
            }
            catch (Exception exception)
            {
                Log.Error(
                    "KillerMudClient",
                    $"Nie udało się obsłużyć pływającego przycisku: {exception}");
            }
        }
    }

    private void FloatingButton_OnPointerCaptureLost(
        object? sender,
        PointerCaptureLostEventArgs eventArgs)
    {
        if (ReferenceEquals(sender, _pressedFloatingButton))
        {
            ResetFloatingButtonGesture(releaseCapture: false);
        }
    }

    private void SetFloatingButtonPosition(Border button, double left, double top)
    {
        var layer = this.FindControl<Canvas>("FloatingButtonLayer");
        if (layer is null)
        {
            return;
        }

        const double edgeMargin = 8;
        Canvas.SetLeft(
            button,
            ViewportPositionCalculator.ClampOrCenter(
                left,
                edgeMargin,
                layer.Bounds.Width - button.Width - edgeMargin));
        Canvas.SetTop(
            button,
            ViewportPositionCalculator.ClampOrCenter(
                top,
                edgeMargin,
                layer.Bounds.Height - button.Height - edgeMargin));
    }

    private void PersistFloatingButtonPosition(
        Border button,
        FloatingButtonDefinition definition)
    {
        var layer = this.FindControl<Canvas>("FloatingButtonLayer");
        if (layer is null || _viewModel is null)
        {
            return;
        }

        const double edgeMargin = 8;
        var horizontalRange = Math.Max(
            1,
            layer.Bounds.Width - button.Width - (2 * edgeMargin));
        var verticalRange = Math.Max(
            1,
            layer.Bounds.Height - button.Height - (2 * edgeMargin));
        _viewModel.MoveFloatingButton(
            definition.Id,
            (Canvas.GetLeft(button) - edgeMargin) / horizontalRange,
            (Canvas.GetTop(button) - edgeMargin) / verticalRange);
    }

    private void ResetFloatingButtonGesture(bool releaseCapture)
    {
        _floatingButtonHoldCancellation?.Cancel();
        _floatingButtonHoldCancellation?.Dispose();
        _floatingButtonHoldCancellation = null;

        var button = _pressedFloatingButton;
        var pointer = _pressedFloatingPointer;
        _pressedFloatingButton = null;
        _pressedFloatingPointer = null;
        _floatingButtonDragging = false;
        if (button is not null)
        {
            SetFloatingButtonPressVisual(button, isPressed: false);
        }

        if (releaseCapture)
        {
            pointer?.Capture(null);
        }
    }

    private void SetFloatingButtonPressVisual(Border button, bool isPressed)
    {
        var baseOpacity = _viewModel?.MobileControlsOpacity ?? 0.76;
        button.Opacity = baseOpacity;
        if (button.RenderTransform is ScaleTransform scale)
        {
            var targetScale = isPressed ? 0.95 : 1;
            scale.ScaleX = targetScale;
            scale.ScaleY = targetScale;
        }
    }

    private void ApplyMobileControlsOpacity()
    {
        var opacity = _viewModel?.MobileControlsOpacity ?? 0.76;
        var movementPad = this.FindControl<Border>("MovementPad");
        if (movementPad is not null)
        {
            movementPad.Opacity = opacity;
        }

        var floatingButtonLayer = this.FindControl<Canvas>("FloatingButtonLayer");
        if (floatingButtonLayer is null)
        {
            return;
        }

        foreach (var button in floatingButtonLayer.Children.OfType<Border>())
        {
            if (!ReferenceEquals(button, _pressedFloatingButton))
            {
                button.Opacity = opacity;
            }
        }
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

    private async void Disconnect_OnClick(object? sender, RoutedEventArgs eventArgs)
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
        }
        catch (Exception exception)
        {
            Log.Error("KillerMudClient", $"Nie udało się rozłączyć: {exception}");
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

    private void OpenHelp_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        OpenToolOverlay("Pomoc", "MobileHelpPanel");
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

        var fullscreenToggle =
            this.FindControl<Avalonia.Controls.Primitives.ToggleButton>(
                "PanelFullscreenToggle");
        if (fullscreenToggle?.IsChecked == true)
        {
            SetPanelFullscreen(false);
            return true;
        }

        return false;
    }

    private void MapTab_OnClick(object? sender, RoutedEventArgs eventArgs) =>
        SelectAuxiliaryTab("Map");

    private void BuffsTab_OnClick(object? sender, RoutedEventArgs eventArgs) =>
        SelectAuxiliaryTab("Buffs");

    private void GroupTab_OnClick(object? sender, RoutedEventArgs eventArgs) =>
        SelectAuxiliaryTab("Group");

    private void ChatTab_OnClick(object? sender, RoutedEventArgs eventArgs) =>
        SelectAuxiliaryTab("Chat");

    private void SelectAuxiliaryTab(string activeTab)
    {
        var mapButton = this.FindControl<Avalonia.Controls.Primitives.ToggleButton>("MapTabButton");
        var buffsButton = this.FindControl<Avalonia.Controls.Primitives.ToggleButton>("BuffsTabButton");
        var groupButton = this.FindControl<Avalonia.Controls.Primitives.ToggleButton>("GroupTabButton");
        var chatButton = this.FindControl<Avalonia.Controls.Primitives.ToggleButton>("ChatTabButton");
        
        var mapPanel = this.FindControl<Control>("MobileMapPanel");
        var buffsPanel = this.FindControl<Control>("MobileBuffsPanel");
        var groupPanel = this.FindControl<Control>("MobileGroupPanel");
        var chatPanel = this.FindControl<Control>("MobileChatPanel");

        if (mapButton is not null) mapButton.IsChecked = activeTab == "Map";
        if (buffsButton is not null) buffsButton.IsChecked = activeTab == "Buffs";
        if (groupButton is not null) groupButton.IsChecked = activeTab == "Group";
        if (chatButton is not null) chatButton.IsChecked = activeTab == "Chat";

        if (mapPanel is not null) mapPanel.IsVisible = activeTab == "Map";
        if (buffsPanel is not null) buffsPanel.IsVisible = activeTab == "Buffs";
        if (groupPanel is not null) groupPanel.IsVisible = activeTab == "Group";
        if (chatPanel is not null) chatPanel.IsVisible = activeTab == "Chat";
    }

    private void PanelFullscreen_OnClick(
        object? sender,
        RoutedEventArgs eventArgs) =>
        ApplyAuxiliaryPanelLayout();

    private void AuxiliaryPanelToggle_OnClick(
        object? sender,
        RoutedEventArgs eventArgs)
    {
        var mapToggle =
            this.FindControl<Avalonia.Controls.Primitives.ToggleButton>("MapToggle");
        var fullscreenToggle =
            this.FindControl<Avalonia.Controls.Primitives.ToggleButton>(
                "PanelFullscreenToggle");
        if (mapToggle?.IsChecked != true && fullscreenToggle?.IsChecked == true)
        {
            SetPanelFullscreen(false);
        }
    }

    private void SetPanelFullscreen(bool isFullscreen)
    {
        var fullscreenToggle =
            this.FindControl<Avalonia.Controls.Primitives.ToggleButton>(
                "PanelFullscreenToggle");
        if (fullscreenToggle is not null)
        {
            fullscreenToggle.IsChecked = isFullscreen;
        }

        ApplyAuxiliaryPanelLayout();
    }

    private void ApplyAuxiliaryPanelLayout()
    {
        var panel = this.FindControl<Border>("AuxiliaryPanel");
        var root = this.FindControl<Grid>("MobileRoot");
        var viewport = this.FindControl<Border>("ImeViewport");
        var terminal =
            this.FindControl<MudClient.App.Views.Panels.TerminalPanelView>(
                "MobileTerminal");
        var fullscreenToggle =
            this.FindControl<Avalonia.Controls.Primitives.ToggleButton>(
                "PanelFullscreenToggle");
        if (panel is null || root is null || terminal is null)
        {
            return;
        }

        var isFullscreen = fullscreenToggle?.IsChecked == true;
        panel.Height = isFullscreen ? double.NaN : 200;
        root.RowDefinitions[0].Height = isFullscreen
            ? new GridLength(1, GridUnitType.Star)
            : GridLength.Auto;
        root.RowDefinitions[1].Height = isFullscreen
            ? new GridLength(0)
            : new GridLength(1, GridUnitType.Star);
        if (isFullscreen && viewport is not null && viewport.Bounds.Height > 0)
        {
            root.Height = viewport.Bounds.Height;
        }
        else if (!_isImeVisible)
        {
            root.Height = double.NaN;
        }

        terminal.IsVisible = !isFullscreen;
    }

    private void OpenToolOverlay(string title, string activePanelName)
    {
        foreach (var panelName in new[]
                 {
                     "MobileSettingsPanel",
                     "MobileAutomationPanel",
                     "MobileAutowalkPanel",
                     "MobileHelpPanel",
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
