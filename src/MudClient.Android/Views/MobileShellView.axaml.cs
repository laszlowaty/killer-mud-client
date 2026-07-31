using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Android.Util;
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

        transform.X = Math.Clamp(requestedTranslation.X, minX, maxX);
        transform.Y = Math.Clamp(requestedTranslation.Y, minY, maxY);
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
        var overlay = this.FindControl<Border>("ToolOverlay");
        if (overlay is not null)
        {
            overlay.IsVisible = false;
        }
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

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
