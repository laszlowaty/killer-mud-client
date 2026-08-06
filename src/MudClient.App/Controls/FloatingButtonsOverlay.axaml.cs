using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using MudClient.App.Models;
using MudClient.App.ViewModels;

namespace MudClient.App.Controls;

public sealed partial class FloatingButtonsOverlay : UserControl
{
    private MainWindowViewModel? _viewModel;
    private CancellationTokenSource? _floatingButtonHoldCancellation;
    private Border? _pressedFloatingButton;
    private IPointer? _pressedFloatingPointer;
    private bool _floatingButtonDragging;
    private Point _floatingButtonPressPosition;
    private Point _floatingButtonPositionAtPress;

    public FloatingButtonsOverlay()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += OnDetachedFromVisualTree;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.FloatingButtons.CollectionChanged -= OnFloatingButtonsChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.FloatingButtons.CollectionChanged += OnFloatingButtonsChanged;
            RebuildFloatingButtons();
        }
    }

    private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        ResetFloatingButtonGesture(releaseCapture: true);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsProfileSelected))
        {
            UpdateFloatingButtonVisibility();
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.MobileControlsOpacity))
        {
            ApplyMobileControlsOpacity();
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.MobileFloatingButtonScale))
        {
            RebuildFloatingButtons();
        }
    }

    private void OnFloatingButtonsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        RebuildFloatingButtons();

    private void UpdateFloatingButtonVisibility()
    {
        var layer = this.FindControl<Canvas>("FloatingButtonLayer");
        if (layer is not null)
        {
            layer.IsVisible = _viewModel?.IsProfileSelected == true
                              && _viewModel.FloatingButtons.Count > 0;
        }
    }

    private void FloatingButtonLayer_OnSizeChanged(object? sender, SizeChangedEventArgs e) =>
        PositionFloatingButtons();

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
                Width = Math.Clamp(50 + (definition.Name.Length * 7), 72, 160) * buttonScale,
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

    private void FloatingButton_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border button
            || !e.GetCurrentPoint(button).Properties.IsLeftButtonPressed)
        {
            return;
        }

        ResetFloatingButtonGesture(releaseCapture: true);
        _pressedFloatingButton = button;
        _pressedFloatingPointer = e.Pointer;
        _floatingButtonPressPosition = e.GetPosition(this);
        _floatingButtonPositionAtPress = new Point(
            Canvas.GetLeft(button),
            Canvas.GetTop(button));
        _floatingButtonHoldCancellation = new CancellationTokenSource();
        SetFloatingButtonPressVisual(button, isPressed: true);
        e.Pointer.Capture(button);
        _ = BeginFloatingButtonDragAfterHoldAsync(
            button,
            e.Pointer,
            _floatingButtonHoldCancellation.Token);
        e.Handled = true;
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

    private void FloatingButton_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_floatingButtonDragging
            || sender is not Border button
            || !ReferenceEquals(button, _pressedFloatingButton))
        {
            return;
        }

        var delta = e.GetPosition(this) - _floatingButtonPressPosition;
        SetFloatingButtonPosition(
            button,
            _floatingButtonPositionAtPress.X + delta.X,
            _floatingButtonPositionAtPress.Y + delta.Y);
        e.Handled = true;
    }

    private async void FloatingButton_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
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
        e.Handled = true;

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
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.WriteLine(
                    $"Nie udało się obsłużyć pływającego przycisku: {exception}");
            }
        }
    }

    private void FloatingButton_OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
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
            Math.Clamp(
                left,
                edgeMargin,
                Math.Max(edgeMargin, layer.Bounds.Width - button.Width - edgeMargin)));
        Canvas.SetTop(
            button,
            Math.Clamp(
                top,
                edgeMargin,
                Math.Max(edgeMargin, layer.Bounds.Height - button.Height - edgeMargin)));
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
}
