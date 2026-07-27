using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using MudClient.App.Models;
using MudClient.App.ViewModels;

namespace MudClient.App.Controls;

/// <summary>
/// Positions and drives drag/resize for the overlay card defined in the matching .axaml file.
/// Geometry is tracked as fractions (0..1) of this control's own bounds so the overlay always
/// stays proportionally inside the Terminal panel regardless of window size; see
/// <see cref="MainWindowViewModel.CommitOverlayGeometry"/> for why commits are batched to
/// pointer-release rather than written on every pointer-moved frame.
/// </summary>
public sealed partial class TerminalOverlayHost : UserControl
{
    private readonly Border _overlayCard;
    private readonly Border _resizeGrip;
    private readonly Border _titleBar;

    private MainWindowViewModel? _viewModel;

    private bool _isDraggingTitle;
    private bool _isResizing;
    private Point _dragStartPointerPos;
    private double _dragStartXFraction;
    private double _dragStartYFraction;
    private double _dragStartWidthFraction;
    private double _dragStartHeightFraction;
    private double? _liveXFraction;
    private double? _liveYFraction;
    private double? _liveWidthFraction;
    private double? _liveHeightFraction;

    public TerminalOverlayHost()
    {
        InitializeComponent();
        _overlayCard = this.FindControl<Border>("OverlayCard")
            ?? throw new InvalidOperationException("OverlayCard not found.");
        _resizeGrip = this.FindControl<Border>("ResizeGrip")
            ?? throw new InvalidOperationException("ResizeGrip not found.");
        _titleBar = this.FindControl<Border>("TitleBar")
            ?? throw new InvalidOperationException("TitleBar not found.");

        _titleBar.PointerPressed += TitleBar_OnPointerPressed;
        _titleBar.PointerMoved += TitleBar_OnPointerMoved;
        _titleBar.PointerReleased += TitleBar_OnPointerReleased;
        _resizeGrip.PointerPressed += ResizeGrip_OnPointerPressed;
        _resizeGrip.PointerMoved += ResizeGrip_OnPointerMoved;
        _resizeGrip.PointerReleased += ResizeGrip_OnPointerReleased;

        DataContextChanged += OnDataContextChanged;
        SizeChanged += (_, _) => UpdateOverlayGeometry();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateOverlayGeometry();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(MainWindowViewModel.OverlayPanel):
            case nameof(MainWindowViewModel.TerminalOverlayXFraction):
            case nameof(MainWindowViewModel.TerminalOverlayYFraction):
            case nameof(MainWindowViewModel.TerminalOverlayWidthFraction):
            case nameof(MainWindowViewModel.TerminalOverlayHeightFraction):
                UpdateOverlayGeometry();
                break;
        }
    }

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel?.OverlayPanel is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isDraggingTitle = true;
        _dragStartPointerPos = e.GetPosition(this);
        _dragStartXFraction = _viewModel.TerminalOverlayXFraction;
        _dragStartYFraction = _viewModel.TerminalOverlayYFraction;
        e.Pointer.Capture(_titleBar);
        e.Handled = true;
    }

    private void TitleBar_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isDraggingTitle || _viewModel is null)
        {
            return;
        }

        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var pos = e.GetPosition(this);
        var deltaXFraction = (pos.X - _dragStartPointerPos.X) / size.Width;
        var deltaYFraction = (pos.Y - _dragStartPointerPos.Y) / size.Height;

        var width = _viewModel.TerminalOverlayWidthFraction;
        var height = _viewModel.TerminalOverlayHeightFraction;
        _liveXFraction = Math.Clamp(_dragStartXFraction + deltaXFraction, 0, Math.Max(0, 1 - width));
        _liveYFraction = Math.Clamp(_dragStartYFraction + deltaYFraction, 0, Math.Max(0, 1 - height));
        UpdateOverlayGeometry();
    }

    private void TitleBar_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isDraggingTitle)
        {
            return;
        }

        _isDraggingTitle = false;
        e.Pointer.Capture(null);

        if (_viewModel is not null && _liveXFraction is { } x && _liveYFraction is { } y)
        {
            _viewModel.CommitOverlayGeometry(
                x, y, _viewModel.TerminalOverlayWidthFraction, _viewModel.TerminalOverlayHeightFraction);
        }

        _liveXFraction = null;
        _liveYFraction = null;
    }

    private void ResizeGrip_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel?.OverlayPanel is null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isResizing = true;
        _dragStartPointerPos = e.GetPosition(this);
        _dragStartWidthFraction = _viewModel.TerminalOverlayWidthFraction;
        _dragStartHeightFraction = _viewModel.TerminalOverlayHeightFraction;
        e.Pointer.Capture(_resizeGrip);
        e.Handled = true;
    }

    private void ResizeGrip_OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isResizing || _viewModel is null)
        {
            return;
        }

        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var pos = e.GetPosition(this);
        var deltaWidthFraction = (pos.X - _dragStartPointerPos.X) / size.Width;
        var deltaHeightFraction = (pos.Y - _dragStartPointerPos.Y) / size.Height;

        var x = _viewModel.TerminalOverlayXFraction;
        var y = _viewModel.TerminalOverlayYFraction;
        _liveWidthFraction = Math.Clamp(
            _dragStartWidthFraction + deltaWidthFraction,
            AppSettings.MinTerminalOverlaySizeFraction,
            Math.Max(AppSettings.MinTerminalOverlaySizeFraction, Math.Min(AppSettings.MaxTerminalOverlaySizeFraction, 1 - x)));
        _liveHeightFraction = Math.Clamp(
            _dragStartHeightFraction + deltaHeightFraction,
            AppSettings.MinTerminalOverlaySizeFraction,
            Math.Max(AppSettings.MinTerminalOverlaySizeFraction, Math.Min(AppSettings.MaxTerminalOverlaySizeFraction, 1 - y)));
        UpdateOverlayGeometry();
    }

    private void ResizeGrip_OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isResizing)
        {
            return;
        }

        _isResizing = false;
        e.Pointer.Capture(null);

        if (_viewModel is not null && _liveWidthFraction is { } w && _liveHeightFraction is { } h)
        {
            _viewModel.CommitOverlayGeometry(
                _viewModel.TerminalOverlayXFraction, _viewModel.TerminalOverlayYFraction, w, h);
        }

        _liveWidthFraction = null;
        _liveHeightFraction = null;
    }

    private void UpdateOverlayGeometry()
    {
        if (_viewModel?.OverlayPanel is null)
        {
            return;
        }

        var size = Bounds.Size;
        if (size.Width <= 0 || size.Height <= 0)
        {
            return;
        }

        var xFraction = _liveXFraction ?? _viewModel.TerminalOverlayXFraction;
        var yFraction = _liveYFraction ?? _viewModel.TerminalOverlayYFraction;
        var widthFraction = _liveWidthFraction ?? _viewModel.TerminalOverlayWidthFraction;
        var heightFraction = _liveHeightFraction ?? _viewModel.TerminalOverlayHeightFraction;

        var x = xFraction * size.Width;
        var y = yFraction * size.Height;
        var width = Math.Max(120, widthFraction * size.Width);
        var height = Math.Max(80, heightFraction * size.Height);

        Canvas.SetLeft(_overlayCard, x);
        Canvas.SetTop(_overlayCard, y);
        _overlayCard.Width = width;
        _overlayCard.Height = height;

        Canvas.SetLeft(_resizeGrip, x + width - _resizeGrip.Width);
        Canvas.SetTop(_resizeGrip, y + height - _resizeGrip.Height);
    }
}
